using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Hora.Core.Aggregation;
using Hora.Core.Browser;
using Hora.Core.Logging;
using Hora.Core.Privacy;
using Hora.Core.Tracking;
using Hora.Storage.Db;
using Hora.Storage.Paths;
using Hora.Storage.Settings;
using Hora.UI.Platform;
using Hora.UI.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hora.UI;

public partial class App : Application
{
    private static Mutex? _mutex;
    private IHost? _host;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        _mutex = new Mutex(true, "HoraSingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        HoraPaths.EnsureCreated();
        var logger = new FileHoraLogger(HoraPaths.LogPath);
        SetupCrashHandlers(logger);

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IHoraLogger>(logger);
                    services.AddSingleton<IForegroundService, ForegroundService>();
                    services.AddSingleton<IIdleService, IdleService>();
                    services.AddSingleton<IFullscreenService, FullscreenService>();
                    services.AddSingleton<IAudioActivityService, AudioActivityService>();
                    services.AddSingleton<ISessionStateService, SessionStateService>();
                    services.AddSingleton<IMeetingService, MeetingService>();
                    services.AddSingleton<IDomainExtractor, DomainExtractor>();
                    services.AddSingleton<IPrivacyFilter, PrivacyFilter>();
                    services.AddSingleton<UsageAggregator>();
                    services.AddSingleton(_ => new UsageRepository(HoraPaths.DbPath));
                    services.AddSingleton(_ => new SettingsStore(HoraPaths.SettingsPath));
                })
                .Build();

            var repo = _host.Services.GetRequiredService<UsageRepository>();
            await repo.InitializeAsync().ConfigureAwait(true);

            var store = _host.Services.GetRequiredService<SettingsStore>();
            var settings = store.Load();
            if (Math.Abs(settings.Width - 340) < 0.5 && Math.Abs(settings.Height - 420) < 0.5)
            {
                settings.Height = 500;
                store.Save(settings);
                logger.Info("Pencere boyutu yeni varsayilana goculdu (420 -> 500).");
            }
            if (settings.FirstRun && settings.LaunchOnStartup)
            {
                AutostartHelper.SetEnabled(true);
            }
            else if (!settings.LaunchOnStartup && AutostartHelper.IsEnabled())
            {
                AutostartHelper.SetEnabled(false);
            }

            var engine = new TrackingEngine(
                _host.Services.GetRequiredService<IForegroundService>(),
                _host.Services.GetRequiredService<IIdleService>(),
                _host.Services.GetRequiredService<IFullscreenService>(),
                _host.Services.GetRequiredService<IAudioActivityService>(),
                _host.Services.GetRequiredService<ISessionStateService>(),
                _host.Services.GetRequiredService<IMeetingService>(),
                _host.Services.GetRequiredService<IDomainExtractor>(),
                _host.Services.GetRequiredService<IPrivacyFilter>(),
                _host.Services.GetRequiredService<UsageAggregator>(),
                repo,
                logger);

            var window = new MainWindow(engine, repo, store, settings, logger);
            MainWindow = window;

            var minimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            if (!minimized)
            {
                window.Show();
            }
        }
        catch (Exception ex)
        {
            logger.Error("Açılış başarısız.", ex);
            MessageBox.Show(
                "Hora başlatılamadı. Log dosyası AppData/Hora/logs altında. Bilgisayarı yeniden başlatıp tekrar dene.",
                "Hora", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void SetupCrashHandlers(IHoraLogger logger)
    {
        DispatcherUnhandledException += (_, e) =>
        {
            logger.Error("UI hatası.", e.Exception);
            e.Handled = true;
            MessageBox.Show("Beklenmeyen bir hata oldu ama Hora çalışmaya devam ediyor.",
                "Hora", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.Error("Arka plan hatası.", e.Exception);
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            logger.Error("Kritik hata.", e.ExceptionObject as Exception);
        };
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
        }

        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
