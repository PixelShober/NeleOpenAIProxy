using System.Threading;
using System.Windows;
using NeleDesktop.ViewModels;

namespace NeleDesktop.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += SettingsWindow_Loaded;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Models.Count == 0)
        {
            await _viewModel.LoadModelsAsync(CancellationToken.None);
        }
    }

    private async void LoadModels_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadModelsAsync(CancellationToken.None);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        App.RequestShutdown();
    }
}
