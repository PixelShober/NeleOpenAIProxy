using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using NeleDesktop.Models;
using NeleDesktop.Services;

namespace NeleDesktop.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly NeleApiClient _apiClient;
    private string _apiKey = string.Empty;
    private string _baseUrl = string.Empty;
    private string _selectedModel = string.Empty;
    private string _hotkey = string.Empty;
    private string _temporaryHotkey = string.Empty;
    private bool _isDarkMode;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(NeleApiClient apiClient, AppSettings settings)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));

        _apiKey = settings.ApiKey;
        _baseUrl = settings.BaseUrl;
        _selectedModel = settings.SelectedModel;
        _hotkey = settings.Hotkey;
        _temporaryHotkey = settings.TemporaryHotkey;
        _isDarkMode = settings.DarkMode;
        _statusMessage = string.IsNullOrWhiteSpace(_apiKey) ? "Enter an API key to load models." : string.Empty;
    }

    public ObservableCollection<string> Models { get; } = new();

    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    public string BaseUrl
    {
        get => _baseUrl;
        set => SetProperty(ref _baseUrl, value);
    }

    public string SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    public string TemporaryHotkey
    {
        get => _temporaryHotkey;
        set => SetProperty(ref _temporaryHotkey, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set => SetProperty(ref _isDarkMode, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public async Task LoadModelsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "API key missing.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Validating models...";
        try
        {
            var models = await _apiClient.GetVerifiedModelsAsync(ApiKey, BaseUrl, cancellationToken);
            Models.Clear();
            foreach (var model in models)
            {
                Models.Add(model);
            }

            if (Models.Count > 0 && (string.IsNullOrWhiteSpace(SelectedModel) || !Models.Contains(SelectedModel)))
            {
                SelectedModel = Models[0];
            }

            StatusMessage = $"Loaded {Models.Count} models.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public AppSettings ToSettings()
    {
        var baseUrl = BaseUrl?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "https://api.aieva.io/api:v1/";
        }

        return new AppSettings
        {
            ApiKey = ApiKey?.Trim() ?? string.Empty,
            BaseUrl = baseUrl,
            SelectedModel = SelectedModel?.Trim() ?? string.Empty,
            Hotkey = Hotkey?.Trim() ?? string.Empty,
            TemporaryHotkey = TemporaryHotkey?.Trim() ?? string.Empty,
            DarkMode = IsDarkMode
        };
    }
}
