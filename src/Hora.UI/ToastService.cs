using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Hora.UI;

public enum ToastKind
{
    Info = 0,
    Success = 1,
    Error = 2,
}

/// <summary>Lightweight in-app notification host. One toast is visible at a time.</summary>
public static class ToastService
{
    private static Border? _host;
    private static TextBlock? _text;
    private static DispatcherTimer? _timer;

    public static void Attach(Border host, TextBlock text)
    {
        _timer?.Stop();
        _timer = null;
        _host = host;
        _text = text;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) =>
        {
            _timer!.Stop();
            Hide();
        };
    }

    /// <summary>Releases the visual references so a closed window is not kept alive.</summary>
    public static void Detach(Border host)
    {
        if (!ReferenceEquals(_host, host))
        {
            return;
        }

        _timer?.Stop();
        _timer = null;
        _host = null;
        _text = null;
    }

    public static void Show(string message, ToastKind kind = ToastKind.Info)
    {
        if (_host is null || _text is null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _text.Text = message;
        _host.Background = kind switch
        {
            ToastKind.Success => (Brush)(Application.Current.TryFindResource("BrushSurface") ?? Brushes.Black),
            ToastKind.Error => (Brush)(Application.Current.TryFindResource("BrushSurface") ?? Brushes.Black),
            _ => (Brush)(Application.Current.TryFindResource("BrushSurface") ?? Brushes.Black),
        };
        var accent = kind switch
        {
            ToastKind.Success => (Brush)(Application.Current.TryFindResource("BrushSuccess") ?? Brushes.White),
            ToastKind.Error => (Brush)(Application.Current.TryFindResource("BrushDanger") ?? Brushes.White),
            _ => (Brush)(Application.Current.TryFindResource("BrushAccent") ?? Brushes.White),
        };

        if (_host.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Border bar)
        {
            bar.Background = accent;
        }

        _host.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
        if (!(Application.Current.TryFindResource("ReducedMotion") is bool reduced && reduced))
        {
            _host.BeginAnimation(UIElement.OpacityProperty, animation);
        }
        else
        {
            _host.Opacity = 1;
        }

        _timer?.Stop();
        _timer?.Start();
    }

    private static void Hide()
    {
        if (_host is null)
        {
            return;
        }

        _host.Visibility = Visibility.Collapsed;
        _host.Opacity = 0;
    }
}
