using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Hora.Core.Browser;
using Hora.Core.Localization;
using Hora.Core.Logging;
using Hora.Core.Privacy;
using Hora.Core.Tracking;
using Hora.Storage.Db;
using Hora.Storage.Paths;
using Hora.Storage.Settings;
using Hora.UI.Platform;
using Hora.UI.Tracking;
using Hora.UI.Wizard;

namespace Hora.UI;

public partial class App : Application
{
    private const string ActivationEventName = @"Local\Hora.Activate";

    private static Mutex? _instanceMutex;
    private static bool _ownsMutex;
    private AppContext? _context;
    private ThemeManager? _themeManager;
    private EventWaitHandle? _activation;
    private Thread? _activationThread;

    public static AppContext? Context { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // The setup wizard is briefly the only window. With the default
        // OnLastWindowClose mode, closing it starts application shutdown before
        // the main window exists. Exit is always explicit in this app.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _instanceMutex = new Mutex(true, @"Local\Hora.SingleInstance", out var isFirst);
        _ownsMutex = isFirst;
        if (!isFirst)
        {
            // A second launch (tray icon, jump list, Start menu) must reach the
            // running window instead of silently exiting.
            SignalRunningInstance(e.Args);
            Shutdown();
            return;
        }

        HoraPaths.EnsureCreated();
        DispatcherUnhandledException += OnDispatcherException;

        var logger = new FileHoraLogger(HoraPaths.LogPath);
        logger.Info("Hora starting.");
        try
        {
            var store = new SettingsStore(HoraPaths.SettingsPath);
            var settings = store.Load();

            // Language defaults to the Windows UI language on first run.
            if (!settings.CompletedSetup && !System.IO.File.Exists(HoraPaths.SettingsPath))
            {
                var resolved = LanguageCatalog.ResolveFromCulture(
                    System.Globalization.CultureInfo.CurrentUICulture.Name);
                if (resolved == LanguageCatalog.FallbackId)
                {
                    resolved = LanguageCatalog.ResolveFromCulture(
                        System.Globalization.CultureInfo.InstalledUICulture.Name);
                }

                settings.Language = resolved;
                store.Save(settings);
            }

            var repository = new UsageRepository(HoraPaths.DbPath);
            repository.InitializeAsync().GetAwaiter().GetResult();

            var session = new SessionStateService();
            var audio = new AudioActivityService();
            var engine = new TrackingEngine(
                new ForegroundService(),
                new IdleService(),
                new FullscreenService(),
                audio,
                session,
                new MeetingService(audio),
                new DomainExtractor(),
                new PrivacyFilter(),
                repository,
                logger);

            _context = new AppContext(settings, store, repository, engine, logger);
            Context = _context;
            _themeManager = new ThemeManager(this);
            _context.ThemeManager = _themeManager;
            _context.ApplyTheme();

            AutostartHelper.SetEnabled(settings.LaunchOnStartup, settings.SilentStart);

            var minimized = e.Args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
            var openSettings = e.Args.Any(a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase));

            if (!settings.CompletedSetup && !minimized)
            {
                var wizard = new SetupWindow(_context);
                if (wizard.ShowDialog() != true)
                {
                    logger.Info("Setup cancelled; exiting.");
                    Shutdown();
                    return;
                }
            }

            var window = new MainWindow(_context);
            MainWindow = window;
            if (!minimized)
            {
                window.Show();
                if (openSettings)
                {
                    window.NavigateTo("settings");
                }
            }

            if (settings.CompletedSetup)
            {
                window.StartTracking();
            }

            StartActivationListener();
        }
        catch (Exception exception)
        {
            logger.Error("Startup failed.", exception);
            MessageBox.Show(
                Translator.Instance.Get("unknownError"),
                "Hora",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>Starts the background wait that turns a second launch into a window activation.</summary>
    private void StartActivationListener()
    {
        try
        {
            _activation = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            // Without the event, single-instance behavior still prevents a second process.
            return;
        }

        _activationThread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    _activation.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (Dispatcher.HasShutdownStarted)
                {
                    return;
                }

                try
                {
                    Dispatcher.BeginInvoke(new Action(HandleActivationRequest));
                }
                catch (InvalidOperationException)
                {
                    // The dispatcher is already shutting down.
                    return;
                }
            }
        })
        {
            IsBackground = true,
            Name = "Hora activation listener",
        };
        _activationThread.Start();
    }
    private static void SignalRunningInstance(IReadOnlyList<string> args)
    {
        try
        {
            Directory.CreateDirectory(HoraPaths.AppDir);
            File.WriteAllLines(HoraPaths.ActivationPath, args);
            using var handle = EventWaitHandle.OpenExisting(ActivationEventName);
            handle.Set();
        }
        catch (Exception exception) when (exception is WaitHandleCannotBeOpenedException
                                              or IOException
                                              or UnauthorizedAccessException)
        {
            // The running instance is already gone; nothing to activate.
        }
    }

    private void HandleActivationRequest()
    {
        if (MainWindow is not MainWindow window)
        {
            return;
        }

        var args = Array.Empty<string>();
        try
        {
            if (File.Exists(HoraPaths.ActivationPath))
            {
                args = File.ReadAllLines(HoraPaths.ActivationPath);
                File.Delete(HoraPaths.ActivationPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The request is a convenience; the window still comes to the front.
        }

        window.ShowFromTray();
        if (args.Any(arg => string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase)))
        {
            window.NavigateTo("settings");
        }
    }

    private void OnDispatcherException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _context?.Logger.Error($"Unhandled UI exception. {e.Exception}");
        if (_context is null)
        {
            return;
        }

        // Expected, recoverable UI failures (a control racing shutdown, a locked
        // file) keep the app alive. Anything else is a defect: report it and shut
        // down instead of continuing in an unknown state.
        var recoverable = e.Exception is InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or System.Runtime.InteropServices.ExternalException;
        ToastService.Show(_context.T("unknownError"), ToastKind.Error);
        e.Handled = true;
        if (!recoverable)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MainWindow is MainWindow window)
                {
                    window.ExitForFailure();
                }
            }));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_context is not null)
            {
                _context.Engine.FlushAsync().GetAwaiter().GetResult();
                _context.Dispose();
            }
        }
        catch (Exception exception) when (exception is AggregateException or TimeoutException or ObjectDisposedException)
        {
            // Shutdown must not fail because of a background flush.
        }
        finally
        {
            try
            {
                _activation?.Set();
            }
            catch (ObjectDisposedException)
            {
                // Already closed.
            }

            _activation?.Dispose();
            // Only the first instance owns the mutex; a second launch releases
            // nothing and must not throw during shutdown.
            if (_ownsMutex)
            {
                try
                {
                    _instanceMutex?.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // The mutex was already released or abandoned.
                }
            }

            _instanceMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
