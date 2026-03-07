using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyncForge.Configurator.ViewModels;

public sealed class MappingRow : INotifyPropertyChanged
{
    private string _sourceField;
    private string _targetField;
    private bool _isRequired;

    public MappingRow(string sourceField, string targetField, bool isRequired)
    {
        _sourceField = sourceField;
        _targetField = targetField;
        _isRequired = isRequired;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RowChanged;

    public string SourceField
    {
        get => _sourceField;
        set
        {
            if (_sourceField == value)
            {
                return;
            }

            _sourceField = value;
            OnPropertyChanged();
            RowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string TargetField
    {
        get => _targetField;
        set
        {
            if (_targetField == value)
            {
                return;
            }

            _targetField = value;
            OnPropertyChanged();
            RowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsRequired
    {
        get => _isRequired;
        set
        {
            if (_isRequired == value)
            {
                return;
            }

            _isRequired = value;
            OnPropertyChanged();
            RowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
