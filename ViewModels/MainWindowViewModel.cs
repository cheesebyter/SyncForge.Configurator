using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SyncForge.Abstractions.Configuration;
using SyncForge.Configurator.Services;

namespace SyncForge.Configurator.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const int MaxLogEntries = 5000;

    private string _jsonContent;
    private string _statusMessage;
    private string? _currentFilePath;
    private string _pluginDirectory;
    private string _connectorMetadata;
    private ConnectorDescriptor? _selectedSourceConnector;
    private ConnectorDescriptor? _selectedTargetConnector;
    private MappingRow? _selectedMappingRow;
    private string _dryRunSummaryJson;
    private string _runSummaryJson;
    private bool _isRunInProgress;
    private string _selectedLogLevel;
    private string _logSearchText;
    private string _jsonPreviewContent;
    private string _jsonPreviewState;
    private string _lastErrorSummary;
    private string _lastErrorDetails;
    private bool _isWizardVisible;
    private int _wizardStep;
    private string _wizardJobName;
    private ConnectorDescriptor? _wizardSourceConnector;
    private ConnectorDescriptor? _wizardTargetConnector;
    private string _wizardStrategyMode;
    private string _wizardSourcePath;
    private string _wizardTargetPath;
    private string _wizardUpsertKeys;
    private string _selectedJobTemplate;
    private bool _isDirty;
    private bool _suppressDirtyTracking;
    private bool _skipUnsavedChangesPromptInSession;
    private string? _selectedValidationError;
    private bool _isConnectorConfigExpanded;
    private bool _isValidationExpanded;
    private bool _isLogsExpanded;
    private bool _isSummaryExpanded;
    private string _preflightState;
    private bool _syncingConnectorSelection;
    private bool _syncingSettings;
    private bool _syncingMappings;
    private bool _syncingFromJsonEditor;
    private readonly Dictionary<string, List<PreflightFinding>> _preflightCache =
        new(StringComparer.OrdinalIgnoreCase);

    public MainWindowViewModel()
    {
        _jsonContent = JobDefinitionJson.Serialize(CreateDefaultJob());
        _statusMessage = "Ready.";
        _pluginDirectory = AppContext.BaseDirectory;
        _connectorMetadata = "No connector selected.";
        _dryRunSummaryJson = "{}";
        _runSummaryJson = "{}";
        _selectedLogLevel = "All";
        _logSearchText = string.Empty;
        _jsonPreviewContent = "{}";
        _jsonPreviewState = "JSON preview ready.";
        _lastErrorSummary = "No error.";
        _lastErrorDetails = string.Empty;
        _wizardJobName = "new-job";
        _wizardStrategyMode = StrategyMode.InsertOnly.ToString();
        _wizardSourcePath = "../data/customers.csv";
        _wizardTargetPath = "../data/output.jsonl";
        _wizardUpsertKeys = "id";
        _selectedJobTemplate = "Custom";
        _isConnectorConfigExpanded = true;
        _isValidationExpanded = true;
        _preflightState = "No preflight executed.";

        ReloadConnectors();
        ReloadMappingsFromCurrentJson();
        RefreshJsonPreviewAndSyncUi();
        MarkClean();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ValidationErrors { get; } = [];

    public ObservableCollection<PreflightFinding> PreflightFindings { get; } = [];

    public ObservableCollection<ConnectorDescriptor> SourceConnectors { get; } = [];

    public ObservableCollection<ConnectorDescriptor> TargetConnectors { get; } = [];

    public ObservableCollection<SettingEntry> SourceSettings { get; } = [];

    public ObservableCollection<SettingEntry> TargetSettings { get; } = [];

    public ObservableCollection<string> SourceColumnsPreview { get; } = [];

    public ObservableCollection<MappingRow> MappingRows { get; } = [];

    public ObservableCollection<string> DryRunLogs { get; } = [];

    public ObservableCollection<string> RunLogs { get; } = [];

    public ObservableCollection<string> LogLevelOptions { get; } =
    [
        "All",
        "INFO",
        "WARN",
        "ERROR"
    ];

    public ObservableCollection<string> FilteredLogs { get; } = [];

    public ObservableCollection<string> StrategyModeOptions { get; } =
    [
        StrategyMode.Replace.ToString(),
        StrategyMode.InsertOnly.ToString(),
        StrategyMode.UpsertByKey.ToString()
    ];

    public ObservableCollection<string> JobTemplateOptions { get; } =
    [
        "Custom",
        "CSV -> MSSQL",
        "REST -> JSONL",
        "Excel -> REST"
    ];

    private List<LogEntry> AllLogs { get; } = [];

    public string JsonContent
    {
        get => _jsonContent;
        set
        {
            if (_jsonContent == value)
            {
                return;
            }

            _jsonContent = value;
            OnPropertyChanged();
            if (!_suppressDirtyTracking)
            {
                MarkDirty();
            }

            RefreshJsonPreviewAndSyncUi();
        }
    }

    public bool HasUnsavedChanges => _isDirty;

    public string UnsavedIndicator => _isDirty ? "*" : string.Empty;

    public bool SkipUnsavedChangesPromptInSession
    {
        get => _skipUnsavedChangesPromptInSession;
        set
        {
            if (_skipUnsavedChangesPromptInSession == value)
            {
                return;
            }

            _skipUnsavedChangesPromptInSession = value;
            OnPropertyChanged();
        }
    }

    public string? SelectedValidationError
    {
        get => _selectedValidationError;
        set
        {
            if (_selectedValidationError == value)
            {
                return;
            }

            _selectedValidationError = value;
            OnPropertyChanged();
        }
    }

    public bool IsConnectorConfigExpanded
    {
        get => _isConnectorConfigExpanded;
        set
        {
            if (_isConnectorConfigExpanded == value)
            {
                return;
            }

            _isConnectorConfigExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsValidationExpanded
    {
        get => _isValidationExpanded;
        set
        {
            if (_isValidationExpanded == value)
            {
                return;
            }

            _isValidationExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsLogsExpanded
    {
        get => _isLogsExpanded;
        set
        {
            if (_isLogsExpanded == value)
            {
                return;
            }

            _isLogsExpanded = value;
            OnPropertyChanged();
        }
    }

    public bool IsSummaryExpanded
    {
        get => _isSummaryExpanded;
        set
        {
            if (_isSummaryExpanded == value)
            {
                return;
            }

            _isSummaryExpanded = value;
            OnPropertyChanged();
        }
    }

    public string PreflightState
    {
        get => _preflightState;
        private set
        {
            if (_preflightState == value)
            {
                return;
            }

            _preflightState = value;
            OnPropertyChanged();
        }
    }

    public string JsonPreviewContent
    {
        get => _jsonPreviewContent;
        private set
        {
            if (_jsonPreviewContent == value)
            {
                return;
            }

            _jsonPreviewContent = value;
            OnPropertyChanged();
        }
    }

    public string JsonPreviewState
    {
        get => _jsonPreviewState;
        private set
        {
            if (_jsonPreviewState == value)
            {
                return;
            }

            _jsonPreviewState = value;
            OnPropertyChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public string PluginDirectory
    {
        get => _pluginDirectory;
        set
        {
            if (_pluginDirectory == value)
            {
                return;
            }

            _pluginDirectory = value;
            OnPropertyChanged();
        }
    }

    public string ConnectorMetadata
    {
        get => _connectorMetadata;
        private set
        {
            if (_connectorMetadata == value)
            {
                return;
            }

            _connectorMetadata = value;
            OnPropertyChanged();
        }
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (_currentFilePath == value)
            {
                return;
            }

            _currentFilePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentFileDisplayName));
        }
    }

    public string CurrentFileDisplayName => string.IsNullOrWhiteSpace(CurrentFilePath)
        ? "(new job.json)"
        : Path.GetFileName(CurrentFilePath);

    public string DryRunSummaryJson
    {
        get => _dryRunSummaryJson;
        private set
        {
            if (_dryRunSummaryJson == value)
            {
                return;
            }

            _dryRunSummaryJson = value;
            OnPropertyChanged();
        }
    }

    public string RunSummaryJson
    {
        get => _runSummaryJson;
        private set
        {
            if (_runSummaryJson == value)
            {
                return;
            }

            _runSummaryJson = value;
            OnPropertyChanged();
        }
    }

    public bool IsRunInProgress
    {
        get => _isRunInProgress;
        private set
        {
            if (_isRunInProgress == value)
            {
                return;
            }

            _isRunInProgress = value;
            OnPropertyChanged();
        }
    }

    public string SelectedLogLevel
    {
        get => _selectedLogLevel;
        set
        {
            if (_selectedLogLevel == value)
            {
                return;
            }

            _selectedLogLevel = value;
            OnPropertyChanged();
            RefreshFilteredLogs();
        }
    }

    public string LogSearchText
    {
        get => _logSearchText;
        set
        {
            if (_logSearchText == value)
            {
                return;
            }

            _logSearchText = value;
            OnPropertyChanged();
            RefreshFilteredLogs();
        }
    }

    public string LastErrorSummary
    {
        get => _lastErrorSummary;
        private set
        {
            if (_lastErrorSummary == value)
            {
                return;
            }

            _lastErrorSummary = value;
            OnPropertyChanged();
        }
    }

    public string LastErrorDetails
    {
        get => _lastErrorDetails;
        private set
        {
            if (_lastErrorDetails == value)
            {
                return;
            }

            _lastErrorDetails = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasErrorDetails));
        }
    }

    public bool HasErrorDetails => !string.IsNullOrWhiteSpace(LastErrorDetails);

    public bool IsWizardVisible
    {
        get => _isWizardVisible;
        private set
        {
            if (_isWizardVisible == value)
            {
                return;
            }

            _isWizardVisible = value;
            OnPropertyChanged();
        }
    }

    public int WizardStep
    {
        get => _wizardStep;
        private set
        {
            if (_wizardStep == value)
            {
                return;
            }

            _wizardStep = Math.Clamp(value, 1, 4);
            OnPropertyChanged();
            OnPropertyChanged(nameof(WizardStepText));
            OnPropertyChanged(nameof(IsWizardStep1));
            OnPropertyChanged(nameof(IsWizardStep2));
            OnPropertyChanged(nameof(IsWizardStep3));
            OnPropertyChanged(nameof(IsWizardStep4));
            OnPropertyChanged(nameof(CanWizardGoBack));
            OnPropertyChanged(nameof(CanWizardGoNext));
            OnPropertyChanged(nameof(CanWizardFinish));
        }
    }

    public string WizardStepText => $"Step {WizardStep}/4";

    public bool IsWizardStep1 => WizardStep == 1;

    public bool IsWizardStep2 => WizardStep == 2;

    public bool IsWizardStep3 => WizardStep == 3;

    public bool IsWizardStep4 => WizardStep == 4;

    public bool CanWizardGoBack => WizardStep > 1;

    public bool CanWizardGoNext => WizardStep < 4;

    public bool CanWizardFinish => WizardStep == 4;

    public string WizardJobName
    {
        get => _wizardJobName;
        set
        {
            if (_wizardJobName == value)
            {
                return;
            }

            _wizardJobName = value;
            OnPropertyChanged();
        }
    }

    public ConnectorDescriptor? WizardSourceConnector
    {
        get => _wizardSourceConnector;
        set
        {
            if (ReferenceEquals(_wizardSourceConnector, value))
            {
                return;
            }

            _wizardSourceConnector = value;
            OnPropertyChanged();
        }
    }

    public ConnectorDescriptor? WizardTargetConnector
    {
        get => _wizardTargetConnector;
        set
        {
            if (ReferenceEquals(_wizardTargetConnector, value))
            {
                return;
            }

            _wizardTargetConnector = value;
            OnPropertyChanged();
        }
    }

    public string WizardStrategyMode
    {
        get => _wizardStrategyMode;
        set
        {
            if (_wizardStrategyMode == value)
            {
                return;
            }

            _wizardStrategyMode = value;
            OnPropertyChanged();
        }
    }

    public string WizardSourcePath
    {
        get => _wizardSourcePath;
        set
        {
            if (_wizardSourcePath == value)
            {
                return;
            }

            _wizardSourcePath = value;
            OnPropertyChanged();
        }
    }

    public string WizardTargetPath
    {
        get => _wizardTargetPath;
        set
        {
            if (_wizardTargetPath == value)
            {
                return;
            }

            _wizardTargetPath = value;
            OnPropertyChanged();
        }
    }

    public string WizardUpsertKeys
    {
        get => _wizardUpsertKeys;
        set
        {
            if (_wizardUpsertKeys == value)
            {
                return;
            }

            _wizardUpsertKeys = value;
            OnPropertyChanged();
        }
    }

    public string SelectedJobTemplate
    {
        get => _selectedJobTemplate;
        set
        {
            if (_selectedJobTemplate == value)
            {
                return;
            }

            _selectedJobTemplate = value;
            OnPropertyChanged();
        }
    }

    public ConnectorDescriptor? SelectedSourceConnector
    {
        get => _selectedSourceConnector;
        set
        {
            if (ReferenceEquals(_selectedSourceConnector, value))
            {
                return;
            }

            _selectedSourceConnector = value;
            OnPropertyChanged();
            UpdateConnectorMetadata();

            if (!_syncingConnectorSelection && value is not null)
            {
                ApplyConnectorSelectionToJson(isSource: true, value);
                ReloadSettingsPanels();
            }
        }
    }

    public ConnectorDescriptor? SelectedTargetConnector
    {
        get => _selectedTargetConnector;
        set
        {
            if (ReferenceEquals(_selectedTargetConnector, value))
            {
                return;
            }

            _selectedTargetConnector = value;
            OnPropertyChanged();
            UpdateConnectorMetadata();

            if (!_syncingConnectorSelection && value is not null)
            {
                ApplyConnectorSelectionToJson(isSource: false, value);
                ReloadSettingsPanels();
            }
        }
    }

    public MappingRow? SelectedMappingRow
    {
        get => _selectedMappingRow;
        set
        {
            if (ReferenceEquals(_selectedMappingRow, value))
            {
                return;
            }

            _selectedMappingRow = value;
            OnPropertyChanged();
        }
    }

    public void NewJob()
    {
        ValidationErrors.Clear();
        ClearError();
        CurrentFilePath = null;
        SetJsonContentWithoutDirty(JobDefinitionJson.Serialize(CreateDefaultJob()));

        SyncSelectionsFromJson();
        ReloadSettingsPanels();
        ReloadMappingsFromCurrentJson();
        SourceColumnsPreview.Clear();
        LoadCachedPreflightForCurrentJob();

        StatusMessage = "New job initialized.";
    }

    public void StartWizard()
    {
        if (SourceConnectors.Count == 0 || TargetConnectors.Count == 0)
        {
            ReloadConnectors();
        }

        WizardJobName = "new-job";
        WizardSourceConnector ??= SourceConnectors.FirstOrDefault();
        WizardTargetConnector ??= TargetConnectors.FirstOrDefault();
        WizardStrategyMode = StrategyMode.InsertOnly.ToString();
        WizardSourcePath = "../data/customers.csv";
        WizardTargetPath = "../data/output.jsonl";
        WizardUpsertKeys = "id";
        SelectedJobTemplate = "Custom";

        WizardStep = 1;
        IsWizardVisible = true;
        ClearError();
        StatusMessage = "Wizard started.";
    }

    public void ApplySelectedTemplate()
    {
        switch (SelectedJobTemplate)
        {
            case "CSV -> MSSQL":
                WizardJobName = "csv-to-mssql";
                WizardStrategyMode = StrategyMode.UpsertByKey.ToString();
                WizardSourcePath = "../data/customers.csv";
                WizardTargetPath = "Server=.;Database=SyncForge;Trusted_Connection=True;";
                WizardUpsertKeys = "id";
                WizardSourceConnector = SourceConnectors.FirstOrDefault(item =>
                    string.Equals(item.ConnectorType, "csv", StringComparison.OrdinalIgnoreCase));
                WizardTargetConnector = TargetConnectors.FirstOrDefault(item =>
                    string.Equals(item.ConnectorType, "mssql", StringComparison.OrdinalIgnoreCase));
                break;

            case "REST -> JSONL":
                WizardJobName = "rest-to-jsonl";
                WizardStrategyMode = StrategyMode.InsertOnly.ToString();
                WizardSourcePath = "https://api.example.com/customers";
                WizardTargetPath = "../data/rest-output.jsonl";
                WizardUpsertKeys = "id";
                WizardSourceConnector = SourceConnectors.FirstOrDefault(item =>
                    string.Equals(item.ConnectorType, "rest", StringComparison.OrdinalIgnoreCase));
                WizardTargetConnector = TargetConnectors.FirstOrDefault(item =>
                    string.Equals(item.ConnectorType, "jsonl", StringComparison.OrdinalIgnoreCase));
                break;

            case "Excel -> REST":
                WizardJobName = "excel-to-rest";
                WizardStrategyMode = StrategyMode.InsertOnly.ToString();
                WizardSourcePath = "../data/customers.xlsx";
                WizardTargetPath = "https://api.example.com/import";
                WizardUpsertKeys = "id";
                WizardSourceConnector = SourceConnectors.FirstOrDefault(item =>
                    string.Equals(item.ConnectorType, "xlsx", StringComparison.OrdinalIgnoreCase));
                WizardTargetConnector = TargetConnectors.FirstOrDefault(item =>
                    string.Equals(item.ConnectorType, "rest", StringComparison.OrdinalIgnoreCase));
                break;

            default:
                WizardJobName = "new-job";
                WizardStrategyMode = StrategyMode.InsertOnly.ToString();
                WizardSourcePath = "../data/customers.csv";
                WizardTargetPath = "../data/output.jsonl";
                WizardUpsertKeys = "id";
                break;
        }

        StatusMessage = $"Wizard template applied: {SelectedJobTemplate}";
    }

    public void CancelWizard()
    {
        IsWizardVisible = false;
        StatusMessage = "Wizard canceled.";
    }

    public void WizardNextStep()
    {
        if (WizardStep < 4)
        {
            WizardStep++;
        }
    }

    public void WizardPreviousStep()
    {
        if (WizardStep > 1)
        {
            WizardStep--;
        }
    }

    public void WizardFinish()
    {
        if (WizardSourceConnector is null || WizardTargetConnector is null)
        {
            SetError("Wizard requires both source and target connector selection.");
            return;
        }

        if (string.IsNullOrWhiteSpace(WizardJobName))
        {
            SetError("Wizard requires a job name.");
            return;
        }

        if (!Enum.TryParse<StrategyMode>(WizardStrategyMode, ignoreCase: true, out var mode))
        {
            SetError("Wizard strategy selection is invalid.");
            return;
        }

        var definition = new JobDefinition
        {
            Name = WizardJobName.Trim(),
            Source = new SourceDefinition
            {
                Type = WizardSourceConnector.ConnectorType,
                Plugin = WizardSourceConnector.AssemblyName,
                Settings = BuildWizardSettings(WizardSourceConnector.ConnectorType, WizardSourcePath, isSource: true)
            },
            Target = new TargetDefinition
            {
                Type = WizardTargetConnector.ConnectorType,
                Plugin = WizardTargetConnector.AssemblyName,
                Settings = BuildWizardSettings(WizardTargetConnector.ConnectorType, WizardTargetPath, isSource: false)
            },
            Mappings =
            [
                new MappingDefinition
                {
                    SourceField = "id",
                    TargetField = "id",
                    IsRequired = true,
                    Transformations = []
                }
            ],
            Strategy = new StrategyDefinition
            {
                Mode = mode,
                KeyFields = mode == StrategyMode.UpsertByKey
                    ? ParseWizardKeyFields(WizardUpsertKeys)
                    : []
            }
        };

        JsonContent = JobDefinitionJson.Serialize(definition);
        MarkClean();
        ValidationErrors.Clear();
        ClearError();
        CurrentFilePath = null;
        IsWizardVisible = false;

        SyncSelectionsFromJson();
        ReloadSettingsPanels();
        ReloadMappingsFromCurrentJson();
        LoadCachedPreflightForCurrentJob();

        StatusMessage = "Wizard generated a new job JSON.";
    }

    public async Task<bool> OpenAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            _ = JobDefinitionJson.Deserialize(json);

            SetJsonContentWithoutDirty(json);
            CurrentFilePath = filePath;
            ValidationErrors.Clear();
            ClearError();

            SyncSelectionsFromJson();
            ReloadSettingsPanels();
            ReloadMappingsFromCurrentJson();
            SourceColumnsPreview.Clear();
            LoadCachedPreflightForCurrentJob();

            StatusMessage = $"Loaded: {filePath}";
            return true;
        }
        catch (InvalidOperationException ex)
        {
            SetError($"Invalid JSON: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            SetError($"Open failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SaveAsync(string filePath)
    {
        try
        {
            var parsed = JobDefinitionJson.Deserialize(JsonContent);
            var normalizedJson = JobDefinitionJson.Serialize(parsed);
            await File.WriteAllTextAsync(filePath, normalizedJson);

            SetJsonContentWithoutDirty(normalizedJson);
            CurrentFilePath = filePath;
            ClearError();

            SyncSelectionsFromJson();
            ReloadSettingsPanels();
            ReloadMappingsFromCurrentJson();
            LoadCachedPreflightForCurrentJob();

            StatusMessage = $"Saved: {filePath}";
            return true;
        }
        catch (InvalidOperationException ex)
        {
            SetError($"Cannot save invalid JSON: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            SetError($"Save failed: {ex.Message}");
            return false;
        }
    }

    public bool ValidateCurrentJson()
    {
        ValidationErrors.Clear();
        SelectedValidationError = null;

        JobDefinition definition;
        try
        {
            definition = JobDefinitionJson.Deserialize(JsonContent);
        }
        catch (InvalidOperationException ex)
        {
            ValidationErrors.Add($"Invalid JSON: {ex.Message}");
            SetError(
                "Validation failed. Please fix invalid JSON.",
                $"Operation: Validate{Environment.NewLine}Reason: {ex.Message}");
            StatusMessage = "Validation failed.";
            return false;
        }

        var results = JobDefinitionValidator.Validate(definition);
        foreach (var validationResult in results)
        {
            var members = validationResult.MemberNames.Any()
                ? string.Join(", ", validationResult.MemberNames)
                : "(unknown)";
            ValidationErrors.Add($"{members}: {validationResult.ErrorMessage}");
        }

        foreach (var row in MappingRows)
        {
            if (row.IsRequired && string.IsNullOrWhiteSpace(row.TargetField))
            {
                ValidationErrors.Add($"Mapping target field is required for source '{row.SourceField}'.");
            }

            if (row.IsRequired && string.IsNullOrWhiteSpace(row.SourceField))
            {
                ValidationErrors.Add("Mapping source field is required for required rows.");
            }
        }

        if (ValidationErrors.Count == 0)
        {
            ClearError();
            StatusMessage = "Validation successful.";
            return true;
        }

        SetError(
            "Validation failed. Please review highlighted issues.",
            BuildValidationErrorDetails());
        StatusMessage = "Validation failed.";
        return false;
    }

    public void NavigateToSelectedValidationError()
    {
        NavigateToValidationError(SelectedValidationError);
    }

    public void NavigateToValidationError(string? validationError)
    {
        if (string.IsNullOrWhiteSpace(validationError))
        {
            return;
        }

        IsValidationExpanded = true;

        if (validationError.Contains("Mappings[", StringComparison.OrdinalIgnoreCase)
            || validationError.Contains("Mapping", StringComparison.OrdinalIgnoreCase))
        {
            IsConnectorConfigExpanded = true;
            var idx = TryExtractMappingIndex(validationError);
            if (idx is >= 0 && idx < MappingRows.Count)
            {
                SelectedMappingRow = MappingRows[idx.Value];
            }

            StatusMessage = "Navigated to mapping section for selected validation issue.";
            return;
        }

        if (validationError.Contains("Source", StringComparison.OrdinalIgnoreCase)
            || validationError.Contains("Target", StringComparison.OrdinalIgnoreCase)
            || validationError.Contains("settings", StringComparison.OrdinalIgnoreCase))
        {
            IsConnectorConfigExpanded = true;
            StatusMessage = "Navigated to connector settings section for selected validation issue.";
            return;
        }

        StatusMessage = "Validation issue selected. Please review highlighted section.";
    }

    public async Task RunPreflightAsync()
    {
        PreflightFindings.Clear();

        JobDefinition definition;
        try
        {
            definition = JobDefinitionJson.Deserialize(JsonContent);
        }
        catch (InvalidOperationException ex)
        {
            PreflightFindings.Add(new PreflightFinding
            {
                Severity = "ERROR",
                Scope = "Config",
                Message = "Cannot run preflight on invalid JSON: " + ex.Message
            });
            PreflightState = "Preflight failed.";
            return;
        }

        var validationErrors = JobDefinitionValidator.Validate(definition);
        foreach (var validationError in validationErrors)
        {
            var members = validationError.MemberNames.Any()
                ? string.Join(", ", validationError.MemberNames)
                : "(unknown)";

            PreflightFindings.Add(new PreflightFinding
            {
                Severity = "ERROR",
                Scope = "Validation",
                Message = $"{members}: {validationError.ErrorMessage}"
            });
        }

        var findings = await PreflightService.RunAsync(
            definition,
            CurrentFilePath,
            PluginDirectory,
            SourceConnectors.ToList(),
            TargetConnectors.ToList(),
            CancellationToken.None);

        foreach (var finding in findings)
        {
            PreflightFindings.Add(finding);
        }

        _preflightCache[definition.Name] = PreflightFindings.ToList();

        var errors = PreflightFindings.Count(item => string.Equals(item.Severity, "ERROR", StringComparison.OrdinalIgnoreCase));
        var warnings = PreflightFindings.Count(item => string.Equals(item.Severity, "WARN", StringComparison.OrdinalIgnoreCase));
        PreflightState = $"Preflight completed. Errors={errors}, Warnings={warnings}, Findings={PreflightFindings.Count}.";
        StatusMessage = PreflightState;
    }

    public async Task<bool> ExportPreflightAsync(string outputPath)
    {
        try
        {
            var lines = PreflightFindings.Select(item => item.Rendered).ToArray();
            await File.WriteAllLinesAsync(outputPath, lines);
            StatusMessage = $"Preflight exported: {outputPath}";
            return true;
        }
        catch (Exception ex)
        {
            SetError("Preflight export failed: " + ex.Message);
            return false;
        }
    }

    public async Task LoadSourcePreviewAsync()
    {
        try
        {
            var definition = JobDefinitionJson.Deserialize(JsonContent);
            var columns = await SourcePreviewService.LoadColumnsAsync(definition, CurrentFilePath);

            SourceColumnsPreview.Clear();
            foreach (var column in columns)
            {
                SourceColumnsPreview.Add(column);
            }

            ClearError();
            StatusMessage = $"Source preview loaded: {SourceColumnsPreview.Count} columns.";
        }
        catch (Exception ex)
        {
            SetError($"Source preview failed: {ex.Message}");
        }
    }

    public void AddMappingRow()
    {
        var sourceField = SourceColumnsPreview.FirstOrDefault() ?? string.Empty;
        var row = new MappingRow(sourceField, string.Empty, isRequired: false);
        row.RowChanged += OnMappingRowChanged;
        MappingRows.Add(row);
        SelectedMappingRow = row;

        ApplyMappingsToJson();
        StatusMessage = "Mapping row added.";
    }

    public void RemoveSelectedMappingRow()
    {
        if (SelectedMappingRow is null)
        {
            return;
        }

        SelectedMappingRow.RowChanged -= OnMappingRowChanged;
        MappingRows.Remove(SelectedMappingRow);
        SelectedMappingRow = null;

        ApplyMappingsToJson();
        StatusMessage = "Mapping row removed.";
    }

    public void SetError(string message)
    {
        SetError(message, details: null);
    }

    public void SetError(string message, string? details)
    {
        var sanitized = SanitizeUserMessage(message);
        LastErrorSummary = sanitized;
        LastErrorDetails = BuildErrorDetailsPayload(sanitized, details);
        StatusMessage = sanitized;
    }

    public void ClearError()
    {
        LastErrorSummary = "No error.";
        LastErrorDetails = string.Empty;
    }

    public string GetErrorDetailsForClipboard()
    {
        return HasErrorDetails
            ? LastErrorDetails
            : $"Timestamp: {DateTime.Now:O}{Environment.NewLine}Status: No error details available.";
    }

    public void SetInfo(string message)
    {
        StatusMessage = SanitizeUserMessage(message);
    }

    public async Task RunDryRunAsync()
    {
        DryRunLogs.Clear();
        DryRunSummaryJson = "{}";

        if (!ValidateCurrentJson())
        {
            return;
        }

        try
        {
            var definition = JobDefinitionJson.Deserialize(JsonContent);
            AddDryRunLog("INFO", $"Starting dry-run for job '{definition.Name}'.");

            var result = await DryRunExecutionService.ExecuteAsync(
                definition,
                CurrentFilePath,
                PluginDirectory,
                dryRun: true,
                AddDryRunLog,
                CancellationToken.None);

            DryRunSummaryJson = result.SummaryJson;
            ClearError();
            StatusMessage = "Dry-run completed successfully.";
        }
        catch (Exception ex)
        {
            AddDryRunLog("ERROR", "Dry-run failed: " + ex.Message);
            SetError("Dry-run failed: " + ex.Message);
        }
    }

    public async Task RunAsync()
    {
        RunLogs.Clear();
        RunSummaryJson = "{}";

        if (!ValidateCurrentJson())
        {
            return;
        }

        IsRunInProgress = true;
        StatusMessage = "Run in progress...";

        try
        {
            var definition = JobDefinitionJson.Deserialize(JsonContent);
            AddRunLog("INFO", $"Starting run for job '{definition.Name}'.");

            var result = await DryRunExecutionService.ExecuteAsync(
                definition,
                CurrentFilePath,
                PluginDirectory,
                dryRun: false,
                AddRunLog,
                CancellationToken.None);

            RunSummaryJson = result.SummaryJson;
            ClearError();
            StatusMessage = "Run completed successfully.";
        }
        catch (Exception ex)
        {
            AddRunLog("ERROR", "Run failed: " + ex.Message);
            SetError("Run failed: " + ex.Message);
        }
        finally
        {
            IsRunInProgress = false;
        }
    }

    public void ReloadConnectors()
    {
        var discovered = ConnectorDiscoveryService.Discover(PluginDirectory);

        SourceConnectors.Clear();
        TargetConnectors.Clear();

        foreach (var descriptor in discovered)
        {
            if (string.Equals(descriptor.Kind, "Source", StringComparison.OrdinalIgnoreCase))
            {
                SourceConnectors.Add(descriptor);
            }
            else if (string.Equals(descriptor.Kind, "Target", StringComparison.OrdinalIgnoreCase))
            {
                TargetConnectors.Add(descriptor);
            }
        }

        SyncSelectionsFromJson();
        ReloadSettingsPanels();
        WizardSourceConnector ??= SourceConnectors.FirstOrDefault();
        WizardTargetConnector ??= TargetConnectors.FirstOrDefault();
        StatusMessage = $"Discovered connectors: source={SourceConnectors.Count}, target={TargetConnectors.Count}";
    }

    public bool HasCurrentFile()
    {
        return !string.IsNullOrWhiteSpace(CurrentFilePath);
    }

    public void MarkClean()
    {
        if (!_isDirty)
        {
            return;
        }

        _isDirty = false;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedIndicator));
    }

    public void MarkDirty()
    {
        if (_isDirty)
        {
            return;
        }

        _isDirty = true;
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedIndicator));
    }

    private void SyncSelectionsFromJson()
    {
        try
        {
            var definition = JobDefinitionJson.Deserialize(JsonContent);
            var source = FindConnector(SourceConnectors, definition.Source.Type, definition.Source.Plugin);
            var target = FindConnector(TargetConnectors, definition.Target.Type, definition.Target.Plugin);

            _syncingConnectorSelection = true;
            SelectedSourceConnector = source;
            SelectedTargetConnector = target;
        }
        catch
        {
            _syncingConnectorSelection = true;
            SelectedSourceConnector = null;
            SelectedTargetConnector = null;
        }
        finally
        {
            _syncingConnectorSelection = false;
        }
    }

    private static ConnectorDescriptor? FindConnector(
        IEnumerable<ConnectorDescriptor> candidates,
        string configuredType,
        string configuredPlugin)
    {
        var byPluginAndType = candidates.FirstOrDefault(item =>
            string.Equals(item.ConnectorType, configuredType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.AssemblyName, configuredPlugin, StringComparison.OrdinalIgnoreCase));

        if (byPluginAndType is not null)
        {
            return byPluginAndType;
        }

        return candidates.FirstOrDefault(item =>
            string.Equals(item.ConnectorType, configuredType, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyConnectorSelectionToJson(bool isSource, ConnectorDescriptor descriptor)
    {
        try
        {
            var rootNode = JsonNode.Parse(JsonContent) as JsonObject
                ?? throw new InvalidOperationException("Current JSON is not an object.");

            var sectionName = isSource ? "source" : "target";
            var section = rootNode[sectionName] as JsonObject ?? new JsonObject();
            section["type"] = descriptor.ConnectorType;
            section["plugin"] = descriptor.AssemblyName;
            rootNode[sectionName] = section;

            JsonContent = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            StatusMessage = $"Updated {sectionName} connector to {descriptor.DisplayName}.";
        }
        catch (Exception ex)
        {
            SetError($"Connector selection could not be applied: {ex.Message}");
        }
    }

    private void UpdateConnectorMetadata()
    {
        var lines = new List<string>
        {
            $"Source connectors: {SourceConnectors.Count}",
            $"Target connectors: {TargetConnectors.Count}"
        };

        if (SelectedSourceConnector is not null)
        {
            lines.Add($"Selected Source: {SelectedSourceConnector.DisplayName}");
            lines.Add($"  Class: {SelectedSourceConnector.ClassName}");
        }

        if (SelectedTargetConnector is not null)
        {
            lines.Add($"Selected Target: {SelectedTargetConnector.DisplayName}");
            lines.Add($"  Class: {SelectedTargetConnector.ClassName}");
        }

        if (SelectedSourceConnector is null && SelectedTargetConnector is null)
        {
            lines.Add("No connector selected.");
        }

        ConnectorMetadata = string.Join(Environment.NewLine, lines);
    }

    private static JobDefinition CreateDefaultJob()
    {
        return new JobDefinition
        {
            Name = "new-job",
            Source = new SourceDefinition
            {
                Type = "csv",
                Plugin = "SyncForge.Plugin.Csv",
                Settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["path"] = "../data/customers.csv",
                    ["delimiter"] = ";"
                }
            },
            Target = new TargetDefinition
            {
                Type = "jsonl",
                Plugin = "builtin",
                Settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["path"] = "../data/output.jsonl"
                }
            },
            Mappings =
            [
                new MappingDefinition
                {
                    SourceField = "id",
                    TargetField = "id",
                    IsRequired = true,
                    Transformations = []
                }
            ],
            Strategy = new StrategyDefinition
            {
                Mode = StrategyMode.InsertOnly,
                KeyFields = []
            }
        };
    }

    private void ReloadSettingsPanels()
    {
        JobDefinition definition;
        try
        {
            definition = JobDefinitionJson.Deserialize(JsonContent);
        }
        catch
        {
            SourceSettings.Clear();
            TargetSettings.Clear();
            return;
        }

        var sourceKeys = ConnectorConfigSchemaService.GetSuggestedKeys(definition.Source.Type, "source");
        var targetKeys = ConnectorConfigSchemaService.GetSuggestedKeys(definition.Target.Type, "target");

        var sourceEntries = BuildEntries(sourceKeys, definition.Source.Settings);
        var targetEntries = BuildEntries(targetKeys, definition.Target.Settings);

        _syncingSettings = true;
        ReplaceSettings(SourceSettings, sourceEntries, isSource: true);
        ReplaceSettings(TargetSettings, targetEntries, isSource: false);
        _syncingSettings = false;
    }

    private static List<SettingEntry> BuildEntries(
        IReadOnlyList<string> suggestedKeys,
        IReadOnlyDictionary<string, string?> existing)
    {
        var entries = new List<SettingEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in suggestedKeys)
        {
            existing.TryGetValue(key, out var value);
            entries.Add(new SettingEntry(key, value));
            seen.Add(key);
        }

        foreach (var pair in existing.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (seen.Contains(pair.Key))
            {
                continue;
            }

            entries.Add(new SettingEntry(pair.Key, pair.Value));
        }

        return entries;
    }

    private void ReplaceSettings(
        ObservableCollection<SettingEntry> targetCollection,
        IEnumerable<SettingEntry> entries,
        bool isSource)
    {
        foreach (var existing in targetCollection)
        {
            existing.ValueChanged -= isSource ? OnSourceSettingChanged : OnTargetSettingChanged;
        }

        targetCollection.Clear();
        foreach (var entry in entries)
        {
            entry.ValueChanged += isSource ? OnSourceSettingChanged : OnTargetSettingChanged;
            targetCollection.Add(entry);
        }
    }

    private void OnSourceSettingChanged(object? sender, EventArgs e)
    {
        ApplySettingsToJson(isSource: true, SourceSettings);
    }

    private void OnTargetSettingChanged(object? sender, EventArgs e)
    {
        ApplySettingsToJson(isSource: false, TargetSettings);
    }

    private void ApplySettingsToJson(bool isSource, IEnumerable<SettingEntry> settings)
    {
        if (_syncingSettings)
        {
            return;
        }

        try
        {
            var rootNode = JsonNode.Parse(JsonContent) as JsonObject
                ?? throw new InvalidOperationException("Current JSON is not an object.");

            var sectionName = isSource ? "source" : "target";
            var section = rootNode[sectionName] as JsonObject ?? new JsonObject();
            var settingsObject = new JsonObject();

            foreach (var entry in settings)
            {
                settingsObject[entry.Key] = entry.Value;
            }

            section["settings"] = settingsObject;
            rootNode[sectionName] = section;

            _syncingSettings = true;
            JsonContent = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            SetError($"Settings could not be applied: {ex.Message}");
        }
        finally
        {
            _syncingSettings = false;
        }
    }

    private void ReloadMappingsFromCurrentJson()
    {
        JobDefinition definition;
        try
        {
            definition = JobDefinitionJson.Deserialize(JsonContent);
        }
        catch
        {
            MappingRows.Clear();
            return;
        }

        _syncingMappings = true;
        foreach (var row in MappingRows)
        {
            row.RowChanged -= OnMappingRowChanged;
        }

        MappingRows.Clear();
        foreach (var mapping in definition.Mappings)
        {
            var row = new MappingRow(mapping.SourceField, mapping.TargetField, mapping.IsRequired);
            row.RowChanged += OnMappingRowChanged;
            MappingRows.Add(row);
        }

        _syncingMappings = false;
    }

    private void OnMappingRowChanged(object? sender, EventArgs e)
    {
        ApplyMappingsToJson();
    }

    private void ApplyMappingsToJson()
    {
        if (_syncingMappings)
        {
            return;
        }

        try
        {
            var rootNode = JsonNode.Parse(JsonContent) as JsonObject
                ?? throw new InvalidOperationException("Current JSON is not an object.");

            var mappingsArray = new JsonArray();
            foreach (var row in MappingRows)
            {
                var mappingObject = new JsonObject
                {
                    ["sourceField"] = row.SourceField,
                    ["targetField"] = row.TargetField,
                    ["isRequired"] = row.IsRequired,
                    ["transformations"] = new JsonArray()
                };

                mappingsArray.Add(mappingObject);
            }

            rootNode["mappings"] = mappingsArray;

            _syncingMappings = true;
            JsonContent = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            SetError($"Mappings could not be applied: {ex.Message}");
        }
        finally
        {
            _syncingMappings = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void AddDryRunLog(string line)
    {
        var (level, message) = ParseLogLine(line);
        AddDryRunLog(level, message);
    }

    private void AddDryRunLog(string level, string message)
    {
        AddLog(level, "dry-run", message, DryRunLogs);
    }

    private void AddRunLog(string line)
    {
        var (level, message) = ParseLogLine(line);
        AddRunLog(level, message);
    }

    private void AddRunLog(string level, string message)
    {
        AddLog(level, "run", message, RunLogs);
    }

    private void AddLog(string level, string source, string message, ObservableCollection<string> sink)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Source = source,
            Message = message
        };

        var rendered = entry.Rendered;
        sink.Add(rendered);

        AllLogs.Add(entry);
        if (AllLogs.Count > MaxLogEntries)
        {
            AllLogs.RemoveAt(0);
        }

        var search = LogSearchText.Trim();
        var levelMatches = string.Equals(SelectedLogLevel, "All", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.Level, SelectedLogLevel, StringComparison.OrdinalIgnoreCase);
        var searchMatches = string.IsNullOrWhiteSpace(search)
            || rendered.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase);

        if (levelMatches && searchMatches)
        {
            FilteredLogs.Add(rendered);
        }

        if (FilteredLogs.Count > MaxLogEntries)
        {
            FilteredLogs.RemoveAt(0);
        }
    }

    private (string Level, string Message) ParseLogLine(string line)
    {
        if (line.StartsWith("INFO  ", StringComparison.Ordinal))
        {
            return ("INFO", line[6..]);
        }

        if (line.StartsWith("WARN  ", StringComparison.Ordinal))
        {
            return ("WARN", line[6..]);
        }

        if (line.StartsWith("ERROR ", StringComparison.Ordinal))
        {
            return ("ERROR", line[6..]);
        }

        return ("INFO", line);
    }

    private void RefreshFilteredLogs()
    {
        var level = SelectedLogLevel;
        var search = LogSearchText.Trim();

        FilteredLogs.Clear();
        foreach (var entry in AllLogs)
        {
            var rendered = entry.Rendered;
            var levelMatches = string.Equals(level, "All", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Level, level, StringComparison.OrdinalIgnoreCase);
            if (!levelMatches)
            {
                continue;
            }

            var searchMatches = string.IsNullOrWhiteSpace(search)
                || rendered.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase);
            if (!searchMatches)
            {
                continue;
            }

            FilteredLogs.Add(rendered);
        }
    }

    public async Task<bool> ExportFilteredLogsAsync(string outputPath)
    {
        try
        {
            var lines = FilteredLogs.Count > 0
                ? FilteredLogs.ToArray()
                : AllLogs.Select(entry => entry.Rendered).ToArray();

            await File.WriteAllLinesAsync(outputPath, lines);
            StatusMessage = $"Logs exported: {outputPath}";
            return true;
        }
        catch (Exception ex)
        {
            SetError("Log export failed: " + ex.Message);
            return false;
        }
    }

    private static Dictionary<string, string?> BuildWizardSettings(string connectorType, string path, bool isSource)
    {
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (string.Equals(connectorType, "csv", StringComparison.OrdinalIgnoreCase))
        {
            settings["path"] = string.IsNullOrWhiteSpace(path) ? "../data/customers.csv" : path.Trim();
            settings["delimiter"] = ";";
            settings["encoding"] = "utf-8";
            return settings;
        }

        if (string.Equals(connectorType, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            settings["path"] = string.IsNullOrWhiteSpace(path) ? "../data/customers.xlsx" : path.Trim();
            return settings;
        }

        if (string.Equals(connectorType, "jsonl", StringComparison.OrdinalIgnoreCase))
        {
            settings["path"] = string.IsNullOrWhiteSpace(path) ? "../data/output.jsonl" : path.Trim();
            return settings;
        }

        if (string.Equals(connectorType, "rest", StringComparison.OrdinalIgnoreCase))
        {
            settings["url"] = string.IsNullOrWhiteSpace(path) ? "https://api.example.com" : path.Trim();
            if (isSource)
            {
                settings["jsonPath"] = "$.items";
            }
            else
            {
                settings["mode"] = "record";
            }

            settings["timeoutSeconds"] = "30";
            return settings;
        }

        if (string.Equals(connectorType, "mssql", StringComparison.OrdinalIgnoreCase))
        {
            settings["connectionString"] = string.IsNullOrWhiteSpace(path)
                ? "Server=.;Database=SyncForge;Trusted_Connection=True;"
                : path.Trim();
            settings["table"] = "dbo.Customers";
            settings["batchSize"] = "500";
            settings["commandTimeoutSeconds"] = "30";
            return settings;
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            settings["path"] = path.Trim();
        }

        return settings;
    }

    private static List<string> ParseWizardKeyFields(string raw)
    {
        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string BuildValidationErrorDetails()
    {
        var lines = new List<string>
        {
            $"Timestamp: {DateTime.Now:O}",
            "Operation: Validate",
            $"CurrentFile: {CurrentFilePath ?? "(unsaved)"}",
            "ValidationErrors:"
        };

        if (ValidationErrors.Count == 0)
        {
            lines.Add("- none");
        }
        else
        {
            foreach (var error in ValidationErrors)
            {
                lines.Add("- " + error);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildErrorDetailsPayload(string summary, string? details)
    {
        var lines = new List<string>
        {
            $"Timestamp: {DateTime.Now:O}",
            $"Summary: {summary}",
            $"CurrentFile: {CurrentFilePath ?? "(unsaved)"}",
            $"JsonPreviewState: {JsonPreviewState}"
        };

        if (!string.IsNullOrWhiteSpace(details))
        {
            lines.Add("Details:");
            lines.AddRange(details.Split(Environment.NewLine));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string SanitizeUserMessage(string message)
    {
        var singleLine = message
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(singleLine))
        {
            return "An unexpected error occurred.";
        }

        // Keep user-facing message compact and avoid noisy call-site fragments.
        var atIndex = singleLine.IndexOf(" at ", StringComparison.Ordinal);
        if (atIndex > 0)
        {
            singleLine = singleLine[..atIndex].Trim();
        }

        return singleLine;
    }

    private void RefreshJsonPreviewAndSyncUi()
    {
        try
        {
            var rootNode = JsonNode.Parse(JsonContent)
                ?? throw new InvalidOperationException("JSON document is empty.");

            JsonPreviewContent = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            JsonPreviewState = "JSON valid. Live preview synced.";

            if (_syncingFromJsonEditor || _syncingSettings || _syncingMappings || _syncingConnectorSelection)
            {
                return;
            }

            _syncingFromJsonEditor = true;
            SyncSelectionsFromJson();
            ReloadSettingsPanels();
            ReloadMappingsFromCurrentJson();
            LoadCachedPreflightForCurrentJob();
        }
        catch (Exception ex)
        {
            JsonPreviewContent = JsonContent;
            JsonPreviewState = "JSON invalid: " + ex.Message;
        }
        finally
        {
            _syncingFromJsonEditor = false;
        }
    }

    private void SetJsonContentWithoutDirty(string json)
    {
        _suppressDirtyTracking = true;
        JsonContent = json;
        _suppressDirtyTracking = false;
        MarkClean();
    }

    private void LoadCachedPreflightForCurrentJob()
    {
        try
        {
            var definition = JobDefinitionJson.Deserialize(JsonContent);
            if (_preflightCache.TryGetValue(definition.Name, out var cached))
            {
                PreflightFindings.Clear();
                foreach (var item in cached)
                {
                    PreflightFindings.Add(item);
                }

                PreflightState = $"Loaded cached preflight for job '{definition.Name}'.";
            }
        }
        catch
        {
            // Ignore cache loading for invalid or partial JSON edits.
        }
    }

    private static int? TryExtractMappingIndex(string text)
    {
        var match = Regex.Match(text, @"Mappings\[(\d+)\]", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(match.Groups[1].Value, out var idx) ? idx : null;
    }
}
