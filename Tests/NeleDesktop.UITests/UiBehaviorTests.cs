using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
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
            var messageText = UiTestHelpers.FindVisualChild<TextBox>(content, tb => tb.TextWrapping == TextWrapping.Wrap);
            Assert.IsNotNull(messageText, "Message TextBox not found.");
            Assert.IsTrue(messageText.IsReadOnly, "Message text is not read-only.");
            Assert.AreEqual(new Thickness(0), messageText.BorderThickness, "Message text box should not draw a border.");
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

            var comboBox = window.Content is DependencyObject rootElement
                ? UiTestHelpers.FindVisualChild<ComboBox>(rootElement)
                : null;
            Assert.IsNotNull(comboBox, "Model ComboBox not found.");
            var parentStack = UiTestHelpers.FindVisualParent<StackPanel>(comboBox);
            Assert.IsNotNull(parentStack, "Model ComboBox is not inside a StackPanel.");
            Assert.IsTrue(parentStack.Children.Count >= 2, "Header stack does not contain expected children.");
            Assert.IsInstanceOfType(parentStack.Children[0], typeof(TextBlock), "Title text is not above the model dropdown.");
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

    private static void AssertContrast(ResourceDictionary dictionary, string textKey, string backgroundKey, double minRatio)
    {
        var textColor = UiTestHelpers.GetResourceColor(dictionary, textKey);
        var backgroundColor = UiTestHelpers.GetResourceColor(dictionary, backgroundKey);
        var ratio = UiTestHelpers.ContrastRatio(textColor, backgroundColor);
        Assert.IsTrue(ratio >= minRatio,
            $"Contrast ratio for {textKey} on {backgroundKey} is {ratio:F2}, expected >= {minRatio:F1}.");
    }
}
