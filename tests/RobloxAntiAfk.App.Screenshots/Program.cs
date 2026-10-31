using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using RobloxAntiAfk.App;
using RobloxAntiAfk.App.ViewModels;
using RobloxAntiAfk.App.Views;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;
using RobloxAntiAfk.Core.Tests.Fakes;

// Рисует экраны приложения в PNG без запуска окна и без кликов.
// Все данные подставные: приложение не обращается ни к Roblox, ни к системе.
var outputFolder = args.Length > 0 ? args[0] : "screenshots";
Directory.CreateDirectory(outputFolder);

AppBuilder.Configure<App>()
    .UseSkia()
    .WithInterFont()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var sampleDay = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
var sampleStatistics = new AfkStatistics
{
    TotalSeconds = 93725,
    LongestSessionSeconds = 14400,
    TotalActions = 412,
    Sessions =
    [
        new SessionRecord(sampleDay.AddHours(-3), 3725, 12),
        new SessionRecord(sampleDay.AddDays(-1), 14400, 48),
        new SessionRecord(sampleDay.AddDays(-2).AddHours(3), 1821, 6),
        new SessionRecord(sampleDay.AddDays(-3), 301, 1),
    ],
};

(AntiAfkEngine Engine, TestWorld World) CreateEngine(AppSettings settings, Action<TestWorld>? arrange = null, bool isRunning = true)
{
    var world = new TestWorld().WithRoblox(TestWorld.First);
    arrange?.Invoke(world);
    var engine = new AntiAfkEngine(world.Platform, world.Clock, new MemoryStore<AppSettings>(settings), new MemoryStore<AfkStatistics>(sampleStatistics));
    if (isRunning)
    {
        engine.ToggleRunning();
    }

    return (engine, world);
}

ScreenTranslator CreateTranslator(AntiAfkEngine engine, TestWorld world) => new(
    world.Platform,
    engine,
    new FakeOverlay(),
    (_, _, _) => Task.FromResult<string?>("Поговори с торговцем, чтобы купить новый меч"),
    world.Clock);

void Capture(string name, AntiAfkEngine engine, MainTab tab, bool isMac = true, double? height = null, ScreenTranslator? translator = null)
{
    var viewModel = new MainViewModel(engine, new TelegramNotifier(new HttpClient()), translator ?? CreateTranslator(engine, new TestWorld()), isMac) { SelectedTab = tab };
    var window = new MainWindow { DataContext = viewModel };
    if (height is { } value)
    {
        window.Height = value;
    }

    window.Show();
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    Dispatcher.UIThread.RunJobs();
    window.CaptureRenderedFrame()!.Save(Path.Combine(outputFolder, $"{name}.png"));
    window.Close();
}

var (running, runningWorld) = CreateEngine(new AppSettings());
running.RunNow();
runningWorld.Clock.Advance(TimeSpan.FromSeconds(113));
running.Tick();
Capture("rules", running, MainTab.Rules);
Capture("rules-windows", running, MainTab.Rules, isMac: false);
Capture("rules-all-windows", running, MainTab.Rules, isMac: false, height: 2100);
Capture("rules-all-mac", running, MainTab.Rules, height: 2200);
Capture("statistics-running", running, MainTab.Statistics);

var (walk, walkWorld) = CreateEngine(new AppSettings { Action = AfkAction.Walk, IntervalMinutes = 17 }, w => w.WithRoblox(TestWorld.Second));
walk.Tick();
Capture("settings-walk", walk, MainTab.Settings);

var (key, _) = CreateEngine(new AppSettings { Action = AfkAction.Key, Key = GameKey.W });
key.Tick();
Capture("settings-key", key, MainTab.Settings);

var (point, _) = CreateEngine(new AppSettings { ClickPoint = new WindowPoint(0.32, 0.61) });
point.Tick();
Capture("settings-point", point, MainTab.Settings);

var (picking, _) = CreateEngine(new AppSettings());
picking.BeginPointPick();
Capture("settings-picking", picking, MainTab.Settings);

var (macro, _) = CreateEngine(new AppSettings
{
    Action = AfkAction.Macro,
    Macro = new Macro { Steps = [new MacroStep(0, MacroStepKind.Key, true, GameKey.W), new MacroStep(1.5, MacroStepKind.Key, false, GameKey.W)] },
    ReconnectEnabled = true,
    GameLink = "roblox.com/share?code=abc",
});
macro.Tick();
Capture("settings-all", macro, MainTab.Settings, isMac: false, height: 1900);

var (stopped, _) = CreateEngine(new AppSettings(), isRunning: false);
stopped.Tick();
Capture("statistics-stopped", stopped, MainTab.Statistics);

var (noPermission, _) = CreateEngine(new AppSettings(), w => w.Permission.IsGranted = false);
noPermission.Tick();
Capture("permission", noPermission, MainTab.Rules);

var (waiting, waitingWorld) = CreateEngine(new AppSettings());
waitingWorld.Windows.Roblox.Clear();
waiting.Tick();
Capture("waiting", waiting, MainTab.Rules, isMac: false);

var (translation, translationWorld) = CreateEngine(new AppSettings { TranslationEnabled = true }, isRunning: false);
translationWorld.Windows.Foreground = TestWorld.First;
translationWorld.TextReader.Result = new TextReadResult(TextReadStatus.Success, [new RecognizedLine("Talk to the merchant to buy a new sword", 0, 0, 1, 0.2)]);
var translator = CreateTranslator(translation, translationWorld);
translator.SyncWithSettings();
translator.BeginSelection();
translationWorld.Capture.Drag(new ScreenPoint(200, 500), new ScreenPoint(800, 650));
Capture("translation", translation, MainTab.Translation, translator: translator);

var (eachTime, eachTimeWorld) = CreateEngine(new AppSettings { TranslationEnabled = true }, isRunning: false);
var eachTimeTranslator = CreateTranslator(eachTime, eachTimeWorld);
eachTimeTranslator.SyncWithSettings();
Capture("translation-each-time", eachTime, MainTab.Translation, translator: eachTimeTranslator);

var (translationOff, translationOffWorld) = CreateEngine(new AppSettings(), isRunning: false);
Capture("translation-off", translationOff, MainTab.Translation, translator: CreateTranslator(translationOff, translationOffWorld));

var overlayWindow = new OverlayWindow { Width = 460, Height = 90 };
overlayWindow.ShowMessage("Поговори с торговцем, чтобы купить новый меч. Награда: 500 золотых монет за каждое выполненное задание.", false, 460);
overlayWindow.Show();
Dispatcher.UIThread.RunJobs();
AvaloniaHeadlessPlatform.ForceRenderTimerTick();
Dispatcher.UIThread.RunJobs();
overlayWindow.CaptureRenderedFrame()!.Save(Path.Combine(outputFolder, "overlay.png"));
overlayWindow.Close();

Console.WriteLine($"Готово: {Path.GetFullPath(outputFolder)}");
