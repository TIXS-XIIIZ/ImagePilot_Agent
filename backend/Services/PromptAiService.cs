using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ImagePilot.Api.Models;

namespace ImagePilot.Api.Services;

public sealed class PromptAiService(
    AppDataStore store,
    WindowsCredentialStore credentialStore,
    IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Save(PromptAiSettingsSaveRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            credentialStore.SavePromptAiApiKey(request.ApiKey.Trim());
        }

        store.Write(data =>
        {
            data.PromptAi = request.Settings;
            data.PromptAi.ApiKeySaved = credentialStore.ReadPromptAiApiKey() is not null;
            return data.PromptAi;
        });
    }

    public async Task<PromptAiResult> TestAsync(PromptAiSettingsSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var apiKey = ResolveApiKey(request.ApiKey);
            using var httpRequest = CreateRequest(HttpMethod.Get, request.Settings, apiKey, "models");
            using var response = await httpClientFactory.CreateClient().SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new PromptAiResult(false, $"API test failed: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            return new PromptAiResult(true, "External prompt API connection succeeded.");
        }
        catch (Exception exception)
        {
            return new PromptAiResult(false, exception.Message);
        }
    }

    public async Task<PromptAiResult> EnhanceAsync(PromptEnhanceRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var settings = store.Read(data => data.PromptAi);
            if (!settings.Enabled)
            {
                return new PromptAiResult(false, "Enable the external prompt API in Prompt AI settings first.");
            }

            if (string.IsNullOrWhiteSpace(settings.Model))
            {
                return new PromptAiResult(false, "Enter a model name in Prompt AI settings first.");
            }

            var apiKey = ResolveApiKey(null);
            return settings.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                ? await EnhanceWithGeminiAsync(settings, apiKey, request, cancellationToken)
                : await EnhanceWithOpenAiCompatibleAsync(settings, apiKey, request, cancellationToken);
        }
        catch (Exception exception)
        {
            return new PromptAiResult(false, exception.Message);
        }
    }

    private async Task<PromptAiResult> EnhanceWithOpenAiCompatibleAsync(
        PromptAiSettings settings,
        string apiKey,
        PromptEnhanceRequest request,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model = settings.Model,
            temperature = Math.Clamp(settings.Temperature, 0, 2),
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = BuildSystemInstruction()
                },
                new
                {
                    role = "user",
                    content = BuildUserPrompt(request)
                }
            }
        };
        using var httpRequest = CreateRequest(HttpMethod.Post, settings, apiKey, "chat/completions");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await httpClientFactory.CreateClient().SendAsync(httpRequest, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new PromptAiResult(false, $"Prompt API failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        using var document = JsonDocument.Parse(json);
        var prompt = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?.Trim();
        return CreatePromptResult(prompt);
    }

    private async Task<PromptAiResult> EnhanceWithGeminiAsync(
        PromptAiSettings settings,
        string apiKey,
        PromptEnhanceRequest request,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = BuildSystemInstruction() } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = BuildUserPrompt(request) } }
                }
            },
            generationConfig = new
            {
                temperature = Math.Clamp(settings.Temperature, 0, 2)
            }
        };
        using var httpRequest = CreateRequest(HttpMethod.Post, settings, apiKey, $"models/{settings.Model}:generateContent");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await httpClientFactory.CreateClient().SendAsync(httpRequest, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new PromptAiResult(false, $"Gemini API failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        using var document = JsonDocument.Parse(json);
        var parts = document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")
            .EnumerateArray()
            .Select(part => part.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text));
        return CreatePromptResult(string.Join("\n", parts).Trim());
    }

    private static PromptAiResult CreatePromptResult(string? prompt) =>
        string.IsNullOrWhiteSpace(prompt)
            ? new PromptAiResult(false, "The external API returned an empty prompt.")
            : new PromptAiResult(true, "Prompt improved by the external API.", prompt);

    private static string BuildSystemInstruction() =>
        """
        You improve prompts for AI image generation. Return only one polished prompt in English.
        Keep useful template variables such as {{subject}} unchanged. Make the subject, composition,
        background, lighting, camera, visual style, and constraints explicit. Do not add explanations.
        """;

    private static string BuildUserPrompt(PromptEnhanceRequest request) =>
        $"Category: {request.Category ?? "General"}\nExtra instructions: {request.ExtraInstructions ?? ""}\nPrompt:\n{request.Prompt}";

    private static HttpRequestMessage CreateRequest(HttpMethod method, PromptAiSettings settings, string apiKey, string relativePath)
    {
        if (!Uri.TryCreate(settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Prompt AI Base URL is invalid.");
        }

        var request = new HttpRequestMessage(method, new Uri(baseUri, relativePath));
        if (settings.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("x-goog-api-key", apiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private string ResolveApiKey(string? providedApiKey)
    {
        var apiKey = string.IsNullOrWhiteSpace(providedApiKey)
            ? credentialStore.ReadPromptAiApiKey()
            : providedApiKey.Trim();
        return !string.IsNullOrWhiteSpace(apiKey)
            ? apiKey
            : throw new InvalidOperationException("Enter and save an external prompt API key first.");
    }
}
