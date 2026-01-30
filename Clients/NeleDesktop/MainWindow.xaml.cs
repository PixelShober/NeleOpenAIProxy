using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfButton = System.Windows.Controls.Button;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using NeleDesktop.Models;
using NeleDesktop.Services;
using NeleDesktop.ViewModels;
using NeleDesktop.Views;
using WpfScreen = System.Windows.Forms.Screen;

namespace NeleDesktop;

public partial class MainWindow : Window
{
    private const int WmEnterSizeMove = 0x0231;
    private const int WmExitSizeMove = 0x0232;
    private readonly MainViewModel _viewModel = new();
    private HotkeyService? _hotkeyService;
    private HotkeyService? _temporaryHotkeyService;
    private WpfPoint _dragStart;
    private ObservableCollection<ChatMessage>? _activeMessages;
    private double? _widthBeforeTemporaryChat;
    private SettingsWindow? _settingsWindow;
    private bool _isHotkeyCaptureActive;
    private HwndSource? _windowSource;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.HotkeyChanged += (_, _) => ApplyHotkey();
        _viewModel.NewFolderRequested += (_, _) => PromptNewFolder();
        _viewModel.ApiKeyMissing += (_, _) =>
        {
            System.Windows.MessageBox.Show(this, "Please set your Nele AI API key in Settings.", "API key missing", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.ActiveMessages))
            {
                AttachMessageCollection();
            }

            if (args.PropertyName == nameof(MainViewModel.IsBusy))
            {
                ScrollToEnd();
            }

            if (args.PropertyName == nameof(MainViewModel.IsSidebarVisible))
            {
                AdjustWidthForSidebarToggle();
            }
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        ApplyWindowPlacement();
        AttachMessageCollection();
        if (string.IsNullOrWhiteSpace(_viewModel.Settings.ApiKey))
        {
            if (!App.IsAutoStartLaunch)
            {
                await OpenSettingsAsync();
            }
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hotkeyService = new HotkeyService(this);
        _temporaryHotkeyService = new HotkeyService(this);
        _hotkeyService.Initialize();
        _temporaryHotkeyService.Initialize();
        _windowSource = PresentationSource.FromVisual(this) as HwndSource;
        _windowSource?.AddHook(WindowProc);
        _hotkeyService.HotkeyPressed += (_, _) =>
        {
            if (_isHotkeyCaptureActive)
            {
                return;
            }

            ToggleVisibility();
        };
        _temporaryHotkeyService.HotkeyPressed += (_, _) =>
        {
            if (_isHotkeyCaptureActive)
            {
                return;
            }

            OpenTemporaryChat();
        };
        ApplyHotkey();
        UpdateMaximizedBounds();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowPlacement();

        if (!App.IsShuttingDown)
        {
            _viewModel.ClearTemporaryChat();
            e.Cancel = true;
            HideToTray();
            return;
        }

        _viewModel.FlushWindowPlacement();
        _hotkeyService?.Dispose();
        _temporaryHotkeyService?.Dispose();
        _windowSource?.RemoveHook(WindowProc);
    }

    private void ToggleVisibility()
    {
        CloseSettingsWindow();
        if (IsVisible)
        {
            HideToTray();
            return;
        }

        ShowFromTray();
    }

    private void ApplyHotkey()
    {
        if (_hotkeyService is null)
        {
            return;
        }

        _hotkeyService.Register(_viewModel.Settings.Hotkey);
        _temporaryHotkeyService?.Register(_viewModel.Settings.TemporaryHotkey);
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

        if (GetAncestor<TreeViewItem>((DependencyObject)e.OriginalSource) is { DataContext: ChatFolderViewModel _ } treeViewItem)
        {
            treeViewItem.IsExpanded = !treeViewItem.IsExpanded;
        }
    }

    private void ConversationTree_PreviewMouseMove(object sender, WpfMouseEventArgs e)
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
            DragDrop.DoDragDrop(ConversationTree, chat, System.Windows.DragDropEffects.Move);
        }
    }

    private void ConversationTree_Drop(object sender, WpfDragEventArgs e)
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

    private void ConversationTree_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (ConversationTree.SelectedItem is ChatConversationViewModel chat)
        {
            if (e.Key == Key.F2)
            {
                RenameChat(chat);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (ConfirmDeleteChat(chat))
                {
                    _viewModel.DeleteChat(chat);
                }

                e.Handled = true;
            }

            return;
        }

        if (ConversationTree.SelectedItem is ChatFolderViewModel folder)
        {
            if (e.Key == Key.F2)
            {
                RenameFolder(folder);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (ConfirmDeleteFolder(folder))
                {
                    _viewModel.DeleteFolder(folder);
                }

                e.Handled = true;
            }
        }
    }

    private void DeleteChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ChatConversationViewModel chat)
        {
            if (ConfirmDeleteChat(chat))
            {
                _viewModel.DeleteChat(chat);
            }
        }
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ChatFolderViewModel folder)
        {
            if (ConfirmDeleteFolder(folder))
            {
                _viewModel.DeleteFolder(folder);
            }
        }
    }

    private void ConvertTemporaryChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ChatConversationViewModel chat)
        {
            _viewModel.ConvertTemporaryChat(chat);
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
        await OpenSettingsAsync();
    }

    public void OpenSettingsFromTray()
    {
        _ = Dispatcher.InvokeAsync(async () =>
        {
            ShowFromTray();
            await OpenSettingsAsync();
        });
    }

    private async Task OpenSettingsAsync()
    {
        var viewModel = _viewModel.CreateSettingsViewModel();
        void HandleModelsChanged(object? sender, IReadOnlyList<string> models)
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(() => viewModel.InitializeModels(models));
                return;
            }

            viewModel.InitializeModels(models);
        }

        _viewModel.AvailableModelsChanged += HandleModelsChanged;
        var dialog = new SettingsWindow(viewModel)
        {
            Owner = this
        };
        _settingsWindow = dialog;
        dialog.Closed += (_, _) =>
        {
            _viewModel.AvailableModelsChanged -= HandleModelsChanged;
            _settingsWindow = null;
            _isHotkeyCaptureActive = false;
        };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.ApplySettingsAsync(viewModel);
            ApplyHotkey();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && GetAncestor<WpfButton>(source) is not null)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        SetDragCacheEnabled(true);
        try
        {
            DragMove();
        }
        finally
        {
            SetDragCacheEnabled(false);
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeIcon is null)
        {
            return;
        }

        MaximizeIcon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        UpdateMaximizedBounds();

        if (WindowState == WindowState.Minimized)
        {
            SaveWindowPlacement();
            _viewModel.ClearTemporaryChat();
            RestoreTemporaryWidth();
        }
    }

    private void InputBox_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                return;
            }

            if (_viewModel.SendMessageCommand.CanExecute(null))
            {
                _viewModel.SendMessageCommand.Execute(null);
            }

            e.Handled = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            if (key == Key.N)
            {
                if (_viewModel.IsApiKeyAvailable)
                {
                    _viewModel.NewChatCommand.Execute(null);
                }

                e.Handled = true;
                return;
            }

            if (key == Key.F)
            {
                _viewModel.NewFolderCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            HideToTray();
            e.Handled = true;
        }
    }

    private void RenameChat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ChatConversationViewModel chat)
        {
            RenameChat(chat);
        }
    }

    private void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ChatFolderViewModel folder)
        {
            RenameFolder(folder);
        }
    }

    private void MoveToFolder_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not ChatConversationViewModel chat)
        {
            return;
        }

        menuItem.Items.Clear();

        var noneItem = new MenuItem { Header = "No folder", CommandParameter = chat, IsCheckable = true };
        noneItem.IsChecked = string.IsNullOrWhiteSpace(chat.FolderId);
        noneItem.Click += MoveChatToFolder_Click;
        menuItem.Items.Add(noneItem);

        if (_viewModel.Folders.Count == 0)
        {
            return;
        }

        menuItem.Items.Add(new Separator());

        foreach (var folder in _viewModel.Folders)
        {
            var folderItem = new MenuItem
            {
                Header = folder.Name,
                Tag = folder.Id,
                CommandParameter = chat,
                IsChecked = string.Equals(chat.FolderId, folder.Id, StringComparison.OrdinalIgnoreCase),
                IsCheckable = true
            };
            folderItem.Click += MoveChatToFolder_Click;
            menuItem.Items.Add(folderItem);
        }
    }

    private void MoveToFolder_Open_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            menuItem.IsSubmenuOpen = true;
        }
    }

    private void MoveChatToFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.CommandParameter is not ChatConversationViewModel chat)
        {
            return;
        }

        var folderId = menuItem.Tag as string;
        var targetFolder = string.IsNullOrWhiteSpace(folderId) ? null : FindFolder(folderId);
        _viewModel.MoveChatToFolder(chat, targetFolder);
    }

    private void RenameChat(ChatConversationViewModel chat)
    {
        var name = PromptDialog.Show(this, "Rename chat", "Chat title:", chat.Title);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _viewModel.RenameChat(chat, name);
        }
    }

    private void RenameFolder(ChatFolderViewModel folder)
    {
        var name = PromptDialog.Show(this, "Rename folder", "Folder name:", folder.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            _viewModel.RenameFolder(folder, name);
        }
    }

    private bool ConfirmDeleteChat(ChatConversationViewModel chat)
    {
        return ConfirmDialog.Show(
            this,
            "Delete chat",
            $"Delete chat \"{chat.Title}\"?",
            "Delete");
    }

    private bool ConfirmDeleteFolder(ChatFolderViewModel folder)
    {
        return ConfirmDialog.Show(
            this,
            "Delete folder",
            $"Delete folder \"{folder.Name}\"? Chats will move to Conversations.",
            "Delete");
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

    private void FocusInputBox()
    {
        if (InputBox is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            InputBox.Focus();
            InputBox.CaretIndex = InputBox.Text?.Length ?? 0;
        }, DispatcherPriority.Input);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void UpdateMaximizedBounds()
    {
        if (WindowState != WindowState.Maximized)
        {
            MaxHeight = double.PositiveInfinity;
            MaxWidth = double.PositiveInfinity;
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var screen = WpfScreen.FromHandle(handle);
        var workArea = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return;
        }

        var transform = source.CompositionTarget.TransformFromDevice;
        var size = transform.Transform(new WpfPoint(workArea.Width, workArea.Height));
        var origin = transform.Transform(new WpfPoint(workArea.Left, workArea.Top));
        MaxHeight = size.Y;
        MaxWidth = size.X;
        Top = origin.Y;
        Left = origin.X;
    }

    private void OpenTemporaryChat()
    {
        CloseSettingsWindow();
        if (_widthBeforeTemporaryChat is null)
        {
            _widthBeforeTemporaryChat = Width;
        }

        _viewModel.OpenTemporaryChat();
        ShowFromTray();
        ApplyTemporaryWidth();
    }

    public void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
        Activate();
        Focus();
        FocusInputBox();
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        _viewModel.ClearTemporaryChat();
        RestoreTemporaryWidth();
        Hide();
    }

    private void CloseSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        if (_settingsWindow.Dispatcher.CheckAccess())
        {
            if (_settingsWindow.IsVisible)
            {
                _settingsWindow.DialogResult = false;
            }
        }
        else
        {
            _settingsWindow.Dispatcher.Invoke(() =>
            {
                if (_settingsWindow.IsVisible)
                {
                    _settingsWindow.DialogResult = false;
                }
            });
        }
    }

    public void SetHotkeyCaptureActive(bool isActive)
    {
        _isHotkeyCaptureActive = isActive;
    }

    private void ApplyTemporaryWidth()
    {
        if (_viewModel.IsSidebarVisible)
        {
            return;
        }

        var right = Left + Width;
        Width = Math.Min(Width, _viewModel.CompactWindowWidth);
        if (WindowState == WindowState.Normal)
        {
            Left = right - Width;
        }
    }

    private void RestoreTemporaryWidth()
    {
        if (_widthBeforeTemporaryChat is null)
        {
            return;
        }

        if (WindowState == WindowState.Normal)
        {
            var right = Left + Width;
            Width = _widthBeforeTemporaryChat.Value;
            Left = right - Width;
        }

        _widthBeforeTemporaryChat = null;
    }

    private void AdjustWidthForSidebarToggle()
    {
        if (_widthBeforeTemporaryChat is null)
        {
            return;
        }

        if (_viewModel.IsSidebarVisible)
        {
            var right = Left + Width;
            Width = Math.Max(Width, _widthBeforeTemporaryChat.Value);
            if (WindowState == WindowState.Normal)
            {
                Left = right - Width;
            }
            return;
        }

        ApplyTemporaryWidth();
    }

    private void ApplyWindowPlacement()
    {
        var settings = _viewModel.Settings;
        if (settings.WindowWidth is > 0 && settings.WindowHeight is > 0)
        {
            Width = settings.WindowWidth.Value;
            Height = settings.WindowHeight.Value;
        }

        if (settings.WindowLeft is not null && settings.WindowTop is not null)
        {
            Left = settings.WindowLeft.Value;
            Top = settings.WindowTop.Value;
        }
    }

    private void SaveWindowPlacement()
    {
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            _viewModel.UpdateWindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        }
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            SaveWindowPlacement();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            SaveWindowPlacement();
        }
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

    private void ToolsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button)
        {
            return;
        }

        if (button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void ChatDropZone_DragEnter(object sender, WpfDragEventArgs e)
    {
        UpdateDragOverlay(e);
    }

    private void ChatDropZone_DragOver(object sender, WpfDragEventArgs e)
    {
        UpdateDragOverlay(e);
    }

    private void ChatDropZone_DragLeave(object sender, WpfDragEventArgs e)
    {
        _viewModel.ResetDragOverlay();
        e.Handled = true;
    }

    private async void ChatDropZone_Drop(object sender, WpfDragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                var allowed = _viewModel.UpdateDragOverlayForFiles(files);
                if (allowed)
                {
                    await _viewModel.AddPendingAttachmentsAsync(files);
                }
            }
        }

        _viewModel.ResetDragOverlay();
        e.Handled = true;
    }

    private void UpdateDragOverlay(WpfDragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                var allowed = _viewModel.UpdateDragOverlayForFiles(files);
                e.Effects = allowed ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
                _viewModel.ResetDragOverlay();
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
            _viewModel.ResetDragOverlay();
        }

        e.Handled = true;
    }

    private void RemovePendingAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.DataContext is ChatAttachment attachment)
        {
            _viewModel.RemovePendingAttachment(attachment);
        }
    }

    private void SetDragCacheEnabled(bool enabled)
    {
        if (MainRoot is null)
        {
            return;
        }

        MainRoot.CacheMode = enabled ? new BitmapCache(1.0) : null;
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmEnterSizeMove)
        {
            SetDragCacheEnabled(true);
        }
        else if (msg == WmExitSizeMove)
        {
            SetDragCacheEnabled(false);
        }

        return IntPtr.Zero;
    }
}

