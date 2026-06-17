using System.Collections.Concurrent;
using ImagePilot.Api.Models;
using Microsoft.Playwright;

namespace ImagePilot.Api.Services;

public sealed class BrowserAutomationService(AppPaths paths, ILogger<BrowserAutomationService> logger) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, IBrowserContext> _contexts = new();
    private IPlaywright? _playwright;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);

    public async Task OpenProviderAsync(ProviderRecord provider)
    {
        try
        {
            await OpenProviderCoreAsync(provider);
        }
        catch (Exception exception) when (IsClosedContextException(exception))
        {
            await ResetContextAsync(provider);
            await OpenProviderCoreAsync(provider);
        }
    }

    private async Task OpenProviderCoreAsync(ProviderRecord provider)
    {
        var context = await GetContextAsync(provider);
        var page = context.Pages.LastOrDefault() ?? await context.NewPageAsync();
        await NavigateToProviderAsync(page, provider);
        await page.BringToFrontAsync();
    }

    public async Task OpenChromeProfileAsync(ChromeProfileRecord profile)
    {
        var provider = new ProviderRecord
        {
            Id = -profile.Id,
            Name = profile.Name,
            StartUrl = profile.StartUrl,
            BrowserProfilePath = profile.BrowserProfilePath,
            BrowserChannel = profile.BrowserChannel
        };
        await OpenProviderAsync(provider);
    }

    public async Task<AutomationResult> SubmitAsync(ProjectRecord project, ProviderRecord provider, PromptJobRecord job)
    {
        if (!provider.AutomationEnabled)
        {
            return AutomationResult.Manual("Manual mode is active. Copy the prompt, finish the website step, then mark the job complete.");
        }

        try
        {
            IBrowserContext context;
            IPage page;
            try
            {
                context = await GetContextAsync(provider);
                page = context.Pages.LastOrDefault() ?? await context.NewPageAsync();
                await NavigateToProviderAsync(page, provider);
                await page.BringToFrontAsync();
            }
            catch (Exception exception) when (IsClosedContextException(exception))
            {
                await ResetContextAsync(provider);
                context = await GetContextAsync(provider);
                page = context.Pages.LastOrDefault() ?? await context.NewPageAsync();
                await NavigateToProviderAsync(page, provider);
                await page.BringToFrontAsync();
            }

            if (!string.IsNullOrWhiteSpace(provider.VerificationSelector) &&
                await page.Locator(provider.VerificationSelector).CountAsync() > 0)
            {
                return AutomationResult.Manual("Verification was detected. Complete it in the browser, then resume the run.");
            }

            await page.Locator(provider.PromptInputSelector).FillAsync(job.Prompt);
            await Task.Delay(500); // Small delay to let the UI register the input

            var submitted = false;
            if (provider.SubmitButtonSelector.Equals("Enter", StringComparison.OrdinalIgnoreCase))
            {
                await page.Locator(provider.PromptInputSelector).PressAsync("Enter");
                submitted = true;
            }
            else
            {
                try
                {
                    await page.Locator(provider.SubmitButtonSelector).First.ClickAsync(
                        new LocatorClickOptions { Timeout = 5000 });
                    submitted = true;
                }
                catch
                {
                    // Selector failed — fallback to pressing Enter
                    logger.LogInformation("Submit button selector failed, falling back to Enter key for job {JobId}", job.Id);
                    await page.Locator(provider.PromptInputSelector).PressAsync("Enter");
                    submitted = true;
                }
            }

            // Wait for result to appear — try configured selector, fallback to generic wait
            try
            {
                if (!string.IsNullOrWhiteSpace(provider.ResultContainerSelector))
                {
                    await page.Locator(provider.ResultContainerSelector).Last.WaitForAsync(new LocatorWaitForOptions
                    {
                        Timeout = provider.DefaultTimeoutSeconds * 1000
                    });
                }
                else
                {
                    await Task.Delay(15000); // No selector configured — just wait
                }
            }
            catch (TimeoutException)
            {
                logger.LogInformation("Result selector timed out for job {JobId}, will attempt image capture anyway", job.Id);
            }

            // Wait a bit extra for the image to fully render
            await Task.Delay(3000);

            // Retry image capture up to 3 times with 5-second gaps
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                var capturedPath = await TrySaveLatestImageAsync(page, project, provider, job);
                if (capturedPath is not null)
                {
                    logger.LogInformation("Image captured on attempt {Attempt} for job {JobId}", attempt, job.Id);
                    return AutomationResult.Completed(capturedPath);
                }
                if (attempt < 3)
                {
                    logger.LogInformation("Image capture attempt {Attempt} returned null for job {JobId}, retrying...", attempt, job.Id);
                    await Task.Delay(5000);
                }
            }
            return AutomationResult.Manual("Result detected, but the image could not be captured after 3 attempts. Download it manually, then mark the job complete.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Browser automation failed for job {JobId}", job.Id);
            return provider.ManualModeFallback
                ? AutomationResult.Manual($"Automation paused: {exception.Message}")
                : AutomationResult.Failed(exception.Message);
        }
    }

    private async Task<string?> TrySaveLatestImageAsync(IPage page, ProjectRecord project, ProviderRecord provider, PromptJobRecord job)
    {
        try
        {
            // Find all large images on the page
            var allImages = page.Locator("img");
            var count = await allImages.CountAsync();

            // Iterate from last to first to find the most recent large image
            for (int i = count - 1; i >= 0; i--)
            {
                var img = allImages.Nth(i);
                try
                {
                    var isVisible = await img.IsVisibleAsync();
                    if (!isVisible) continue;

                    var naturalWidth = await img.EvaluateAsync<int>("el => el.naturalWidth");
                    var naturalHeight = await img.EvaluateAsync<int>("el => el.naturalHeight");
                    if (naturalWidth < 128 || naturalHeight < 128) continue;

                    var src = await img.EvaluateAsync<string?>("el => el.currentSrc || el.src || ''");
                    if (string.IsNullOrWhiteSpace(src) || src.StartsWith("chrome-extension:")) continue;

                    // Use Playwright's native screenshot — always produces valid PNG bytes
                    var screenshotBytes = await img.ScreenshotAsync(new LocatorScreenshotOptions
                    {
                        Type = ScreenshotType.Png
                    });

                    if (screenshotBytes.Length < 1000)
                    {
                        // Too small — likely a placeholder or icon
                        continue;
                    }

                    var outputPath = BuildOutputPath(project, provider, job, ".png");
                    Directory.CreateDirectory(project.OutputFolder);
                    await File.WriteAllBytesAsync(outputPath, screenshotBytes);
                    logger.LogInformation("Saved image ({Width}x{Height}, {Size} bytes) for job {JobId} to {Path}",
                        naturalWidth, naturalHeight, screenshotBytes.Length, job.Id, outputPath);
                    return outputPath;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Skipping image element {Index} for job {JobId}", i, job.Id);
                    continue;
                }
            }

            return null;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not capture latest image for job {JobId}", job.Id);
            return null;
        }
    }

    private async Task<IBrowserContext> GetContextAsync(ProviderRecord provider)
    {
        var contextKey = BuildContextKey(provider);
        if (_contexts.TryGetValue(contextKey, out var current))
        {
            return current;
        }

        await _initializationGate.WaitAsync();
        try
        {
            if (_contexts.TryGetValue(contextKey, out current))
            {
                return current;
            }

            _playwright ??= await Playwright.CreateAsync();
            var profilePath = string.IsNullOrWhiteSpace(provider.BrowserProfilePath)
                ? Path.Combine(paths.BrowserProfilesFolder, AppDataStore.Slugify(provider.Name))
                : provider.BrowserProfilePath;
            Directory.CreateDirectory(profilePath);

            var options = new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                AcceptDownloads = true,
                IgnoreDefaultArgs = new[] { "--enable-automation" },
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            };
            if (!string.IsNullOrWhiteSpace(provider.BrowserChannel))
            {
                options.Channel = provider.BrowserChannel;
            }

            current = await _playwright.Chromium.LaunchPersistentContextAsync(profilePath, options);
            _contexts[contextKey] = current;
            return current;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private static string BuildContextKey(ProviderRecord provider)
    {
        var profilePath = string.IsNullOrWhiteSpace(provider.BrowserProfilePath)
            ? provider.Name
            : Path.GetFullPath(provider.BrowserProfilePath);
        return $"{provider.Id}:{profilePath}";
    }

    private async Task ResetContextAsync(ProviderRecord provider)
    {
        var contextKey = BuildContextKey(provider);
        if (!_contexts.TryRemove(contextKey, out var context))
        {
            return;
        }

        try
        {
            await context.DisposeAsync();
        }
        catch (Exception exception) when (IsClosedContextException(exception))
        {
            logger.LogInformation(exception, "Closed browser context was removed for {Provider}", provider.Name);
        }
    }

    private static bool IsClosedContextException(Exception exception) =>
        exception.Message.Contains("has been closed", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("Target closed", StringComparison.OrdinalIgnoreCase);

    private async Task NavigateToProviderAsync(IPage page, ProviderRecord provider)
    {
        try
        {
            await page.GotoAsync(provider.StartUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Commit,
                Timeout = 30000
            });
        }
        catch (PlaywrightException exception) when (exception.Message.Contains("ERR_ABORTED", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(exception, "Provider navigation was interrupted by a redirect for {Provider}", provider.Name);
        }
    }

    private static string BuildOutputPath(ProjectRecord project, ProviderRecord provider, PromptJobRecord job, string extension)
    {
        var pattern = project.FileNamingPattern;
        var fileName = pattern
            .Replace("{projectCode}", AppDataStore.Slugify(project.Code), StringComparison.OrdinalIgnoreCase)
            .Replace("{provider}", AppDataStore.Slugify(provider.Name), StringComparison.OrdinalIgnoreCase)
            .Replace("{yyyyMMdd}", DateTime.Now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{jobNo}", job.JobNo.ToString("000"), StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", extension.TrimStart('.'), StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            fileName += extension;
        }

        return Path.Combine(project.OutputFolder, fileName);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _contexts.Values)
        {
            await context.DisposeAsync();
        }

        _playwright?.Dispose();
        _initializationGate.Dispose();
    }
}

public sealed record AutomationResult(string Status, string? OutputFilePath, string? Message)
{
    public static AutomationResult Completed(string outputPath) => new(JobStates.Completed, outputPath, null);
    public static AutomationResult Manual(string message) => new(JobStates.WaitingForUser, null, message);
    public static AutomationResult Failed(string message) => new(JobStates.Failed, null, message);
}
