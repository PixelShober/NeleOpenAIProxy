using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using NeleDesktop.ViewModels;

namespace NeleDesktop.Views;

public partial class MoveToFolderDialog : Window
{
    public MoveToFolderDialog(ObservableCollection<ChatFolderViewModel> folders)
    {
        InitializeComponent();

        Options = new ObservableCollection<FolderOption>
        {
            new FolderOption(null, "No folder")
        };

        foreach (var folder in folders)
        {
            Options.Add(new FolderOption(folder.Id, folder.Name));
        }

        SelectedOption = Options.FirstOrDefault();
        DataContext = this;
    }

    public ObservableCollection<FolderOption> Options { get; }

    public FolderOption? SelectedOption { get; set; }

    public string? SelectedFolderId => SelectedOption?.Id;

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && GetAncestor<WpfButton>(source) is not null)
        {
            return;
        }

        DragMove();
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

    public sealed class FolderOption
    {
        public FolderOption(string? id, string name)
        {
            Id = id;
            Name = name;
        }

        public string? Id { get; }
        public string Name { get; }
    }
}
