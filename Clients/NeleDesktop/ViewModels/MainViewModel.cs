using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using NeleDesktop.Models;
using NeleDesktop.Services;

namespace NeleDesktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppDataStore _dataStore = new();
    private readonly NeleApiClient _apiClient = new();
    private readonly ThemeService _themeService = new();
    private readonly ObservableCollection<ChatMessage> _emptyMessages = new();

    private AppSettings _settings = new();
    private AppState _state = new();
    private ChatConversationViewModel? _selectedChat;
    private string _inputText = string.Empty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public MainViewModel()
    {
        NewChatCommand = new RelayCommand(CreateNewChat);
        NewFolderCommand = new RelayCommand(RequestNewFolder);
        SendMessageCommand = new AsyncRelayCommand(SendMessageAsync, CanSendMessage);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
    }

    public event EventHandler? HotkeyChanged;
    public event EventHandler? NewFolderRequested;
    public event EventHandler? ApiKeyMissing;

    public ObservableCollection<ChatFolderViewModel> Folders { get; } = new();

    public ObservableCollection<ChatConversationViewModel> RootChats { get; } = new();

    public ICommand NewChatCommand { get; }

    public ICommand NewFolderCommand { get; }

    public ICommand SendMessageCommand { get; }

    public ICommand ToggleThemeCommand { get; }

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
                RaiseSendCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

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

        BuildViewModels();

        if (SelectedChat is null)
        {
            CreateNewChat();
        }

        StatusMessage = "Ready.";
    }

    public SettingsViewModel CreateSettingsViewModel()
    {
        return new SettingsViewModel(_apiClient, _settings);
    }

    public async Task ApplySettingsAsync(SettingsViewModel settingsViewModel)
    {
        _settings = settingsViewModel.ToSettings();
        _themeService.ApplyTheme(_settings.DarkMode);
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(IsDarkMode));
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
        await _dataStore.SaveSettingsAsync(_settings);
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
    }

    public void DeleteChat(ChatConversationViewModel chat)
    {
        if (chat is null)
        {
            return;
        }

        RemoveChatFromCollections(chat);
        _state.Conversations.Remove(chat.Model);

        if (SelectedChat == chat)
        {
            SelectedChat = RootChats.FirstOrDefault()
                ?? Folders.SelectMany(folder => folder.Chats).FirstOrDefault();
        }

        _ = _dataStore.SaveStateAsync(_state);
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
        }

        SelectedChat = FindChatById(_state.ActiveChatId)
            ?? RootChats.FirstOrDefault()
            ?? Folders.SelectMany(folder => folder.Chats).FirstOrDefault();
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
        var chat = new ChatConversation
        {
            Title = GetNextChatTitle()
        };
        _state.Conversations.Add(chat);

        var viewModel = new ChatConversationViewModel(chat);
        RootChats.Insert(0, viewModel);
        SelectedChat = viewModel;
        _ = _dataStore.SaveStateAsync(_state);
    }

    public void AddFolder(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var folder = new ChatFolder { Name = name.Trim() };
        _state.Folders.Add(folder);
        Folders.Add(new ChatFolderViewModel(folder));
        _ = _dataStore.SaveStateAsync(_state);
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
            StatusMessage = "API key missing. Open settings to configure.";
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
            var model = string.IsNullOrWhiteSpace(_settings.SelectedModel)
                ? "google-claude-4.5-sonnet"
                : _settings.SelectedModel;

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
