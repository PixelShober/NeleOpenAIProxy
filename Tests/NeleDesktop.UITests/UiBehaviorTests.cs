using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using NeleDesktop;
using NeleDesktop.Models;
using NeleDesktop.ViewModels;

namespace NeleDesktop.UITests;

[TestClass]
public sealed class UiBehaviorTests
{
    [TestMethod]
    public void ThemeColors_HaveReadableContrast()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            var dark = UiTestHelpers.LoadThemeDictionary("Dark.xaml");
            AssertContrast(dark, "PrimaryTextColor", "WindowBackgroundColor", 4.5);
            AssertContrast(dark, "SecondaryTextColor", "WindowBackgroundColor", 3.0);
            AssertContrast(dark, "PrimaryTextColor", "PanelBackgroundColor", 4.5);

            var light = UiTestHelpers.LoadThemeDictionary("Light.xaml");
            AssertContrast(light, "PrimaryTextColor", "WindowBackgroundColor", 4.5);
            AssertContrast(light, "SecondaryTextColor", "WindowBackgroundColor", 3.0);
            AssertContrast(light, "PrimaryTextColor", "PanelBackgroundColor", 4.5);
        });
    }

    [TestMethod]
    public void ConversationTree_DisablesHorizontalScroll()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();
            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var tree = window.FindName("ConversationTree") as TreeView;
            Assert.IsNotNull(tree, "ConversationTree was not found.");
            var visibility = (ScrollBarVisibility)tree.GetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty);
            Assert.AreEqual(ScrollBarVisibility.Disabled, visibility);
        });
    }

    [TestMethod]
    public void ConversationTemplates_UseMousePointContextMenus()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            var tree = window.FindName("ConversationTree") as TreeView;
            Assert.IsNotNull(tree, "ConversationTree was not found.");

            var folderTemplate = UiTestHelpers.FindTemplate(tree.Resources, typeof(ChatFolderViewModel));
            Assert.IsNotNull(folderTemplate, "Folder template was not found.");
            var folderRoot = folderTemplate.LoadContent() as FrameworkElement;
            Assert.IsNotNull(folderRoot, "Folder template root was not created.");
            Assert.IsNotNull(folderRoot.ContextMenu, "Folder context menu missing.");
            Assert.AreEqual(PlacementMode.MousePoint, folderRoot.ContextMenu.Placement);

            var chatTemplate = UiTestHelpers.FindTemplate(tree.Resources, typeof(ChatConversationViewModel));
            Assert.IsNotNull(chatTemplate, "Chat template was not found.");
            var chatRoot = chatTemplate.LoadContent() as FrameworkElement;
            Assert.IsNotNull(chatRoot, "Chat template root was not created.");
            Assert.IsNotNull(chatRoot.ContextMenu, "Chat context menu missing.");
            Assert.AreEqual(PlacementMode.MousePoint, chatRoot.ContextMenu.Placement);
        });
    }

    [TestMethod]
    public void ConversationTemplates_TruncateLongText()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            var tree = window.FindName("ConversationTree") as TreeView;
            Assert.IsNotNull(tree, "ConversationTree was not found.");

            var folderTemplate = UiTestHelpers.FindTemplate(tree.Resources, typeof(ChatFolderViewModel));
            Assert.IsNotNull(folderTemplate, "Folder template was not found.");
            var folderRoot = folderTemplate.LoadContent() as FrameworkElement;
            Assert.IsNotNull(folderRoot, "Folder template root was not created.");
            var folderText = UiTestHelpers.FindVisualChild<TextBlock>(folderRoot, tb => tb.TextTrimming == TextTrimming.CharacterEllipsis);
            Assert.IsNotNull(folderText, "Folder name text trimming is missing.");

            var chatTemplate = UiTestHelpers.FindTemplate(tree.Resources, typeof(ChatConversationViewModel));
            Assert.IsNotNull(chatTemplate, "Chat template was not found.");
            var chatRoot = chatTemplate.LoadContent() as FrameworkElement;
            Assert.IsNotNull(chatRoot, "Chat template root was not created.");
            var chatText = UiTestHelpers.FindVisualChild<TextBlock>(chatRoot, tb => tb.TextTrimming == TextTrimming.CharacterEllipsis);
            Assert.IsNotNull(chatText, "Chat title text trimming is missing.");
        });
    }

    [TestMethod]
    public void SettingsWindow_HasAutoStartToggle()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var viewModel = new SettingsViewModel(new NeleDesktop.Services.NeleApiClient(), new AppSettings());
            var window = new NeleDesktop.Views.SettingsWindow(viewModel);
            window.ApplyTemplate();

            var toggle = window.FindName("AutoStartToggle") as ToggleButton;
            Assert.IsNotNull(toggle, "AutoStart toggle was not found.");
        });
    }

    [TestMethod]
    public void BubbleWidthConverter_RespectsRole()
    {
        var converter = new NeleDesktop.Converters.BubbleWidthConverter();
        var assistant = (double)converter.Convert(new object[] { 600d, "assistant" }, typeof(double), string.Empty, System.Globalization.CultureInfo.InvariantCulture);
        var user = (double)converter.Convert(new object[] { 600d, "user" }, typeof(double), string.Empty, System.Globalization.CultureInfo.InvariantCulture);
        var assistantMin = (double)converter.Convert(new object[] { 600d, "assistant" }, typeof(double), "min", System.Globalization.CultureInfo.InvariantCulture);
        var userMin = (double)converter.Convert(new object[] { 600d, "user" }, typeof(double), "min", System.Globalization.CultureInfo.InvariantCulture);

        Assert.IsTrue(assistant > 500, $"Assistant width should be near full width, got {assistant:F1}.");
        Assert.IsTrue(user > 500, $"User max width should allow full line, got {user:F1}.");
        Assert.AreEqual(0d, userMin, "User min width should be zero for compact bubbles.");
        Assert.IsTrue(assistantMin > 0, "Assistant min width should be positive.");
    }

    [TestMethod]
    public void ChatBubbles_ResizeWithWindow()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            var viewModel = new MainViewModel();
            var conversation = new ChatConversation
            {
                Messages = new ObservableCollection<ChatMessage>
                {
                    new() { Role = "user", Content = "Test" },
                    new() { Role = "assistant", Content = string.Join(' ', Enumerable.Repeat("Long assistant response", 6)) }
                }
            };
            viewModel.SelectedChat = new ChatConversationViewModel(conversation);
            window.DataContext = viewModel;
            window.Width = 800;
            window.Height = 600;
            UiTestHelpers.DoEvents();
            window.ApplyTemplate();
            window.Measure(new Size(window.Width, window.Height));
            window.Arrange(new Rect(0, 0, window.Width, window.Height));
            window.UpdateLayout();

            var root = window.Content as FrameworkElement;
            Assert.IsNotNull(root, "Main window content was not created.");
            root.Measure(new Size(window.Width, window.Height));
            root.Arrange(new Rect(0, 0, window.Width, window.Height));
            root.UpdateLayout();

            var bubbles = FindMessageBubbles(root);
            var userBubble = bubbles.First(b => (b.DataContext as ChatMessage)?.Role == "user");
            var assistantBubble = bubbles.First(b => (b.DataContext as ChatMessage)?.Role == "assistant");
            var chatWidthLarge = ((FrameworkElement)window.FindName("ChatScrollViewer")).ActualWidth;
            var userWidthLarge = userBubble.ActualWidth;
            var assistantWidthLarge = assistantBubble.ActualWidth;

            Assert.IsTrue(userWidthLarge < assistantWidthLarge, "User bubble should be narrower than assistant bubble.");
            Assert.IsTrue(userWidthLarge < chatWidthLarge * 0.75, "User bubble should not span the full chat width.");

            window.Width = 480;
            UiTestHelpers.DoEvents();
            window.Measure(new Size(window.Width, window.Height));
            window.Arrange(new Rect(0, 0, window.Width, window.Height));
            window.UpdateLayout();

            var chatWidthSmall = ((FrameworkElement)window.FindName("ChatScrollViewer")).ActualWidth;
            var userWidthSmall = userBubble.ActualWidth;
            var assistantWidthSmall = assistantBubble.ActualWidth;

            Assert.IsTrue(assistantWidthSmall <= assistantWidthLarge, "Assistant bubble should not grow when window narrows.");
            Assert.IsTrue(assistantWidthSmall <= chatWidthSmall + 1, "Assistant bubble should fit the smaller chat width.");
            Assert.IsTrue(assistantWidthSmall > userWidthSmall, "Assistant bubble should remain wider than user bubble.");

        });
    }

    [TestMethod]
    public void ChatMessageText_IsSelectable()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();
            window.Measure(new Size(800, 600));
            window.Arrange(new Rect(0, 0, 800, 600));
            window.UpdateLayout();

            var scrollViewer = window.FindName("ChatScrollViewer") as ScrollViewer;
            Assert.IsNotNull(scrollViewer, "ChatScrollViewer not found.");
            var itemsControl = UiTestHelpers.FindLogicalChild<ItemsControl>(scrollViewer, control => control is not TreeView);
            Assert.IsNotNull(itemsControl, "Messages ItemsControl not found.");
            Assert.IsNotNull(itemsControl.ItemTemplate, "Message ItemTemplate missing.");

            var content = itemsControl.ItemTemplate.LoadContent() as FrameworkElement;
            Assert.IsNotNull(content, "Message template content not created.");
            var messageBox = UiTestHelpers.FindVisualChild<RichTextBox>(content);
            Assert.IsNotNull(messageBox, "Message RichTextBox not found.");
            Assert.IsTrue(messageBox.IsReadOnly, "Message text is not read-only.");
            Assert.AreEqual(new Thickness(0), messageBox.BorderThickness, "Message RichTextBox should not draw a border.");
            var binding = BindingOperations.GetBindingExpression(messageBox, NeleDesktop.Converters.MarkdownRichTextBox.TextProperty);
            Assert.IsNotNull(binding, "Markdown binding is missing on message RichTextBox.");
        });
    }

    [TestMethod]
    public void MarkdownConverter_FormatsBoldAndItalic()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var converter = new NeleDesktop.Converters.MarkdownToFlowDocumentConverter();
            var document = converter.Convert("***BoldItalic*** and **Bold** and *Italic*", typeof(System.Windows.Documents.FlowDocument), null!, System.Globalization.CultureInfo.InvariantCulture)
                as System.Windows.Documents.FlowDocument;
            Assert.IsNotNull(document, "Markdown converter did not return a FlowDocument.");

            var text = new System.Text.StringBuilder();
            foreach (var block in document!.Blocks)
            {
                if (block is System.Windows.Documents.Paragraph paragraph)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        if (inline is System.Windows.Documents.Run run)
                        {
                            text.Append(run.Text);
                        }
                    }
                }
            }

            Assert.IsTrue(text.ToString().Contains("BoldItalic"));
            Assert.IsTrue(text.ToString().Contains("Bold"));
            Assert.IsTrue(text.ToString().Contains("Italic"));
        });
    }

    [TestMethod]
    public void SubmenuPopup_UsesPlacementTargetBinding()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));

            var menuItem = new MenuItem { Header = "Move to folder" };
            menuItem.Items.Add(new MenuItem { Header = "Folder A" });
            menuItem.ApplyTemplate();

            var popup = menuItem.Template.FindName("SubmenuPopup", menuItem) as Popup;
            Assert.IsNotNull(popup, "Submenu popup not found in MenuItem template.");
            Assert.AreEqual(PlacementMode.Right, popup.Placement);

            var binding = BindingOperations.GetBindingExpression(popup, Popup.PlacementTargetProperty);
            Assert.IsNotNull(binding, "Submenu popup missing PlacementTarget binding.");
        });
    }

    [TestMethod]
    public void ChatHeader_ModelDropdownIsBelowTitle()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();
            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var rootElement = window.Content as DependencyObject;
            Assert.IsNotNull(rootElement, "Root element not found.");

            var selectorButton = window.FindName("ModelSelectorButton") as Button
                ?? UiTestHelpers.FindVisualChild<Button>(rootElement, button => string.Equals(button.Name, "ModelSelectorButton", StringComparison.Ordinal));
            Assert.IsNotNull(selectorButton, "Model selector button not found.");
            var titleText = UiTestHelpers.FindVisualChild<TextBlock>(rootElement, tb => Math.Abs(tb.FontSize - 18) < 0.1);
            Assert.IsNotNull(titleText, "Chat title text not found.");

            var titleBounds = titleText.TransformToAncestor((Visual)rootElement)
                .TransformBounds(new Rect(0, 0, titleText.ActualWidth, titleText.ActualHeight));
            var selectorBounds = selectorButton.TransformToAncestor((Visual)rootElement)
                .TransformBounds(new Rect(0, 0, selectorButton.ActualWidth, selectorButton.ActualHeight));

            Assert.IsTrue(titleBounds.Bottom <= selectorBounds.Top + 2,
                "Title text should be above the model selector.");
            Assert.AreEqual(HorizontalAlignment.Left, selectorButton.HorizontalAlignment, "Model selector should be left aligned.");
        });
    }

    [TestMethod]
    public void ChatHeader_ReasoningDropdownBindsToSelectedChat()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            var viewModel = new MainViewModel();
            var conversation = new ChatConversation { Model = "gpt-5" };
            viewModel.SelectedChat = new ChatConversationViewModel(conversation);
            viewModel.IsModelSelectorOpen = true;
            viewModel.SelectedChat.IsReasoningOptionsOpen = true;
            window.DataContext = viewModel;
            window.ApplyTemplate();
            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }
            UiTestHelpers.DoEvents();

            var rootElement = window.Content as DependencyObject;
            Assert.IsNotNull(rootElement, "Root element not found.");

            var selectorButton = window.FindName("ModelSelectorButton") as Button
                ?? UiTestHelpers.FindVisualChild<Button>(rootElement, button => string.Equals(button.Name, "ModelSelectorButton", StringComparison.Ordinal));
            Assert.IsNotNull(selectorButton, "Model selector button not found.");

            var popup = UiTestHelpers.FindVisualChild<Popup>(rootElement, candidate => ReferenceEquals(candidate.PlacementTarget, selectorButton));
            Assert.IsNotNull(popup, "Model selector popup not found.");

            var popupRoot = popup.Child as DependencyObject;
            Assert.IsNotNull(popupRoot, "Model selector popup content not found.");

            var reasoningList = UiTestHelpers.FindVisualChild<ItemsControl>(popupRoot, control =>
            {
                var binding = BindingOperations.GetBindingExpression(control, ItemsControl.ItemsSourceProperty);
                return string.Equals(binding?.ParentBinding?.Path?.Path, "SelectedChat.ReasoningOptions", StringComparison.Ordinal);
            });

            Assert.IsNotNull(reasoningList, "Reasoning dropdown not found.");
        });
    }

    [TestMethod]
    public void MoveToFolderMenu_StaysOpenOnClick()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            var tree = window.FindName("ConversationTree") as TreeView;
            Assert.IsNotNull(tree, "ConversationTree was not found.");

            var chatTemplate = UiTestHelpers.FindTemplate(tree.Resources, typeof(ChatConversationViewModel));
            Assert.IsNotNull(chatTemplate, "Chat template was not found.");
            var chatRoot = chatTemplate.LoadContent() as FrameworkElement;
            Assert.IsNotNull(chatRoot, "Chat template root was not created.");
            Assert.IsNotNull(chatRoot.ContextMenu, "Chat context menu missing.");

            var moveMenu = chatRoot.ContextMenu.Items.OfType<MenuItem>()
                .FirstOrDefault(item => string.Equals(item.Header as string, "Move to folder", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(moveMenu, "Move to folder menu item not found.");
            Assert.IsTrue(moveMenu.StaysOpenOnClick, "Move to folder should stay open on click.");
        });
    }

    [TestMethod]
    public void PromptDialog_UsesDefaultOkButton()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var dialog = (Window?)Activator.CreateInstance(
                typeof(NeleDesktop.Views.PromptDialog),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new object?[] { "Rename chat", "Chat title:", "Test" },
                null);
            Assert.IsNotNull(dialog, "PromptDialog could not be created.");
            dialog.ApplyTemplate();
            dialog.Measure(new Size(400, 200));
            dialog.Arrange(new Rect(0, 0, 400, 200));
            dialog.UpdateLayout();

            var okButton = UiTestHelpers.FindLogicalChild<Button>(dialog, button => string.Equals(button.Content as string, "OK", StringComparison.OrdinalIgnoreCase))
                ?? UiTestHelpers.FindVisualChild<Button>(dialog, button => string.Equals(button.Content as string, "OK", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(okButton, "OK button not found on PromptDialog.");
            Assert.IsTrue(okButton.IsDefault, "OK button should be default to allow Enter confirm.");
        });
    }

    [TestMethod]
    public void BusyIndicator_UsesPulseEllipse()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            if (window.DataContext is MainViewModel viewModel)
            {
                viewModel.IsBusy = true;
            }

            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var ellipse = window.FindName("BusyIndicatorPulse") as Ellipse;
            Assert.IsNotNull(ellipse, "Busy indicator ellipse not found.");
            Assert.IsInstanceOfType(ellipse.RenderTransform, typeof(ScaleTransform), "Busy indicator should use scale transform.");
        });
    }

    [TestMethod]
    public void ConfirmDialog_UsesThemeAndDefaultAction()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var dialog = (Window?)Activator.CreateInstance(
                typeof(NeleDesktop.Views.ConfirmDialog),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new object?[] { "Delete chat", "Delete this chat?", "Delete" },
                null);
            Assert.IsNotNull(dialog, "ConfirmDialog could not be created.");
            dialog.ApplyTemplate();
            dialog.Measure(new Size(400, 200));
            dialog.Arrange(new Rect(0, 0, 400, 200));
            dialog.UpdateLayout();

            var confirmButton = dialog.FindName("ConfirmButton") as Button;
            Assert.IsNotNull(confirmButton, "Confirm button not found.");
            Assert.IsTrue(confirmButton.IsDefault, "Confirm button should be default.");
            Assert.AreEqual("Delete", confirmButton.Content);
        });
    }

    [TestMethod]
    public void TemporaryChatMenuItem_IsConditional()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            var tree = window.FindName("ConversationTree") as TreeView;
            Assert.IsNotNull(tree, "ConversationTree was not found.");

            var chatTemplate = UiTestHelpers.FindTemplate(tree.Resources, typeof(ChatConversationViewModel));
            Assert.IsNotNull(chatTemplate, "Chat template was not found.");
            var chatRoot = chatTemplate.LoadContent() as FrameworkElement;
            Assert.IsNotNull(chatRoot, "Chat template root was not created.");
            Assert.IsNotNull(chatRoot.ContextMenu, "Chat context menu missing.");

            var convertMenu = chatRoot.ContextMenu.Items.OfType<MenuItem>()
                .FirstOrDefault(item => string.Equals(item.Header as string, "Convert to regular chat", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(convertMenu, "Convert to regular chat menu item not found.");

            var binding = BindingOperations.GetBindingExpression(convertMenu, MenuItem.VisibilityProperty);
            Assert.IsNotNull(binding, "Convert to regular chat menu should be visibility-bound.");
        });
    }

    [TestMethod]
    public void SettingsWindow_ShowsTemporaryHotkeyField()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var viewModel = new SettingsViewModel(new NeleDesktop.Services.NeleApiClient(), new NeleDesktop.Models.AppSettings());
            var window = new NeleDesktop.Views.SettingsWindow(viewModel);
            window.Show();
            UiTestHelpers.DoEvents();
            window.ApplyTemplate();
            var size = new Size(window.Width, window.Height);
            window.Measure(size);
            window.Arrange(new Rect(0, 0, size.Width, size.Height));
            window.UpdateLayout();

            var label = UiTestHelpers.FindLogicalChild<TextBlock>(window, text => string.Equals(text.Text, "Temporary chat hotkey", StringComparison.OrdinalIgnoreCase))
                ?? UiTestHelpers.FindVisualChild<TextBlock>(window, text => string.Equals(text.Text, "Temporary chat hotkey", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(label, "Temporary chat hotkey label is missing.");
        });
    }

    [TestMethod]
    public void SettingsWindow_WebSearchDefaultToggleExists()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var viewModel = new SettingsViewModel(new NeleDesktop.Services.NeleApiClient(), new NeleDesktop.Models.AppSettings());
            var window = new NeleDesktop.Views.SettingsWindow(viewModel);
            window.ApplyTemplate();
            window.UpdateLayout();

            var toggle = window.FindName("WebSearchDefaultToggle") as ToggleButton;
            Assert.IsNotNull(toggle, "Web search default toggle not found.");
            var binding = BindingOperations.GetBindingExpression(toggle, ToggleButton.IsCheckedProperty);
            Assert.IsNotNull(binding, "Web search default toggle is not data-bound.");
        });
    }

    [TestMethod]
    public void SettingsWindow_TranscriptionModelDropdownExists()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var viewModel = new SettingsViewModel(new NeleDesktop.Services.NeleApiClient(), new NeleDesktop.Models.AppSettings());
            var window = new NeleDesktop.Views.SettingsWindow(viewModel);
            window.ApplyTemplate();
            window.UpdateLayout();

            var comboBox = window.FindName("TranscriptionModelCombo") as ComboBox;
            Assert.IsNotNull(comboBox, "Transcription model ComboBox not found.");
        });
    }

    [TestMethod]
    public void SettingsWindow_HotkeyFieldsAreReadOnly()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var viewModel = new SettingsViewModel(new NeleDesktop.Services.NeleApiClient(), new NeleDesktop.Models.AppSettings());
            var window = new NeleDesktop.Views.SettingsWindow(viewModel);
            window.ApplyTemplate();
            var size = new Size(window.Width, window.Height);
            window.Measure(size);
            window.Arrange(new Rect(0, 0, size.Width, size.Height));
            window.UpdateLayout();

            var hotkeyBox = window.FindName("HotkeyInput") as TextBox;
            var tempHotkeyBox = window.FindName("TemporaryHotkeyInput") as TextBox;

            Assert.IsNotNull(hotkeyBox, "Hotkey TextBox not found.");
            Assert.IsNotNull(tempHotkeyBox, "Temporary hotkey TextBox not found.");
            Assert.IsTrue(hotkeyBox.IsReadOnly, "Hotkey TextBox should be read-only.");
            Assert.IsTrue(tempHotkeyBox.IsReadOnly, "Temporary hotkey TextBox should be read-only.");

            window.Close();
        });
    }

    [TestMethod]
    public void SettingsWindow_HotkeyHintsExist()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var viewModel = new SettingsViewModel(new NeleDesktop.Services.NeleApiClient(), new NeleDesktop.Models.AppSettings());
            var window = new NeleDesktop.Views.SettingsWindow(viewModel);
            window.ApplyTemplate();
            var size = new Size(window.Width, window.Height);
            window.Measure(size);
            window.Arrange(new Rect(0, 0, size.Width, size.Height));
            window.UpdateLayout();

            var hotkeyHint = window.FindName("HotkeyHint") as TextBlock;
            var tempHotkeyHint = window.FindName("TemporaryHotkeyHint") as TextBlock;

            Assert.IsNotNull(hotkeyHint, "Hotkey hint text missing.");
            Assert.IsNotNull(tempHotkeyHint, "Temporary hotkey hint text missing.");
            Assert.AreEqual("Press keys to set hotkey", hotkeyHint.Text);
            Assert.AreEqual("Press keys to set hotkey", tempHotkeyHint.Text);
        });
    }

    [TestMethod]
    public void SettingsWindow_TemporaryHotkeyFieldFitsDefaultHeight()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var viewModel = new SettingsViewModel(new NeleDesktop.Services.NeleApiClient(), new NeleDesktop.Models.AppSettings());
            var window = new NeleDesktop.Views.SettingsWindow(viewModel);
            window.Show();
            UiTestHelpers.DoEvents();
            window.ApplyTemplate();
            window.UpdateLayout();

            var input = window.FindName("TemporaryHotkeyInput") as TextBox;
            Assert.IsNotNull(input, "Temporary hotkey input not found.");
            Assert.IsNotNull(window.Content, "Settings window content not found.");

            var content = (FrameworkElement)window.Content;
            var bounds = input.TransformToAncestor(content)
                .TransformBounds(new Rect(0, 0, input.ActualWidth, input.ActualHeight));
            Assert.IsTrue(bounds.Bottom <= content.ActualHeight - 12,
                $"Temporary hotkey input is clipped (bottom {bounds.Bottom:F1} > content height {content.ActualHeight:F1}).");

            window.Close();
        });
    }

    [TestMethod]
    public void SettingsWindow_TemporaryHotkeyHintFitsDefaultHeight()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var viewModel = new SettingsViewModel(new NeleDesktop.Services.NeleApiClient(), new NeleDesktop.Models.AppSettings())
            {
                IsTemporaryHotkeyCaptureActive = true
            };
            var window = new NeleDesktop.Views.SettingsWindow(viewModel);
            window.Show();
            UiTestHelpers.DoEvents();
            window.ApplyTemplate();
            window.UpdateLayout();

            var hint = window.FindName("TemporaryHotkeyHint") as TextBlock;
            Assert.IsNotNull(hint, "Temporary hotkey hint not found.");
            Assert.AreEqual(Visibility.Visible, hint.Visibility, "Temporary hotkey hint should be visible.");
            Assert.IsNotNull(window.Content, "Settings window content not found.");

            var content = (FrameworkElement)window.Content;
            var bounds = hint.TransformToAncestor(content)
                .TransformBounds(new Rect(0, 0, hint.ActualWidth, hint.ActualHeight));
            Assert.IsTrue(bounds.Bottom <= content.ActualHeight - 8,
                $"Temporary hotkey hint is clipped (bottom {bounds.Bottom:F1} > content height {content.ActualHeight:F1}).");

            window.Close();
        });
    }

    [TestMethod]
    public void ChatHeader_WebSearchMenuExists()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();
            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var toolsButton = window.FindName("ToolsButton") as Button;
            Assert.IsNotNull(toolsButton, "Tools button not found.");
            Assert.IsNotNull(toolsButton.ContextMenu, "Tools context menu not found.");

            var menuItem = toolsButton.ContextMenu.Items.OfType<MenuItem>()
                .FirstOrDefault(item => string.Equals(item.Header as string, "Web search", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(menuItem, "Web search menu item not found.");
            Assert.IsTrue(menuItem.IsCheckable, "Web search menu item should be checkable.");
            var binding = BindingOperations.GetBindingExpression(menuItem, MenuItem.IsCheckedProperty);
            Assert.IsNotNull(binding, "Web search menu item is not data-bound.");
        });
    }

    [TestMethod]
    public void ChatInput_PendingAttachmentsPanelBinds()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var pendingPanel = window.FindName("PendingAttachmentsPanel") as ItemsControl;
            Assert.IsNotNull(pendingPanel, "Pending attachments panel not found.");

            var binding = BindingOperations.GetBindingExpression(pendingPanel, ItemsControl.ItemsSourceProperty);
            Assert.IsNotNull(binding, "Pending attachments ItemsSource is not data-bound.");
            Assert.AreEqual("PendingAttachments", binding.ParentBinding?.Path?.Path);

            var panel = pendingPanel.ItemsPanel.LoadContent() as Panel;
            Assert.IsInstanceOfType(panel, typeof(WrapPanel), "Pending attachments panel should use a WrapPanel.");
        });
    }

    [TestMethod]
    public void PendingAttachmentTemplate_ShowsProgressBar()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            var template = window.Resources["PendingAttachmentTemplate"] as DataTemplate;
            Assert.IsNotNull(template, "PendingAttachmentTemplate not found.");
            var content = template!.LoadContent() as FrameworkElement;
            Assert.IsNotNull(content, "Pending attachment template content not created.");

            var progress = UiTestHelpers.FindVisualChild<ProgressBar>(content);
            Assert.IsNotNull(progress, "Progress bar not found in pending attachment template.");
        });
    }

    [TestMethod]
    public void ChatInput_MicrophoneButtonExists()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var micButton = window.FindName("MicButton") as Button;
            Assert.IsNotNull(micButton, "Mic button not found.");
            Assert.IsNotNull(micButton.Command, "Mic button should be command-bound.");
        });
    }

    [TestMethod]
    public void ChatInput_RecordingWaveformExists()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var waveform = window.FindName("RecordingWaveform") as Border;
            Assert.IsNotNull(waveform, "Recording waveform container not found.");
        });
    }

    [TestMethod]
    public void ChatMessage_AttachmentChipsArePresentInTemplate()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var scrollViewer = window.FindName("ChatScrollViewer") as ScrollViewer;
            Assert.IsNotNull(scrollViewer, "ChatScrollViewer not found.");

            ItemsControl? messageItems = null;
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(scrollViewer);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var childCount = VisualTreeHelper.GetChildrenCount(current);
                for (var i = 0; i < childCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(current, i);
                    if (child is ItemsControl items)
                    {
                        var binding = BindingOperations.GetBindingExpression(items, ItemsControl.ItemsSourceProperty);
                        if (string.Equals(binding?.ParentBinding?.Path?.Path, "ActiveMessages", StringComparison.Ordinal))
                        {
                            messageItems = items;
                            break;
                        }
                    }

                    queue.Enqueue(child);
                }

                if (messageItems is not null)
                {
                    break;
                }
            }

            Assert.IsNotNull(messageItems, "Message ItemsControl not found.");
            Assert.IsNotNull(messageItems.ItemTemplate, "Message ItemTemplate missing.");

            var templateRoot = messageItems.ItemTemplate.LoadContent() as FrameworkElement;
            Assert.IsNotNull(templateRoot, "Message template could not be loaded.");

            var attachmentList = UiTestHelpers.FindVisualChild<ItemsControl>(templateRoot, control =>
            {
                var binding = BindingOperations.GetBindingExpression(control, ItemsControl.ItemsSourceProperty);
                return string.Equals(binding?.ParentBinding?.Path?.Path, "Attachments", StringComparison.Ordinal);
            });

            Assert.IsNotNull(attachmentList, "Attachment list ItemsControl missing in message template.");
            Assert.IsNotNull(attachmentList.ItemTemplate, "Attachment chip template not applied.");
        });
    }

    [TestMethod]
    public void ChatDragOverlay_IsConfiguredForFileDrop()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow();
            window.ApplyTemplate();

            if (window.Content is FrameworkElement root)
            {
                root.Measure(new Size(800, 600));
                root.Arrange(new Rect(0, 0, 800, 600));
                root.UpdateLayout();
            }

            var overlay = window.FindName("ChatDragOverlay") as Border;
            Assert.IsNotNull(overlay, "Chat drag overlay not found.");
            Assert.IsFalse(overlay.IsHitTestVisible, "Chat drag overlay should not intercept input.");
            Assert.IsTrue(overlay.Opacity <= 0.01, "Chat drag overlay should start hidden.");
        });
    }

    [TestMethod]
    public void MainWindow_MaximizedBoundsStayWithinWorkArea()
    {
        UiTestHelpers.RunOnSta(() =>
        {
            UiTestHelpers.ApplyTheme(UiTestHelpers.LoadThemeDictionary("Dark.xaml"));
            var window = new MainWindow
            {
                Left = 0,
                Top = 0
            };
            window.Show();
            UiTestHelpers.DoEvents();
            window.WindowState = WindowState.Maximized;
            UiTestHelpers.DoEvents();
            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            var screen = System.Windows.Forms.Screen.FromHandle(handle);
            var workArea = screen.WorkingArea;
            var source = PresentationSource.FromVisual(window);
            var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var origin = transform.Transform(new Point(workArea.Left, workArea.Top));
            var size = transform.Transform(new Point(workArea.Width, workArea.Height));
            Assert.IsFalse(double.IsPositiveInfinity(window.MaxHeight), "MaxHeight should be constrained when maximized.");
            Assert.IsFalse(double.IsPositiveInfinity(window.MaxWidth), "MaxWidth should be constrained when maximized.");
            Assert.IsTrue(window.MaxHeight <= size.Y + 1,
                $"MaxHeight {window.MaxHeight:F1} exceeds work area height {size.Y:F1}.");
            Assert.IsTrue(window.MaxWidth <= size.X + 1,
                $"MaxWidth {window.MaxWidth:F1} exceeds work area width {size.X:F1}.");
            const double positionTolerance = 12;
            Assert.IsTrue(window.Top >= origin.Y - positionTolerance
                          && window.Top <= origin.Y + size.Y + positionTolerance,
                $"Window Top {window.Top:F1} should stay within work area bounds ({origin.Y:F1}..{origin.Y + size.Y:F1}).");
            Assert.IsTrue(window.Left >= origin.X - positionTolerance
                          && window.Left <= origin.X + size.X + positionTolerance,
                $"Window Left {window.Left:F1} should stay within work area bounds ({origin.X:F1}..{origin.X + size.X:F1}).");
            window.Close();
        });
    }

    private static void AssertContrast(ResourceDictionary dictionary, string textKey, string backgroundKey, double minRatio)
    {
        var textColor = UiTestHelpers.GetResourceColor(dictionary, textKey);
        var backgroundColor = UiTestHelpers.GetResourceColor(dictionary, backgroundKey);
        var ratio = UiTestHelpers.ContrastRatio(textColor, backgroundColor);
        Assert.IsTrue(ratio >= minRatio,
            $"Contrast ratio for {textKey} on {backgroundKey} is {ratio:F2}, expected >= {minRatio:F1}.");
    }

    private static List<Border> FindMessageBubbles(DependencyObject root)
    {
        var results = new List<Border>();
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var childCount = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(current, i);
                if (child is Border border && Equals(border.Tag, "MessageBubble"))
                {
                    results.Add(border);
                }

                queue.Enqueue(child);
            }
        }

        return results;
    }
}

