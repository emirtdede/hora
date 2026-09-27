using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hora.UI.ViewModels;

public sealed class UsageItemVm : INotifyPropertyChanged
{
    private string _label = string.Empty;
    private long _seconds;
    private double _percent;
    private string _color = "#7C8CF8";
    private int _rank;
    private System.Windows.Media.ImageSource? _icon;

    public int Rank
    {
        get => _rank;
        set { _rank = value; OnPropertyChanged(); }
    }

    public System.Windows.Media.ImageSource? Icon
    {
        get => _icon;
        set { _icon = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasIcon)); }
    }

    public bool HasIcon => _icon is not null;

    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public long Seconds
    {
        get => _seconds;
        set { _seconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationText)); }
    }

    public double Percent
    {
        get => _percent;
        set { _percent = value; OnPropertyChanged(); }
    }

    public string Color
    {
        get => _color;
        set { _color = value; OnPropertyChanged(); }
    }

    public string DurationText => Hora.Core.Presentation.DurationFormatter.Format(Seconds);

    public static string FormatDuration(long totalSeconds) =>
        Hora.Core.Presentation.DurationFormatter.Format(totalSeconds);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string _totalText = "Bugün: 0 dk";
    private string _view = "Index";
    private string _theme = "Black";

    public string TotalText
    {
        get => _totalText;
        set { _totalText = value; OnPropertyChanged(); }
    }

    public string View
    {
        get => _view;
        set { _view = value; OnPropertyChanged(); }
    }

    public string Theme
    {
        get => _theme;
        set { _theme = value; OnPropertyChanged(); }
    }

    public ObservableCollection<UsageItemVm> Items { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
