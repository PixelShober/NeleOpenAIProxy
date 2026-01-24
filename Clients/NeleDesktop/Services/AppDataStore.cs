using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NeleDesktop.Models;

namespace NeleDesktop.Services;

public sealed class AppDataStore
{
    private readonly string _rootPath;
    private readonly string _settingsPath;
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AppDataStore()
    {
        _rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NeleAIProxy");
        _settingsPath = Path.Combine(_rootPath, "settings.json");
        _statePath = Path.Combine(_rootPath, "conversations.json");
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
        return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        Directory.CreateDirectory(_rootPath);
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
    }

    public async Task<AppState> LoadStateAsync()
    {
        if (!File.Exists(_statePath))
        {
            return new AppState();
        }

        var json = await File.ReadAllTextAsync(_statePath).ConfigureAwait(false);
        return JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? new AppState();
    }

    public async Task SaveStateAsync(AppState state)
    {
        Directory.CreateDirectory(_rootPath);
        var json = JsonSerializer.Serialize(state, _jsonOptions);
        await File.WriteAllTextAsync(_statePath, json).ConfigureAwait(false);
    }
}
