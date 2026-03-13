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
        Placeholder = BuildPlaceholder(key);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? ValueChanged;

    public string Key { get; }

    public string Placeholder { get; }

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

    private static string BuildPlaceholder(string key)
    {
        return key.ToLowerInvariant() switch
        {
            "path" => @"z.B. J:\\SyncForge\\examples\\CH_Adressen.csv",
            "delimiter" => "z.B. ; oder ,",
            "encoding" => "z.B. utf-8",
            "hasheader" => "true oder false",
            "quote" => "z.B. \"",
            "escape" => "z.B. \\",
            "sheetname" => "z.B. Sheet1",
            "url" => "z.B. https://api.example.com/items",
            "connectionstring" => "z.B. Server=.;Database=SyncForge;Trusted_Connection=True;",
            "table" => "z.B. dbo.Customers",
            _ => "Wert eingeben..."
        };
    }
}
