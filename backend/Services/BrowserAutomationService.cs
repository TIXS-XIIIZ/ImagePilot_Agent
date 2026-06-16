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
            if (provider.SubmitButtonSelector.Equals("Enter", StringComparison.OrdinalIgnoreCase))
            {
                await page.Locator(provider.PromptInputSelector).PressAsync("Enter");
            }
            else
            {
                await page.Locator(provider.SubmitButtonSelector).ClickAsync();
            }

            if (string.IsNullOrWhiteSpace(provider.ResultContainerSelector))
            {
                return AutomationResult.Manual("Prompt submitted. Result selector is empty, so confirm and download the output manually.");
            }

            await page.Locator(provider.ResultContainerSelector).Last.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = provider.DefaultTimeoutSeconds * 1000
            });

            // Bypass the flaky UI download button entirely and fetch the image directly!
            var capturedPath = await TrySaveLatestImageAsync(page, project, provider, job);
            return capturedPath is not null
                ? AutomationResult.Completed(capturedPath)
                : AutomationResult.Manual("Result detected, but the image could not be captured automatically. Download it manually, then mark the job complete.");
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
            var sourceOrDataUrl = await page.EvaluateAsync<string?>(
                """
                async () => {
                  const images = Array.from(document.images)
                    .filter((image) => image.naturalWidth >= 128 && image.naturalHeight >= 128)
                    .filter((image) => {
                      const source = image.currentSrc || image.src || "";
                      return source && !source.startsWith("chrome-extension:");
                    });
                  const image = images.at(-1);
                  if (!image) return null;
                  
                  const source = image.currentSrc || image.src;
                  if (source.startsWith("data:")) return source;
                  
                  try {
                    const response = await fetch(source);
                    const blob = await response.blob();
                    return await new Promise((resolve) => {
                      const reader = new FileReader();
                      reader.onloadend = () => resolve(String(reader.result));
                      reader.onerror = () => resolve(null);
                      reader.readAsDataURL(blob);
                    });
                  } catch (e) {
                    return null;
                  }
                }
                """);

            if (string.IsNullOrWhiteSpace(sourceOrDataUrl))
            {
                return null;
            }

            byte[] bytes;
            var extension = ".png";

            if (sourceOrDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var commaIndex = sourceOrDataUrl.IndexOf(',');
                if (commaIndex < 0) return null;
                bytes = Convert.FromBase64String(sourceOrDataUrl[(commaIndex + 1)..]);
            }
            else if (sourceOrDataUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var response = await page.Context.APIRequest.GetAsync(sourceOrDataUrl);
                if (!response.Ok) return null;
                bytes = await response.BodyAsync();
                
                if (sourceOrDataUrl.Contains(".jpg", StringComparison.OrdinalIgnoreCase) || sourceOrDataUrl.Contains(".jpeg", StringComparison.OrdinalIgnoreCase)) extension = ".jpg";
                else if (sourceOrDataUrl.Contains(".webp", StringComparison.OrdinalIgnoreCase)) extension = ".webp";
            }
            else 
            {
                return null;
            }

            var outputPath = BuildOutputPath(project, provider, job, extension);
            Directory.CreateDirectory(project.OutputFolder);
            await File.WriteAllBytesAsync(outputPath, bytes);

            return outputPath;
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
