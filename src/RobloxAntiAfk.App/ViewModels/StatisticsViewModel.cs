using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Services;

namespace RobloxAntiAfk.App.ViewModels;

public sealed record SessionItem(string Start, string Duration, string Actions);

public sealed partial class StatisticsViewModel(AntiAfkEngine engine) : ObservableObject
{
    private IReadOnlyList<SessionRecord>? _shownSessions;

    [ObservableProperty] private IReadOnlyList<SessionItem> _sessions = [];
    [ObservableProperty] private bool _hasSessions;
    [ObservableProperty] private SessionItem? _ongoingSession;
    [ObservableProperty] private bool _hasOngoingSession;
    [ObservableProperty] private bool _isListEmpty = true;
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

        OngoingSession = snapshot.OngoingSession is { } ongoing ? ToItem(ongoing) : null;
        HasOngoingSession = OngoingSession is not null;
        if (!ReferenceEquals(snapshot.Statistics.Sessions, _shownSessions))
        {
            _shownSessions = snapshot.Statistics.Sessions;
            Sessions = _shownSessions.Select(ToItem).ToArray();
            HasSessions = Sessions.Count > 0;
        }

        IsListEmpty = !HasSessions && !HasOngoingSession;
    }

    private static SessionItem ToItem(SessionRecord session) => new(
        session.Start.ToLocalTime().ToString("dd.MM.yyyy, HH:mm"),
        Texts.Duration(TimeSpan.FromSeconds(session.DurationSeconds)),
        Texts.Actions(session.Actions));

    [RelayCommand]
    private void Reset() => engine.ResetStatistics();
}
