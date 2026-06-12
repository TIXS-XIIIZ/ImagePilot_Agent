using System.Text.Json;
using System.Text.RegularExpressions;
using ImagePilot.Api.Models;

namespace ImagePilot.Api.Services;

public sealed partial class PromptTemplateService
{
    public IReadOnlyList<string> Generate(string basePrompt, string variablesJson, int requestedCount)
    {
        var count = Math.Clamp(requestedCount, 1, 200);
        var variables = ParseVariables(variablesJson);
        if (variables.Count == 0)
        {
            return Enumerable.Range(1, count)
                .Select(index => count == 1 ? basePrompt.Trim() : $"{basePrompt.Trim()}\nVariation: {index}")
                .ToArray();
        }

        var results = new List<string>();
        Expand(basePrompt, variables, 0, results, count);
        while (results.Count < count)
        {
            results.Add($"{results[results.Count % Math.Max(results.Count, 1)]}\nVariation: {results.Count + 1}");
        }

        return results.Take(count).ToArray();
    }

    private static List<KeyValuePair<string, string[]>> ParseVariables(string variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(variablesJson);
            return document.RootElement.EnumerateObject()
                .Select(property => new KeyValuePair<string, string[]>(
                    property.Name,
                    property.Value.EnumerateArray().Select(value => value.GetString() ?? "").Where(value => value.Length > 0).ToArray()))
                .Where(pair => pair.Value.Length > 0)
                .ToList();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Variables JSON is invalid.", nameof(variablesJson), exception);
        }
    }

    private static void Expand(
        string template,
        IReadOnlyList<KeyValuePair<string, string[]>> variables,
        int index,
        ICollection<string> results,
        int limit)
    {
        if (results.Count >= limit)
        {
            return;
        }

        if (index >= variables.Count)
        {
            results.Add(UnresolvedVariableRegex().Replace(template, "").Trim());
            return;
        }

        var variable = variables[index];
        foreach (var value in variable.Value)
        {
            Expand(template.Replace($"{{{{{variable.Key}}}}}", value, StringComparison.OrdinalIgnoreCase), variables, index + 1, results, limit);
        }
    }

    [GeneratedRegex(@"\{\{[^}]+\}\}")]
    private static partial Regex UnresolvedVariableRegex();
}
