using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NeleDesktop.Models;
using NeleDesktop.Services;
using NeleDesktop.ViewModels;
using NeleDesktop.Views;

namespace NeleDesktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private HotkeyService? _hotkeyService;
    private Point _dragStart;
    private ObservableCollection<ChatMessage>? _activeMessages;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.HotkeyChanged += (_, _) => ApplyHotkey();
        _viewModel.NewFolderRequested += (_, _) => PromptNewFolder();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.ActiveMessages))
            {
                AttachMessageCollection();
            }
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        AttachMessageCollection();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hotkeyService = new HotkeyService(this);
        _hotkeyService.Initialize();
        _hotkeyService.HotkeyPressed += (_, _) => ToggleVisibility();
        ApplyHotkey();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!App.IsShuttingDown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _hotkeyService?.Dispose();
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
        Activate();
        Focus();
    }

    private void ApplyHotkey()
    {
        if (_hotkeyService is null)
        {
            return;
        }

        _hotkeyService.Register(_viewModel.Settings.Hotkey);
    }

    private void ConversationTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ChatConversationViewModel chat)
        {
            _viewModel.SelectedChat = chat;
        }
    }

    private void ConversationTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
    }

    private void ConversationTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (ConversationTree.SelectedItem is ChatConversationViewModel chat)
        {
            DragDrop.DoDragDrop(ConversationTree, chat, DragDropEffects.Move);
        }
    }

    private void ConversationTree_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ChatConversationViewModel)))
        {
            return;
        }

        if (GetAncestor<TreeViewItem>((DependencyObject)e.OriginalSource) is not { DataContext: ChatFolderViewModel folder })
        {
            return;
        }

        var chat = (ChatConversationViewModel)e.Data.GetData(typeof(ChatConversationViewModel))!;
        _viewModel.MoveChatToFolder(chat, folder);
    }

    private void DeleteChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ChatConversationViewModel chat)
        {
            _viewModel.DeleteChat(chat);
        }
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ChatFolderViewModel folder)
        {
            _viewModel.DeleteFolder(folder);
        }
    }

    private void MoveToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.DataContext is not ChatConversationViewModel chat)
        {
            return;
        }

        var dialog = new MoveToFolderDialog(_viewModel.Folders)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            var targetFolder = dialog.SelectedFolderId is null
                ? null
                : FindFolder(dialog.SelectedFolderId);

            _viewModel.MoveChatToFolder(chat, targetFolder);
        }
    }

    private void RemoveFromFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ChatConversationViewModel chat)
        {
            _viewModel.MoveChatToFolder(chat, null);
        }
    }

    private async void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = _viewModel.CreateSettingsViewModel();
        var dialog = new SettingsWindow(viewModel)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.ApplySettingsAsync(viewModel);
            ApplyHotkey();
        }
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (_viewModel.SendMessageCommand.CanExecute(null))
            {
                _viewModel.SendMessageCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void PromptNewFolder()
    {
        var name = PromptDialog.Show(this, "New folder", "Folder name:");
        if (!string.IsNullOrWhiteSpace(name))
        {
            _viewModel.AddFolder(name);
        }
    }

    private void AttachMessageCollection()
    {
        if (_activeMessages is not null)
        {
            _activeMessages.CollectionChanged -= Messages_CollectionChanged;
        }

        _activeMessages = _viewModel.ActiveMessages;
        _activeMessages.CollectionChanged += Messages_CollectionChanged;
        ScrollToEnd();
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        ChatScrollViewer?.ScrollToEnd();
    }

    private ChatFolderViewModel? FindFolder(string folderId)
    {
        foreach (var folder in _viewModel.Folders)
        {
            if (string.Equals(folder.Id, folderId, StringComparison.OrdinalIgnoreCase))
            {
                return folder;
            }
        }

        return null;
    }

    private static T? GetAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
