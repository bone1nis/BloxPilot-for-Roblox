using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.App.ViewModels;
using RobloxAntiAfk.App.Views;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Storage;

namespace RobloxAntiAfk.App;

public sealed class App : Application
{
    private const string AutoStartArgument = "--autostart";
    private const string SelfTestArgument = "--self-test=";
    private static readonly TimeSpan TelegramTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan TranslationTimeout = TimeSpan.FromSeconds(8);

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var platform = PlatformFactory.Create();
#if WINDOWS_OCR
            if (OperatingSystem.IsWindows() && desktop.Args?.FirstOrDefault(argument => argument.StartsWith(SelfTestArgument, StringComparison.Ordinal)) is { } selfTest)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                Dispatcher.UIThread.Post(async () => desktop.Shutdown(await SelfTest.RunAsync(platform, selfTest[SelfTestArgument.Length..])));
                base.OnFrameworkInitializationCompleted();
                return;
            }
#endif

            var clock = new SystemClock();
            var engine = CreateEngine(platform, clock);
            var telegram = new TelegramNotifier(TelegramNotifier.CreateHttpClient(TelegramTimeout));
            telegram.UseRelay(engine.Snapshot.Settings.TelegramRelayUrl);
            engine.Changed += () => telegram.UseRelay(engine.Snapshot.Settings.TelegramRelayUrl);
            var outbox = new TelegramOutbox(telegram, clock);
            var translator = new ScreenTranslator(
                platform,
                engine,
                new TranslationOverlay(platform.Overlay, OperatingSystem.IsMacOS()),
                new OnlineTranslator(new HttpClient { Timeout = TranslationTimeout }).TranslateAsync,
                clock);
            engine.Changed += translator.SyncWithSettings;
            var window = new MainWindow { DataContext = new MainViewModel(engine, telegram, translator) };

            // Окном управляем сами: приложение может жить в трее и без него.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var settings = engine.Snapshot.Settings;
            var startsHidden = settings.CloseToTray && desktop.Args?.Contains(AutoStartArgument) == true;
            if (!startsHidden)
            {
                desktop.MainWindow = window;
            }

            window.Closing += (_, e) =>
            {
                if (e.CloseReason == WindowCloseReason.WindowClosing && engine.Snapshot.Settings.CloseToTray)
                {
                    e.Cancel = true;
                    window.Hide();
                }
                else if (e.CloseReason == WindowCloseReason.WindowClosing)
                {
                    desktop.Shutdown();
                }
            };

            SetUpTray(desktop, window, engine);
            engine.ActivityAdded += entry => QueueTelegram(engine, outbox, entry);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                engine.Tick();
                translator.Tick();
                var settings = engine.Snapshot.Settings;
                _ = outbox.SendPendingAsync(settings.TelegramToken, settings.TelegramChatId);
            };
            timer.Start();

            engine.Start();
            engine.Tick();
            desktop.Exit += (_, _) =>
            {
                translator.Shutdown();
                engine.Shutdown();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetUpTray(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window, AntiAfkEngine engine)
    {
        var open = new NativeMenuItem("Открыть окно");
        open.Click += (_, _) => ShowWindow(window);

        var toggle = new NativeMenuItem("Запустить");
        toggle.Click += (_, _) => engine.ToggleRunning();
        var isStopped = true;
        engine.Changed += () =>
        {
            var nowStopped = engine.Snapshot.State == EngineState.Stopped;
            if (nowStopped != isStopped)
            {
                isStopped = nowStopped;
                toggle.Header = nowStopped ? "Запустить" : "Остановить";
            }
        };

        var quit = new NativeMenuItem("Выйти");
        quit.Click += (_, _) => desktop.Shutdown();

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://RobloxAntiAFK/Assets/icon.png"))),
            ToolTipText = "Roblox AntiAFK",
            Menu = new NativeMenu { open, toggle, new NativeMenuItemSeparator(), quit },
        };
        tray.Clicked += (_, _) => ShowWindow(window);
        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }

    private static void ShowWindow(MainWindow window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    /// <summary>Сообщение может уйти намного позже события, поэтому в нём указано время.</summary>
    private static void QueueTelegram(AntiAfkEngine engine, TelegramOutbox outbox, ActivityEntry entry)
    {
        var settings = engine.Snapshot.Settings;
        if (Texts.IsWorthNotifying(entry.Code) && settings.TelegramChatId.Length > 0)
        {
            outbox.Add($"Roblox AntiAFK, {entry.Time.ToLocalTime():HH:mm}: {Texts.Activity(entry)}");
            _ = outbox.SendPendingAsync(settings.TelegramToken, settings.TelegramChatId);
        }
    }

    private static AntiAfkEngine CreateEngine(PlatformServices platform, IClock clock)
    {
        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Roblox AntiAFK");

        return new AntiAfkEngine(
            platform,
            clock,
            new JsonFileStore<AppSettings>(Path.Combine(dataFolder, "settings.json"), () => new AppSettings()),
            new JsonFileStore<AfkStatistics>(Path.Combine(dataFolder, "statistics.json"), () => new AfkStatistics()));
    }
}
