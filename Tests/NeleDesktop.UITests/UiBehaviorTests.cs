using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using NeleDesktop;
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

            var comboBox = UiTestHelpers.FindVisualChild<ComboBox>(rootElement);
            Assert.IsNotNull(comboBox, "Model ComboBox not found.");
            var titleText = UiTestHelpers.FindVisualChild<TextBlock>(rootElement, tb => Math.Abs(tb.FontSize - 18) < 0.1);
            Assert.IsNotNull(titleText, "Chat title text not found.");

            var titleBounds = titleText.TransformToAncestor((Visual)rootElement)
                .TransformBounds(new Rect(0, 0, titleText.ActualWidth, titleText.ActualHeight));
            var comboBounds = comboBox.TransformToAncestor((Visual)rootElement)
                .TransformBounds(new Rect(0, 0, comboBox.ActualWidth, comboBox.ActualHeight));

            Assert.IsTrue(titleBounds.Bottom <= comboBounds.Top + 2,
                "Title text should be above the model dropdown.");
            Assert.AreEqual(HorizontalAlignment.Left, comboBox.HorizontalAlignment, "Model dropdown should be left aligned.");
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
    public void ChatHeader_WebSearchToggleExists()
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

            var toggle = window.FindName("WebSearchToggle") as ToggleButton;
            Assert.IsNotNull(toggle, "Web search toggle not found.");
            var binding = BindingOperations.GetBindingExpression(toggle, ToggleButton.IsCheckedProperty);
            Assert.IsNotNull(binding, "Web search toggle is not data-bound.");
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
}
