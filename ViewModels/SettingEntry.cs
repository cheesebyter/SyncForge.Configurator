using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SyncForge.Configurator.ViewModels;

public sealed class SettingEntry : INotifyPropertyChanged
{
    private string _value;

    public SettingEntry(string key, string? value)
    {
        Key = key;
        _value = value ?? string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? ValueChanged;

    public string Key { get; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
