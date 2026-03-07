using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SyncForge.Configurator.ViewModels;

namespace SyncForge.Configurator.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private void OnNewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.NewJob();
    }

    private void OnNewJobWizardClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.StartWizard();
    }

    private async void OnOpenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open job.json",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] }
            ]
        });

        var selected = files.FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        await ViewModel.OpenAsync(selected.Path.LocalPath);
    }

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!ViewModel.HasCurrentFile())
        {
            await SaveAsAsync();
            return;
        }

        await ViewModel.SaveAsync(ViewModel.CurrentFilePath!);
    }

    private async void OnSaveAsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SaveAsAsync();
    }

    private void OnValidateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.ValidateCurrentJson();
    }

    private async void OnDryRunClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.RunDryRunAsync();
    }

    private async void OnRunClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.RunAsync();
    }

    private void OnReloadConnectorsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.ReloadConnectors();
    }

    private async void OnLoadSourcePreviewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.LoadSourcePreviewAsync();
    }

    private void OnAddMappingClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.AddMappingRow();
    }

    private void OnRemoveMappingClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.RemoveSelectedMappingRow();
    }

    private void OnWizardBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.WizardPreviousStep();
    }

    private void OnWizardNextClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.WizardNextStep();
    }

    private void OnWizardFinishClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.WizardFinish();
    }

    private void OnWizardCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.CancelWizard();
    }

    private async void OnExportLogsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export logs",
            SuggestedFileName = "syncforge.log",
            FileTypeChoices =
            [
                new FilePickerFileType("Log") { Patterns = ["*.log", "*.txt"] }
            ]
        });

        if (file is null)
        {
            return;
        }

        await ViewModel.ExportFilteredLogsAsync(file.Path.LocalPath);
    }

    private async void OnCopyErrorDetailsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var text = ViewModel.GetErrorDetailsForClipboard();
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is null)
        {
            ViewModel.SetError("Copy failed. Clipboard is not available.");
            return;
        }

        await topLevel.Clipboard.SetTextAsync(text);
        ViewModel.ClearError();
        ViewModel.SetInfo("Error details copied to clipboard.");
    }

    private async Task SaveAsAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save job.json",
            SuggestedFileName = "job.json",
            FileTypeChoices =
            [
                new FilePickerFileType("JSON") { Patterns = ["*.json"] }
            ]
        });

        if (file is null)
        {
            return;
        }

        await ViewModel.SaveAsync(file.Path.LocalPath);
    }
}
