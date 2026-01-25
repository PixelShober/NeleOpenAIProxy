using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Input;
using NeleDesktop.Models;
using NeleDesktop.Services;

namespace NeleDesktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const double ExpandedSidebarWidth = 280;
    private const double ExpandedMinWidth = 500;
    private const double CompactMinWidth = 380;

    private readonly AppDataStore _dataStore = new();
    private readonly NeleApiClient _apiClient = new();
    private readonly ThemeService _themeService = new();
    private readonly ObservableCollection<ChatMessage> _emptyMessages = new();

    private AppSettings _settings = new();
    private AppState _state = new();
    private ChatConversationViewModel? _selectedChat;
    private string _inputText = string.Empty;
    private bool _isBusy;
    private string _busyIndicatorText = "...";
    private string _statusMessage = string.Empty;
    private bool _isSidebarVisible = true;
    private double _sidebarWidth = ExpandedSidebarWidth;
    private ChatConversationViewModel? _temporaryChat;
    private bool _wasSidebarVisibleBeforeTemp = true;
    private readonly DispatcherTimer _busyIndicatorTimer;
    private int _busyIndicatorDotIndex;

    public MainViewModel()
    {
        NewChatCommand = new RelayCommand(CreateNewChat);
        NewFolderCommand = new RelayCommand(RequestNewFolder);
        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, CanSendMessage);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);

        _busyIndicatorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _busyIndicatorTimer.Tick += (_, _) => AdvanceBusyIndicator();
    }

    public event EventHandler? HotkeyChanged;
    public event EventHandler? NewFolderRequested;
    public event EventHandler? ApiKeyMissing;

    public ObservableCollection<ChatFolderViewModel> Folders { get; } = new();

    public ObservableCollection<ChatConversationViewModel> RootChats { get; } = new();

    public ObservableCollection<object> ConversationItems { get; } = new();

    public ObservableCollection<string> AvailableModels { get; } = new();

    public ICommand NewChatCommand { get; }

    public ICommand NewFolderCommand { get; }

    public ICommand SendMessageCommand { get; }

    public ICommand ToggleThemeCommand { get; }

    public ICommand ToggleSidebarCommand { get; }

    public AppSettings Settings => _settings;

    public ChatConversationViewModel? SelectedChat
    {
        get => _selectedChat;
        set
        {
            if (SetProperty(ref _selectedChat, value))
            {
                if (_selectedChat is not null)
                {
                    _state.ActiveChatId = _selectedChat.Id;
                    EnsureChatModel(_selectedChat);
                }

                OnPropertyChanged(nameof(ActiveMessages));
            }
        }
    }

    public ObservableCollection<ChatMessage> ActiveMessages => SelectedChat?.Model.Messages ?? _emptyMessages;

    public string InputText
    {
        get => _inputText;
        set
        {
            if (SetProperty(ref _inputText, value))
            {
                RaiseSendCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                if (value)
                {
                    _busyIndicatorDotIndex = 0;
                    AdvanceBusyIndicator();
                    _busyIndicatorTimer.Start();
                }
                else
                {
                    _busyIndicatorTimer.Stop();
                    BusyIndicatorText = "...";
                }

                RaiseSendCanExecuteChanged();
            }
        }
    }

    public string BusyIndicatorText
    {
        get => _busyIndicatorText;
        private set => SetProperty(ref _busyIndicatorText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsSidebarVisible
    {
        get => _isSidebarVisible;
        set
        {
            if (SetProperty(ref _isSidebarVisible, value))
            {
                SidebarWidth = _isSidebarVisible ? ExpandedSidebarWidth : 0;
                OnPropertyChanged(nameof(WindowMinWidth));
            }
        }
    }

    public double SidebarWidth
    {
        get => _sidebarWidth;
        private set => SetProperty(ref _sidebarWidth, value);
    }

    public double WindowMinWidth => IsSidebarVisible ? ExpandedMinWidth : CompactMinWidth;

    public bool IsApiKeyMissing => string.IsNullOrWhiteSpace(_settings.ApiKey);

    public bool IsApiKeyAvailable => !IsApiKeyMissing;

    public bool IsDarkMode
    {
        get => _settings.DarkMode;
        set
        {
            if (_settings.DarkMode != value)
            {
                _settings.DarkMode = value;
                OnPropertyChanged();
                _themeService.ApplyTheme(_settings.DarkMode);
                _ = _dataStore.SaveSettingsAsync(_settings);
            }
        }
    }

    public async Task InitializeAsync()
    {
        _settings = await _dataStore.LoadSettingsAsync();
        _state = await _dataStore.LoadStateAsync();

        _themeService.ApplyTheme(_settings.DarkMode);

        SeedAvailableModels();
        BuildViewModels();
        OnPropertyChanged(nameof(IsApiKeyMissing));
        OnPropertyChanged(nameof(IsApiKeyAvailable));

        if (SelectedChat is null)
        {
            CreateNewChat();
        }

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            await LoadAvailableModelsAsync();
        }

        StatusMessage = "Ready.";
    }

    public SettingsViewModel CreateSettingsViewModel()
    {
        var viewModel = new SettingsViewModel(_apiClient, _settings);
        viewModel.InitializeModels(AvailableModels);
        return viewModel;
    }

    public async Task ApplySettingsAsync(SettingsViewModel settingsViewModel)
    {
        _settings = settingsViewModel.ToSettings();
        _themeService.ApplyTheme(_settings.DarkMode);
        if (settingsViewModel.Models.Count > 0)
        {
            UpdateAvailableModels(settingsViewModel.Models);
        }
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(IsDarkMode));
        OnPropertyChanged(nameof(IsApiKeyMissing));
        OnPropertyChanged(nameof(IsApiKeyAvailable));
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
        await _dataStore.SaveSettingsAsync(_settings);

        if (AvailableModels.Count == 0 && !string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            await LoadAvailableModelsAsync();
        }
    }

    public void MoveChatToFolder(ChatConversationViewModel chat, ChatFolderViewModel? folder)
    {
        if (chat is null)
        {
            return;
        }

        if (folder is null)
        {
            if (!string.IsNullOrWhiteSpace(chat.FolderId))
            {
                var currentFolder = Folders.FirstOrDefault(f => f.Id == chat.FolderId);
                currentFolder?.Chats.Remove(chat);
                chat.FolderId = null;
                RootChats.Insert(0, chat);
            }
        }
        else
        {
            RemoveChatFromCollections(chat);
            chat.FolderId = folder.Id;
            folder.Chats.Insert(0, chat);
        }

        chat.Model.UpdatedAt = DateTimeOffset.UtcNow;
        _ = _dataStore.SaveStateAsync(_state);
        RebuildConversationItems();
    }

    public void DeleteChat(ChatConversationViewModel chat)
    {
        if (chat is null)
        {
            return;
        }

        if (_temporaryChat == chat)
        {
            _temporaryChat = null;
        }

        chat.PropertyChanged -= Chat_PropertyChanged;
        RemoveChatFromCollections(chat);
        _state.Conversations.Remove(chat.Model);

        if (SelectedChat == chat)
        {
            SelectedChat = RootChats.FirstOrDefault()
                ?? Folders.SelectMany(folder => folder.Chats).FirstOrDefault();
        }

        _ = _dataStore.SaveStateAsync(_state);
        RebuildConversationItems();
    }

    public void DeleteFolder(ChatFolderViewModel folder)
    {
        if (folder is null)
        {
            return;
        }

        foreach (var chat in folder.Chats.ToList())
        {
            chat.FolderId = null;
            RootChats.Insert(0, chat);
        }

        folder.Chats.Clear();
        Folders.Remove(folder);
        _state.Folders.Remove(folder.Model);
        _ = _dataStore.SaveStateAsync(_state);
        RebuildConversationItems();
    }

    public void RenameChat(ChatConversationViewModel chat, string title)
    {
        if (chat is null || string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        chat.Title = title.Trim();
        chat.Model.UpdatedAt = DateTimeOffset.UtcNow;
        _ = _dataStore.SaveStateAsync(_state);
        RebuildConversationItems();
    }

    public void RenameFolder(ChatFolderViewModel folder, string name)
    {
        if (folder is null || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        folder.Name = name.Trim();
        _ = _dataStore.SaveStateAsync(_state);
        RebuildConversationItems();
    }

    private void BuildViewModels()
    {
        Folders.Clear();
        RootChats.Clear();

        var folderMap = _state.Folders
            .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
            .Select(folder => new ChatFolderViewModel(folder))
            .ToDictionary(folder => folder.Id, folder => folder, StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folderMap.Values)
        {
            Folders.Add(folder);
        }

        var chats = _state.Conversations
            .OrderByDescending(chat => chat.UpdatedAt)
            .Select(chat => new ChatConversationViewModel(chat));

        foreach (var chatViewModel in chats)
        {
            if (!string.IsNullOrWhiteSpace(chatViewModel.FolderId)
                && folderMap.TryGetValue(chatViewModel.FolderId, out var folder))
            {
                folder.Chats.Add(chatViewModel);
            }
            else
            {
                RootChats.Add(chatViewModel);
            }

            chatViewModel.PropertyChanged += Chat_PropertyChanged;
        }

        SelectedChat = FindChatById(_state.ActiveChatId)
            ?? RootChats.FirstOrDefault()
            ?? Folders.SelectMany(folder => folder.Chats).FirstOrDefault();

        EnsureChatModels();
        RebuildConversationItems();
    }

    private ChatConversationViewModel? FindChatById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return RootChats.FirstOrDefault(chat => chat.Id == id)
            ?? Folders.SelectMany(folder => folder.Chats).FirstOrDefault(chat => chat.Id == id);
    }

    private void CreateNewChat()
    {
        IsSidebarVisible = true;
        var chat = new ChatConversation
        {
            Title = GetNextChatTitle(),
            Model = ResolveDefaultModel()
        };
        _state.Conversations.Add(chat);

        var viewModel = new ChatConversationViewModel(chat);
        viewModel.PropertyChanged += Chat_PropertyChanged;
        RootChats.Insert(0, viewModel);
        SelectedChat = viewModel;
        _ = _dataStore.SaveStateAsync(_state);
        RebuildConversationItems();
    }

    public void OpenTemporaryChat()
    {
        if (_temporaryChat is not null)
        {
            SelectedChat = _temporaryChat;
            IsSidebarVisible = false;
            return;
        }

        _wasSidebarVisibleBeforeTemp = IsSidebarVisible;
        IsSidebarVisible = false;
        var chat = new ChatConversation
        {
            Title = "Temporary chat",
            Model = ResolveTemporaryModel(),
            IsTemporary = true
        };

        _state.Conversations.Add(chat);
        var viewModel = new ChatConversationViewModel(chat);
        viewModel.PropertyChanged += Chat_PropertyChanged;
        RootChats.Insert(0, viewModel);
        _temporaryChat = viewModel;
        SelectedChat = viewModel;
        _ = _dataStore.SaveStateAsync(_state);
        RebuildConversationItems();
    }

    public void ClearTemporaryChat()
    {
        if (_temporaryChat is null)
        {
            return;
        }

        var restoreSidebar = _wasSidebarVisibleBeforeTemp;
        DeleteChat(_temporaryChat);
        IsSidebarVisible = restoreSidebar;
    }

    public void AddFolder(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        IsSidebarVisible = true;
        var folder = new ChatFolder { Name = name.Trim() };
        _state.Folders.Add(folder);
        Folders.Add(new ChatFolderViewModel(folder));
        _ = _dataStore.SaveStateAsync(_state);
        RebuildConversationItems();
    }

    private void RequestNewFolder()
    {
        NewFolderRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task SendMessageAsync()
    {
        if (SelectedChat is null)
        {
            CreateNewChat();
        }

        if (SelectedChat is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            ApiKeyMissing?.Invoke(this, EventArgs.Empty);
            return;
        }

        var text = InputText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        InputText = string.Empty;
        IsBusy = true;

        var chat = SelectedChat;
        var userMessage = new ChatMessage
        {
            Role = "user",
            Content = text,
            Timestamp = DateTimeOffset.UtcNow
        };

        chat.Model.Messages.Add(userMessage);
        UpdateChatTitle(chat, text);
        chat.Model.UpdatedAt = DateTimeOffset.UtcNow;
        await _dataStore.SaveStateAsync(_state);

        try
        {
            var model = string.IsNullOrWhiteSpace(chat.SelectedModel)
                ? ResolveDefaultModel()
                : chat.SelectedModel;

            var reply = await _apiClient.SendChatAsync(
                _settings.ApiKey,
                _settings.BaseUrl,
                model,
                chat.Model.Messages,
                CancellationToken.None);

            chat.Model.Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = reply,
                Timestamp = DateTimeOffset.UtcNow
            });
            chat.Model.UpdatedAt = DateTimeOffset.UtcNow;
            StatusMessage = "Ready.";
            await _dataStore.SaveStateAsync(_state);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            chat.Model.Messages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = $"Error: {ex.Message}",
                Timestamp = DateTimeOffset.UtcNow
            });
            await _dataStore.SaveStateAsync(_state);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateChatTitle(ChatConversationViewModel chat, string text)
    {
        if (!chat.Title.StartsWith("New chat", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var title = text.Length > 40 ? text[..40] + "..." : text;
        chat.Title = title;
    }

    private string GetNextChatTitle()
    {
        const string baseTitle = "New chat";
        var titles = _state.Conversations.Select(chat => chat.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!titles.Contains(baseTitle))
        {
            return baseTitle;
        }

        var index = 2;
        while (titles.Contains($"{baseTitle} {index}"))
        {
            index++;
        }

        return $"{baseTitle} {index}";
    }

    private bool CanSendMessage()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(InputText);
    }

    private void RaiseSendCanExecuteChanged()
    {
        if (SendMessageCommand is AsyncRelayCommand asyncCommand)
        {
            asyncCommand.RaiseCanExecuteChanged();
        }
    }

    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    public void UpdateWindowPlacement(double left, double top, double width, double height)
    {
        _settings.WindowLeft = left;
        _settings.WindowTop = top;
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        _ = _dataStore.SaveSettingsAsync(_settings);
    }

    private async Task LoadAvailableModelsAsync()
    {
        try
        {
            var models = await _apiClient.GetVerifiedModelsAsync(_settings.ApiKey, _settings.BaseUrl, CancellationToken.None);
            UpdateAvailableModels(models);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private void UpdateAvailableModels(IEnumerable<string> models)
    {
        AvailableModels.Clear();
        foreach (var model in models)
        {
            AvailableModels.Add(model);
        }

        EnsureChatModels();
    }

    private void EnsureChatModels()
    {
        foreach (var chat in RootChats)
        {
            EnsureChatModel(chat);
        }

        foreach (var chat in Folders.SelectMany(folder => folder.Chats))
        {
            EnsureChatModel(chat);
        }
    }

    private void EnsureChatModel(ChatConversationViewModel chat)
    {
        if (chat is null)
        {
            return;
        }

        var defaultModel = ResolveDefaultModel();
        if (string.IsNullOrWhiteSpace(chat.SelectedModel))
        {
            chat.SelectedModel = defaultModel;
            return;
        }

        if (AvailableModels.Count > 0 && !AvailableModels.Contains(chat.SelectedModel))
        {
            chat.SelectedModel = defaultModel;
        }
    }

    private string ResolveDefaultModel()
    {
        if (!string.IsNullOrWhiteSpace(_settings.SelectedModel))
        {
            if (AvailableModels.Count == 0 || AvailableModels.Contains(_settings.SelectedModel))
            {
                return _settings.SelectedModel;
            }
        }

        return AvailableModels.FirstOrDefault() ?? GetFallbackModel();
    }

    private string ResolveTemporaryModel()
    {
        if (!string.IsNullOrWhiteSpace(_settings.TemporaryChatModel))
        {
            if (AvailableModels.Count == 0 || AvailableModels.Contains(_settings.TemporaryChatModel))
            {
                return _settings.TemporaryChatModel;
            }
        }

        return ResolveDefaultModel();
    }

    private string GetFallbackModel()
    {
        return string.IsNullOrWhiteSpace(_settings.SelectedModel)
            ? "google-claude-4.5-sonnet"
            : _settings.SelectedModel;
    }

    private void SeedAvailableModels()
    {
        if (AvailableModels.Count > 0)
        {
            return;
        }

        var fallback = GetFallbackModel();
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            AvailableModels.Add(fallback);
        }
    }

    private void RebuildConversationItems()
    {
        ConversationItems.Clear();

        foreach (var folder in Folders)
        {
            ConversationItems.Add(folder);
        }

        foreach (var chat in RootChats)
        {
            ConversationItems.Add(chat);
        }
    }

    private void Chat_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ChatConversationViewModel chat)
        {
            return;
        }

        if (e.PropertyName == nameof(ChatConversationViewModel.SelectedModel)
            || e.PropertyName == nameof(ChatConversationViewModel.Title))
        {
            chat.Model.UpdatedAt = DateTimeOffset.UtcNow;
            _ = _dataStore.SaveStateAsync(_state);
        }
    }

    private void AdvanceBusyIndicator()
    {
        _busyIndicatorDotIndex = (_busyIndicatorDotIndex % 3) + 1;
        BusyIndicatorText = new string('.', _busyIndicatorDotIndex);
    }

    private void RemoveChatFromCollections(ChatConversationViewModel chat)
    {
        if (RootChats.Contains(chat))
        {
            RootChats.Remove(chat);
            return;
        }

        foreach (var folder in Folders)
        {
            if (folder.Chats.Contains(chat))
            {
                folder.Chats.Remove(chat);
                return;
            }
        }
    }
}
