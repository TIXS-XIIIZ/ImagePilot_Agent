using System.Text.Json;
using ImagePilot.Api.Models;

namespace ImagePilot.Api.Services;

public sealed class AppDataStore
{
    private readonly object _gate = new();
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private AppData _data;

    public AppDataStore(AppPaths paths)
    {
        _paths = paths;
        _data = Load();
        NormalizeExistingData();
        SeedProviders();
    }

    public T Read<T>(Func<AppData, T> read)
    {
        lock (_gate)
        {
            return Clone(read(_data));
        }
    }

    public T Write<T>(Func<AppData, T> write)
    {
        lock (_gate)
        {
            var result = write(_data);
            Save();
            return Clone(result);
        }
    }

    private AppData Load()
    {
        if (!File.Exists(_paths.DataFile))
        {
            return new AppData();
        }

        try
        {
            return JsonSerializer.Deserialize<AppData>(File.ReadAllText(_paths.DataFile), _jsonOptions) ?? new AppData();
        }
        catch
        {
            var backup = $"{_paths.DataFile}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Copy(_paths.DataFile, backup, overwrite: true);
            return new AppData();
        }
    }

    private void Save()
    {
        var temporaryFile = $"{_paths.DataFile}.tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(_data, _jsonOptions));
        File.Move(temporaryFile, _paths.DataFile, overwrite: true);
    }

    private T Clone<T>(T value)
    {
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, _jsonOptions), _jsonOptions)!;
    }

    private void SeedProviders()
    {
        if (_data.Providers.Count > 0)
        {
            return;
        }

        foreach (var configFile in Directory.GetFiles(_paths.ProviderConfigsFolder, "*.json"))
        {
            var provider = JsonSerializer.Deserialize<ProviderRecord>(File.ReadAllText(configFile), _jsonOptions);
            if (provider is null)
            {
                continue;
            }

            provider.Id = _data.NextProviderId++;
            provider.CreatedAt = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(provider.BrowserProfilePath))
            {
                provider.BrowserProfilePath = Path.Combine(_paths.BrowserProfilesFolder, Slugify(provider.Name));
            }

            _data.Providers.Add(provider);
        }

        Save();
    }

    private void NormalizeExistingData()
    {
        var changed = false;

        foreach (var profile in _data.ChromeProfiles)
        {
            if (string.IsNullOrWhiteSpace(profile.BrowserChannel))
            {
                profile.BrowserChannel = "chrome";
                changed = true;
            }
        }

        foreach (var provider in _data.Providers.Where(provider =>
            provider.AutomationEnabled &&
            provider.Name.Contains("Gemini", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(provider.BrowserChannel)))
        {
            provider.BrowserChannel = "chrome";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(_data.PersistenceMode))
        {
            _data.PersistenceMode = "Json";
            changed = true;
        }

        if (changed)
        {
            Save();
        }
    }

    public static string Slugify(string value)
    {
        var clean = new string(value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray());
        return string.Join("-", clean.Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
