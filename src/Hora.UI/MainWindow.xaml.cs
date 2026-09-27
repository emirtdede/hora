using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hora.Core.Logging;
using Hora.Storage.Db;
using Hora.Storage.Settings;
using Hora.UI.Platform;
using Hora.UI.Tracking;
using Hora.UI.ViewModels;
using Forms = System.Windows.Forms;

namespace Hora.UI;

public partial class MainWindow : Window
{
    private static readonly string[] Palette =
    {
        "#7C8CF8", "#3DDC84", "#FFB020", "#FF6B81", "#38BDF8", "#C084FC", "#F472B6", "#34D399"
    };

    private readonly TrackingEngine _engine;
    private readonly UsageRepository _repo;
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly IHoraLogger _log;
    private readonly AppIconResolver _icons;
    private readonly MainViewModel _vm = new();
    private readonly DispatcherTimer _tick = new();
    private readonly Forms.NotifyIcon _tray = new();

    private int _secondsSinceFlush;
    private bool _trayHintShown;
    private Point _resizeStart;
    private Size _sizeStart;
    private string _resizeMode = string.Empty;

    public MainWindow(
        TrackingEngine engine,
        UsageRepository repo,
        SettingsStore settingsStore,
        AppSettings settings,
        IHoraLogger log)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _icons = new AppIconResolver(_log);

        InitializeComponent();
        DataContext = _vm;

        Left = _settings.Left;
        Top = _settings.Top;
        Width = _settings.Width;
        Height = _settings.Height;
        Opacity = _settings.Opacity;

        _vm.View = _settings.View;
        _vm.Theme = _settings.Theme;
        OpacitySlider.Value = _settings.Opacity;
        AutostartBox.IsChecked = _settings.LaunchOnStartup;

        ApplyTheme(_settings.Theme);
        ApplyView(_settings.View);
        TrySetWindowIcon();
        SetupTray();

        SourceInitialized += (_, _) =>
        {
            GlassEffect.ApplyRoundedCorners(this, _log);
        };

        _tick.Interval = TimeSpan.FromSeconds(1);
        _tick.Tick += OnTick;
        _tick.Start();

        Loaded += async (_, _) =>
        {
            await RefreshAsync().ConfigureAwait(true);
            if (_settings.FirstRun)
            {
                ApplySettingsOpen(true);
                _tray.BalloonTipTitle = "Hora'ya hoş geldin";
                _tray.BalloonTipText = "Otomatik başlatma açık. İstemiyorsan Ayarlar'dan kapatabilirsin.";
                try { _tray.ShowBalloonTip(4000); } catch { /* yoksay */ }
            }
        };
        Closing += OnClosing;
        LocationChanged += (_, _) => SaveGeometry();
        SizeChanged += (_, _) => SaveGeometry();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        try
        {
            await Task.Run(() => _engine.Tick()).ConfigureAwait(true);

            _secondsSinceFlush++;
            if (_secondsSinceFlush >= 10)
            {
                _secondsSinceFlush = 0;
                await _engine.FlushAsync().ConfigureAwait(true);
                await RefreshAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Tick işlenemedi.", ex);
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
            var rows = await _repo.GetDayAsync(today).ConfigureAwait(true);
            var total = rows.Sum(r => r.Seconds);
            _vm.TotalText = $"Bugün: {UsageItemVm.FormatDuration(total)}";
            TotalText.Text = _vm.TotalText;

            var inputs = rows.Select(r => (
                Label: string.IsNullOrEmpty(r.Domain) ? FriendlyExe(r.Exe) : $"{FriendlyExe(r.Exe)} · {r.Domain}",
                IconKey: r.Exe,
                r.Seconds)).ToList();
            var slices = Hora.Core.Presentation.ChartHelper.GroupSmall(inputs, Palette);
            _vm.Items.Clear();
            foreach (var s in slices)
            {
                _vm.Items.Add(new UsageItemVm
                {
                    Label = s.Label,
                    Seconds = s.Seconds,
                    Percent = s.Percent,
                    Color = s.Color,
                    Icon = _icons.Resolve(s.IconKey),
                });
            }

            IndexList.ItemsSource = _vm.Items;
            var mini = _vm.Items
                .Take(3).Select((item, i) => new UsageItemVm
            {
                Rank = i + 1,
                Label = item.Label,
                Seconds = item.Seconds,
                Percent = item.Percent,
                Color = item.Color,
                Icon = item.Icon,
            }).ToList();
            MiniList.ItemsSource = mini;
            var hasData = _vm.Items.Count > 0;
            IndexEmpty.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;
            MiniEmpty.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;

            Donut.Render(_vm.Items.ToList(), total);
            LegendList.ItemsSource = _vm.Items.Take(5).ToList();

            var weekStart = DateOnly.FromDateTime(DateTime.Now.AddDays(-6)).ToString("yyyy-MM-dd");
            var week = await _repo.GetRangeAsync(weekStart, today).ConfigureAwait(true);
            WeekText.Text = UsageItemVm.FormatDuration(week.Sum(r => r.Seconds));
        }
        catch (Exception ex)
        {
            _log.Error("Liste yenilenemedi.", ex);
        }
    }

    private static string FriendlyExe(string exe)
    {
        return exe switch
        {
            "idle" => "Boşta",
            "locked" => "Kilitli",
            "chrome" => "Chrome",
            "msedge" => "Edge",
            "firefox" => "Firefox",
            "code" => "VS Code",
            "devenv" => "Visual Studio",
            "explorer" => "Dosya Gezgini",
            "snippingtool" => "Ekran Alıntısı",
            _ => exe.Length > 0 ? char.ToUpper(exe[0]) + exe[1..] : exe,
        };
    }

    private string _accentHex = "#7C8CF8";

    private void ApplyView(string view)
    {
        _vm.View = view;
        MiniPanel.Visibility = view == "Mini" ? Visibility.Visible : Visibility.Collapsed;
        IndexPanel.Visibility = view == "Index" ? Visibility.Visible : Visibility.Collapsed;
        SpectrumPanel.Visibility = view == "Spectrum" ? Visibility.Visible : Visibility.Collapsed;
        PaintSegment(BtnMini, view == "Mini");
        PaintSegment(BtnIndex, view == "Index");
        PaintSegment(BtnSpectrum, view == "Spectrum");
        _settings.View = view;
        _settingsStore.Save(_settings);
        _ = RefreshAsync();
    }

    private void PaintSegment(System.Windows.Controls.Button btn, bool selected)
    {
        if (selected)
        {
            btn.Background = Resources["AppAccent"] as System.Windows.Media.Brush
                ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString(_accentHex)!);
            btn.Foreground = Resources["AppOnAccent"] as System.Windows.Media.Brush
                ?? new SolidColorBrush(Colors.White);
        }
        else
        {
            btn.ClearValue(BackgroundProperty);
            btn.ClearValue(ForegroundProperty);
        }
    }

    private void ApplyTheme(string theme)
    {
        _vm.Theme = theme;
        if (theme == "White")
        {
            _accentHex = "#4F5BD5";
            SetBrush("AppFg", "#11142A");
            SetBrush("AppMuted", "#5A6285");
            SetBrush("AppCard", "#FFFFFF");
            SetBrush("AppBorder", "#E3E8F7");
            SetBrush("AppTrack", "#EDF0FA");
            SetBrush("AppOnAccent", "#FFFFFF");
            GlassEffect.DisableBlur(this, _log);
        }
        else if (theme == "Glass")
        {
            _accentHex = "#E9EBF3";
            SetBrush("AppFg", "#FFFFFF");
            SetBrush("AppMuted", "#B9B9C2");
            SetBrush("AppCard", "#99212121");
            SetBrush("AppBorder", "#33FFFFFF");
            SetBrush("AppTrack", "#2EFFFFFF");
            SetBrush("AppOnAccent", "#17171C");
            GlassEffect.DisableBlur(this, _log);
        }
        else
        {
            theme = "Black";
            _vm.Theme = theme;
            _accentHex = "#7C8CF8";
            SetBrush("AppFg", "#FFFFFF");
            SetBrush("AppMuted", "#9AA3C7");
            SetBrush("AppCard", "#0B0E1F");
            SetBrush("AppBorder", "#262B45");
            SetBrush("AppTrack", "#232842");
            SetBrush("AppOnAccent", "#FFFFFF");
            GlassEffect.DisableBlur(this, _log);
        }

        _settings.Theme = theme;
        _settingsStore.Save(_settings);
        SetBrush("AppAccent", _accentHex);
        PaintSegment(BtnMini, _vm.View == "Mini");
        PaintSegment(BtnIndex, _vm.View == "Index");
        PaintSegment(BtnSpectrum, _vm.View == "Spectrum");
        PaintSwatches(theme);
    }

    private void PaintSwatches(string theme)
    {
        var accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_accentHex)!);
        var clear = new SolidColorBrush(Colors.Transparent);
        SwatchBlack.BorderBrush = theme == "Black" ? accent : clear;
        SwatchBlack.BorderThickness = theme == "Black" ? new Thickness(2) : new Thickness(1);
        SwatchWhite.BorderBrush = theme == "White" ? accent : clear;
        SwatchWhite.BorderThickness = theme == "White" ? new Thickness(2) : new Thickness(1);
        SwatchGlass.BorderBrush = theme == "Glass" ? accent : clear;
        SwatchGlass.BorderThickness = theme == "Glass" ? new Thickness(2) : new Thickness(1);
    }

    private void SetBrush(string key, string hex)
    {
        // XAML kaynaklı fırçalar dondurulmuş (frozen) olabilir; mutate etme, değiştir.
        Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
    }

    private void TrySetWindowIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                return;
            }

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            if (icon is null)
            {
                return;
            }

            Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        catch
        {
            // Simge yuklenemezse varsayilanla devam.
        }
    }

    private void SetupTray()
    {
        _tray.Text = "Hora - kullanım takibi";
        _tray.Visible = true;
        try
        {
            var exe = Environment.ProcessPath;
            _tray.Icon = !string.IsNullOrEmpty(exe)
                ? System.Drawing.Icon.ExtractAssociatedIcon(exe)
                : System.Drawing.SystemIcons.Application;
        }
        catch
        {
            // Simge yüklenemezse tepsi simgesiz devam eder.
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Göster", null, (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); });
        menu.Items.Add("Mini görünüm", null, (_, _) => { Show(); ApplyView("Mini"); });
        menu.Items.Add("Liste görünüm", null, (_, _) => { Show(); ApplyView("Liste"); });
        menu.Items.Add("Çıkış", null, (_, _) => { _tray.Visible = false; Application.Current.Shutdown(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
    }

    private void SaveGeometry()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Width = Width;
        _settings.Height = Height;
        _settingsStore.Save(_settings);
    }

    private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        if (!_trayHintShown)
        {
            _trayHintShown = true;
            _tray.BalloonTipTitle = "Hora çalışıyor";
            _tray.BalloonTipText = "Widget tepsiye alındı. Geri açmak için simgeye çift tıkla.";
            try { _tray.ShowBalloonTip(3000); } catch { /* yoksay */ }
        }
    }

    private void SettingsToggle_Click(object sender, RoutedEventArgs e)
    {
        ApplySettingsOpen(SettingsPanel.Visibility != Visibility.Visible);
    }

    private void ApplySettingsOpen(bool open)
    {
        SettingsPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ContentPanel.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ViewMini_Click(object sender, RoutedEventArgs e) => ApplyView("Mini");
    private void ViewIndex_Click(object sender, RoutedEventArgs e) => ApplyView("Index");
    private void ViewSpectrum_Click(object sender, RoutedEventArgs e) => ApplyView("Spectrum");

    private void ThemeSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string theme)
        {
            ApplyTheme(theme);
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
        _settings.Opacity = e.NewValue;
        _settingsStore.Save(_settings);
    }

    private void Autostart_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = AutostartBox.IsChecked == true;
        _settings.LaunchOnStartup = enabled;
        _settingsStore.Save(_settings);
        AutostartHelper.SetEnabled(enabled);
    }

    private void ResizeRight_Start(object sender, MouseButtonEventArgs e)
    {
        _resizeMode = "right";
        _resizeStart = PointToScreen(e.GetPosition(this));
        _sizeStart = new Size(Width, Height);
        CaptureMouse();
    }

    private void ResizeBottom_Start(object sender, MouseButtonEventArgs e)
    {
        _resizeMode = "bottom";
        _resizeStart = PointToScreen(e.GetPosition(this));
        _sizeStart = new Size(Width, Height);
        CaptureMouse();
    }

    private void ResizeCorner_Start(object sender, MouseButtonEventArgs e)
    {
        _resizeMode = "corner";
        _resizeStart = PointToScreen(e.GetPosition(this));
        _sizeStart = new Size(Width, Height);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_resizeMode.Length == 0 || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var now = PointToScreen(e.GetPosition(this));
        var dx = now.X - _resizeStart.X;
        var dy = now.Y - _resizeStart.Y;
        if (_resizeMode is "right" or "corner")
        {
            Width = Math.Clamp(_sizeStart.Width + dx, MinWidth, MaxWidth);
        }

        if (_resizeMode is "bottom" or "corner")
        {
            Height = Math.Clamp(_sizeStart.Height + dy, MinHeight, MaxHeight);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_resizeMode.Length > 0)
        {
            _resizeMode = string.Empty;
            ReleaseMouseCapture();
            SaveGeometry();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        try
        {
            _tick.Stop();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            _engine.FlushAsync(cts.Token).GetAwaiter().GetResult();
            _settings.FirstRun = false;
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            _log.Error("Kapanışta flush başarısız.", ex);
        }
        finally
        {
            _tray.Visible = false;
            _engine.Dispose();
        }
    }
}
