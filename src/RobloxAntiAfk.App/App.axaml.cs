using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RobloxAntiAfk.App.ViewModels;
using RobloxAntiAfk.App.Views;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Storage;

namespace RobloxAntiAfk.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var engine = CreateEngine();
            desktop.MainWindow = new MainWindow { DataContext = new MainViewModel(engine) };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => engine.Tick();
            timer.Start();

            engine.Start();
            engine.Tick();
            desktop.Exit += (_, _) => engine.Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static AntiAfkEngine CreateEngine()
    {
        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Roblox AntiAFK");

        return new AntiAfkEngine(
            PlatformFactory.Create(),
            new SystemClock(),
            new JsonFileStore<AppSettings>(Path.Combine(dataFolder, "settings.json"), () => new AppSettings()),
            new JsonFileStore<AfkStatistics>(Path.Combine(dataFolder, "statistics.json"), () => new AfkStatistics()));
    }
}
