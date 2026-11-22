using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Hora.Core.Localization;

namespace Hora.UI.Platform;

/// <summary>
/// Bridges the translator into XAML. Bind a control to <see cref="Instance"/>
/// and refresh with <see cref="Refresh"/> after a language switch.
/// </summary>
public sealed class LocalizationProxy : System.ComponentModel.INotifyPropertyChanged
{
    private static readonly LocalizationProxy Shared = new();

    private LocalizationProxy()
    {
        Translator.Instance.LanguageChanged += (_, _) => RaiseAll();
    }

    public static LocalizationProxy Instance => Shared;

    public Translator Translator => Translator.Instance;

    public FlowDirection Flow => Translator.Instance.Language.Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public string this[string key] => Translator.Instance.Get(key);

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public void RaiseAll()
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(string.Empty));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Flow)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
    }

    public void Raise(string key) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(key));
}

/// <summary>Marks generated text as translated so the leak gate can verify it.</summary>
public static class Localized
{
    public static string Text(string key) => Translator.Instance.Get(key);

    public static string Format(string key, params object?[] args) => Translator.Instance.Format(key, args);
}

/// <summary>Converts a brush resource key into a brush at bind time.</summary>
public sealed class BrushKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current.TryFindResource(key) is Brush brush)
        {
            return brush;
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
