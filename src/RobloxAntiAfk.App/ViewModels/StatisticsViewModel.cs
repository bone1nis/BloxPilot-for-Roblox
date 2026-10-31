using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.App.ViewModels;

public sealed partial class StatisticsViewModel(AntiAfkEngine engine) : ObservableObject
{
    [ObservableProperty] private string _currentSession = "0:00";
    [ObservableProperty] private string _longestSession = "0:00";
    [ObservableProperty] private string _totalTime = "0:00";
    [ObservableProperty] private string _totalActions = "0";

    public void Apply(EngineSnapshot snapshot)
    {
        CurrentSession = Texts.Duration(snapshot.CurrentSession);
        LongestSession = Texts.Duration(TimeSpan.FromSeconds(snapshot.Statistics.LongestSessionSeconds));
        TotalTime = Texts.Duration(TimeSpan.FromSeconds(snapshot.Statistics.TotalSeconds));
        TotalActions = snapshot.Statistics.TotalActions.ToString();
    }

    [RelayCommand]
    private void Reset() => engine.ResetStatistics();
}
