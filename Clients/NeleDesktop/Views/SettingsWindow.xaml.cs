using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NeleDesktop.ViewModels;

namespace NeleDesktop.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private CancellationTokenSource? _apiKeyLoadCts;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += SettingsWindow_Loaded;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.ApiKey) && _viewModel.Models.Count == 0)
        {
            await _viewModel.LoadModelsAsync(CancellationToken.None);
        }
    }

    private async void ApiKey_TextChanged(object sender, TextChangedEventArgs e)
    {
        _apiKeyLoadCts?.Cancel();

        if (string.IsNullOrWhiteSpace(_viewModel.ApiKey))
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _apiKeyLoadCts = cts;

        try
        {
            await Task.Delay(600, cts.Token);
            await _viewModel.LoadModelsAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}
