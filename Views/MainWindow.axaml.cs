using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SyncForge.Configurator.ViewModels;



namespace SyncForge.Configurator.Views;

public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnNewClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await EnsureCanDiscardChangesAsync())
        {
            return;
        }

        ViewModel.NewJob();
    }

    private void OnNewJobWizardClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.StartWizard();
    }

    private async void OnOpenClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!await EnsureCanDiscardChangesAsync())
        {
            return;
        }

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

    private void OnNavigateValidationErrorClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.NavigateToSelectedValidationError();
    }

    private async void OnDryRunClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.RunDryRunAsync();
    }

    private async void OnRunClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.RunAsync();
    }

    private async void OnPreflightClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ViewModel.RunPreflightAsync();
    }

    private async void OnExportPreflightClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export preflight",
            SuggestedFileName = "preflight.txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Text") { Patterns = ["*.txt", "*.log"] }
            ]
        });

        if (file is null)
        {
            return;
        }

        await ViewModel.ExportPreflightAsync(file.Path.LocalPath);
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

    private void OnApplyTemplateClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel.ApplySelectedTemplate();
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

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed)
        {
            return;
        }

        if (!ViewModel.HasUnsavedChanges)
        {
            return;
        }

        e.Cancel = true;
        var action = await PromptUnsavedChangesAsync();
        if (action == UnsavedChangesAction.Cancel)
        {
            return;
        }

        if (action == UnsavedChangesAction.Save)
        {
            var saveOk = await SaveWithPromptWhenNeededAsync();
            if (!saveOk)
            {
                return;
            }
        }

        _closeConfirmed = true;
        Close();
    }

    private async Task<bool> EnsureCanDiscardChangesAsync()
    {
        if (!ViewModel.HasUnsavedChanges)
        {
            return true;
        }

        if (ViewModel.SkipUnsavedChangesPromptInSession)
        {
            return true;
        }

        var action = await PromptUnsavedChangesAsync();
        if (action == UnsavedChangesAction.Cancel)
        {
            return false;
        }

        if (action == UnsavedChangesAction.Save)
        {
            return await SaveWithPromptWhenNeededAsync();
        }

        return true;
    }

    private async Task<bool> SaveWithPromptWhenNeededAsync()
    {
        if (ViewModel.HasCurrentFile())
        {
            return await ViewModel.SaveAsync(ViewModel.CurrentFilePath!);
        }

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
            return false;
        }

        return await ViewModel.SaveAsync(file.Path.LocalPath);
    }

    private async Task<UnsavedChangesAction> PromptUnsavedChangesAsync()
    {
        var dialog = new Window
        {
            Title = "Unsaved changes",
            Width = 460,
            Height = 220,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var rememberCheckBox = new CheckBox
        {
            Content = "Nicht erneut fragen (nur diese Session)",
            IsChecked = false
        };

        var saveButton = new Button { Content = "Save", Classes = { "primary" }, MinWidth = 90 };
        var discardButton = new Button { Content = "Discard", Classes = { "secondary" }, MinWidth = 90 };
        var cancelButton = new Button { Content = "Cancel", Classes = { "secondary" }, MinWidth = 90 };

        var tcs = new TaskCompletionSource<UnsavedChangesAction>();

        saveButton.Click += (_, _) => { tcs.TrySetResult(UnsavedChangesAction.Save); dialog.Close(); };
        discardButton.Click += (_, _) => { tcs.TrySetResult(UnsavedChangesAction.Discard); dialog.Close(); };
        cancelButton.Click += (_, _) => { tcs.TrySetResult(UnsavedChangesAction.Cancel); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(UnsavedChangesAction.Cancel);

        dialog.Content = new Border
        {
            Padding = new Thickness(16),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Es gibt ungespeicherte Aenderungen. Was moechtest du tun?" },
                    rememberCheckBox,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Children = { saveButton, discardButton, cancelButton }
                    }
                }
            }
        };

        await dialog.ShowDialog(this);
        var action = await tcs.Task;

        if (rememberCheckBox.IsChecked == true)
        {
            ViewModel.SkipUnsavedChangesPromptInSession = true;
        }

        return action;
    }

    private enum UnsavedChangesAction
    {
        Save,
        Discard,
        Cancel
    }
}
