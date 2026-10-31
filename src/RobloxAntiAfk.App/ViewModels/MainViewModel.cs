using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.App.ViewModels;

public enum MainTab
{
    Rules,
    Settings,
    Statistics,
    Translation,
}

/// <summary>Корень интерфейса: получает снимки состояния движка и раздаёт их панелям.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AntiAfkEngine _engine;

    public MainViewModel(AntiAfkEngine engine, TelegramNotifier telegram, ScreenTranslator translator, bool? isMac = null)
    {
        _engine = engine;
        IsMac = isMac ?? OperatingSystem.IsMacOS();
        Status = new StatusViewModel(engine);
        Settings = new SettingsViewModel(engine, telegram);
        Statistics = new StatisticsViewModel(engine);
        Rules = new RulesViewModel(IsMac, translator.Snapshot.HotkeyLabel);
        Translation = new TranslationViewModel(engine, translator);

        engine.Changed += Refresh;
        Refresh();
    }

    public bool IsMac { get; }
    public StatusViewModel Status { get; }
    public SettingsViewModel Settings { get; }
    public StatisticsViewModel Statistics { get; }
    public RulesViewModel Rules { get; }
    public TranslationViewModel Translation { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRulesTab), nameof(IsSettingsTab), nameof(IsStatisticsTab), nameof(IsTranslationTab))]
    private MainTab _selectedTab = MainTab.Rules;

    public bool IsRulesTab => SelectedTab == MainTab.Rules;
    public bool IsSettingsTab => SelectedTab == MainTab.Settings;
    public bool IsStatisticsTab => SelectedTab == MainTab.Statistics;
    public bool IsTranslationTab => SelectedTab == MainTab.Translation;

    [RelayCommand]
    private void SelectTab(MainTab tab) => SelectedTab = tab;

    private void Refresh()
    {
        EngineSnapshot snapshot = _engine.Snapshot;
        Status.Apply(snapshot);
        Settings.Apply(snapshot);
        Statistics.Apply(snapshot);
        Rules.Apply(snapshot.Settings);
        Translation.Refresh();
    }
}
