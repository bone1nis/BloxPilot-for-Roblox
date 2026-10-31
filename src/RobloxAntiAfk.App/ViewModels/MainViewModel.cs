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
}

/// <summary>Корень интерфейса: получает снимки состояния движка и раздаёт их панелям.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AntiAfkEngine _engine;

    public MainViewModel(AntiAfkEngine engine, bool? isMac = null)
    {
        _engine = engine;
        IsMac = isMac ?? OperatingSystem.IsMacOS();
        Status = new StatusViewModel(engine);
        Settings = new SettingsViewModel(engine);
        Statistics = new StatisticsViewModel(engine);
        Rules = new RulesViewModel(IsMac);

        engine.Changed += Refresh;
        Refresh();
    }

    public bool IsMac { get; }
    public StatusViewModel Status { get; }
    public SettingsViewModel Settings { get; }
    public StatisticsViewModel Statistics { get; }
    public RulesViewModel Rules { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRulesTab), nameof(IsSettingsTab), nameof(IsStatisticsTab))]
    private MainTab _selectedTab = MainTab.Rules;

    public bool IsRulesTab => SelectedTab == MainTab.Rules;
    public bool IsSettingsTab => SelectedTab == MainTab.Settings;
    public bool IsStatisticsTab => SelectedTab == MainTab.Statistics;

    [RelayCommand]
    private void SelectTab(MainTab tab) => SelectedTab = tab;

    private void Refresh()
    {
        EngineSnapshot snapshot = _engine.Snapshot;
        Status.Apply(snapshot);
        Settings.Apply(snapshot);
        Statistics.Apply(snapshot);
        Rules.Apply(snapshot.Settings);
    }
}
