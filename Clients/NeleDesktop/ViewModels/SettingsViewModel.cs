using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using NeleDesktop.Models;
using NeleDesktop.Services;

namespace NeleDesktop.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly NeleApiClient _apiClient;
    private string _apiKey = string.Empty;
    private string _baseUrl = string.Empty;
    private string _selectedModel = string.Empty;
    private string _temporaryModel = string.Empty;
    private string _hotkey = string.Empty;
    private string _temporaryHotkey = string.Empty;
    private bool _isDarkMode;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _apiKeyErrorMessage = string.Empty;
    private bool _isModelLoading;
    private string _lastLoadedApiKey = string.Empty;
    private bool _isHotkeyCaptureActive;
    private bool _isTemporaryHotkeyCaptureActive;
    private readonly DispatcherTimer _modelLoadingTimer;
    private int _modelLoadingDotIndex;
    private string _modelLoadingDots = string.Empty;

    public SettingsViewModel(NeleApiClient apiClient, AppSettings settings)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));

        _apiKey = settings.ApiKey;
        _baseUrl = settings.BaseUrl;
        _selectedModel = settings.SelectedModel;
        _temporaryModel = string.IsNullOrWhiteSpace(settings.TemporaryChatModel)
            ? settings.SelectedModel
            : settings.TemporaryChatModel;
        _hotkey = settings.Hotkey;
        _temporaryHotkey = settings.TemporaryHotkey;
        _isDarkMode = settings.DarkMode;
        _statusMessage = string.IsNullOrWhiteSpace(_apiKey) ? "Enter an API key to load models." : string.Empty;
        Models.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ModelPlaceholder));
            OnPropertyChanged(nameof(IsModelPlaceholderVisible));
            OnPropertyChanged(nameof(IsModelSelectionEnabled));
        };

        _modelLoadingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _modelLoadingTimer.Tick += (_, _) => AdvanceModelLoadingDots();
    }

    public ObservableCollection<string> Models { get; } = new();

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (SetProperty(ref _apiKey, value))
            {
                ApiKeyErrorMessage = string.Empty;
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    _lastLoadedApiKey = string.Empty;
                    Models.Clear();
                    SelectedModel = string.Empty;
                    TemporaryModel = string.Empty;
                }

                OnPropertyChanged(nameof(ModelPlaceholder));
                OnPropertyChanged(nameof(IsModelPlaceholderVisible));
                OnPropertyChanged(nameof(IsModelSelectionEnabled));
            }
        }
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

    public string TemporaryModel
    {
        get => _temporaryModel;
        set => SetProperty(ref _temporaryModel, value);
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

    public string ApiKeyErrorMessage
    {
        get => _apiKeyErrorMessage;
        set
        {
            if (SetProperty(ref _apiKeyErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasApiKeyError));
                OnPropertyChanged(nameof(IsModelSelectionEnabled));
            }
        }
    }

    public bool HasApiKeyError => !string.IsNullOrWhiteSpace(ApiKeyErrorMessage);

    public bool IsHotkeyCaptureActive
    {
        get => _isHotkeyCaptureActive;
        set => SetProperty(ref _isHotkeyCaptureActive, value);
    }

    public bool IsTemporaryHotkeyCaptureActive
    {
        get => _isTemporaryHotkeyCaptureActive;
        set => SetProperty(ref _isTemporaryHotkeyCaptureActive, value);
    }

    public bool IsModelLoading
    {
        get => _isModelLoading;
        private set
        {
            if (SetProperty(ref _isModelLoading, value))
            {
                if (value)
                {
                    _modelLoadingDotIndex = 0;
                    _modelLoadingDots = string.Empty;
                    AdvanceModelLoadingDots();
                    _modelLoadingTimer.Start();
                }
                else
                {
                    _modelLoadingTimer.Stop();
                    _modelLoadingDots = string.Empty;
                }

                OnPropertyChanged(nameof(ModelPlaceholder));
                OnPropertyChanged(nameof(IsModelPlaceholderVisible));
                OnPropertyChanged(nameof(IsModelSelectionEnabled));
            }
        }
    }

    public bool IsModelSelectionEnabled => Models.Count > 0
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !HasApiKeyError;

    public string ModelPlaceholder
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                return "API Key erforderlich";
            }

            if (IsModelLoading && Models.Count == 0)
            {
                return $"Model Liste wird geladen{_modelLoadingDots}";
            }

            return string.Empty;
        }
    }

    public bool IsModelPlaceholderVisible => !string.IsNullOrWhiteSpace(ModelPlaceholder);

    public async Task LoadModelsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = "API key missing.";
            return;
        }

        if (IsModelLoading)
        {
            return;
        }

        if (string.Equals(ApiKey, _lastLoadedApiKey, StringComparison.Ordinal) && !HasApiKeyError)
        {
            return;
        }

        IsBusy = true;
        IsModelLoading = true;
        ApiKeyErrorMessage = string.Empty;
        StatusMessage = "Loading models...";
        try
        {
            var models = await _apiClient.GetModelsAsync(ApiKey, BaseUrl, cancellationToken);
            Models.Clear();
            foreach (var model in models)
            {
                Models.Add(model);
            }

            if (Models.Count > 0 && (string.IsNullOrWhiteSpace(SelectedModel) || !Models.Contains(SelectedModel)))
            {
                SelectedModel = Models[0];
            }

            if (Models.Count > 0 && (string.IsNullOrWhiteSpace(TemporaryModel) || !Models.Contains(TemporaryModel)))
            {
                TemporaryModel = Models[0];
            }

            _lastLoadedApiKey = ApiKey;

            StatusMessage = "Validating models...";
            foreach (var model in models)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isUsable = await _apiClient.IsModelUsableAsync(ApiKey, BaseUrl, model, cancellationToken);
                if (!isUsable)
                {
                    Models.Remove(model);

                    if (string.Equals(SelectedModel, model, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedModel = Models.Count > 0 ? Models[0] : string.Empty;
                    }

                    if (string.Equals(TemporaryModel, model, StringComparison.OrdinalIgnoreCase))
                    {
                        TemporaryModel = Models.Count > 0 ? Models[0] : string.Empty;
                    }
                }
            }

            StatusMessage = $"Loaded {Models.Count} models.";
        }
        catch (UnauthorizedAccessException)
        {
            Models.Clear();
            ApiKeyErrorMessage = "API Key ungueltig oder nicht autorisiert.";
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            IsModelLoading = false;
        }
    }

    public void InitializeModels(IEnumerable<string> models)
    {
        if (models is null)
        {
            return;
        }

        Models.Clear();

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            SelectedModel = string.Empty;
            TemporaryModel = string.Empty;
            OnPropertyChanged(nameof(ModelPlaceholder));
            OnPropertyChanged(nameof(IsModelPlaceholderVisible));
            OnPropertyChanged(nameof(IsModelSelectionEnabled));
            return;
        }

        foreach (var model in models)
        {
            Models.Add(model);
        }

        if (Models.Count > 0 && (string.IsNullOrWhiteSpace(SelectedModel) || !Models.Contains(SelectedModel)))
        {
            SelectedModel = Models[0];
        }

        if (Models.Count > 0 && (string.IsNullOrWhiteSpace(TemporaryModel) || !Models.Contains(TemporaryModel)))
        {
            TemporaryModel = Models[0];
        }

        if (Models.Count > 0 && !string.IsNullOrWhiteSpace(ApiKey))
        {
            _lastLoadedApiKey = ApiKey;
        }

        OnPropertyChanged(nameof(ModelPlaceholder));
        OnPropertyChanged(nameof(IsModelPlaceholderVisible));
        OnPropertyChanged(nameof(IsModelSelectionEnabled));
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
            TemporaryChatModel = TemporaryModel?.Trim() ?? string.Empty,
            Hotkey = Hotkey?.Trim() ?? string.Empty,
            TemporaryHotkey = TemporaryHotkey?.Trim() ?? string.Empty,
            DarkMode = IsDarkMode
        };
    }

    private void AdvanceModelLoadingDots()
    {
        _modelLoadingDotIndex = (_modelLoadingDotIndex % 3) + 1;
        _modelLoadingDots = new string('.', _modelLoadingDotIndex);
        OnPropertyChanged(nameof(ModelPlaceholder));
        OnPropertyChanged(nameof(IsModelPlaceholderVisible));
    }
}
