using CommunityToolkit.Mvvm.ComponentModel;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.App.ViewModels;

public sealed partial class RulesViewModel(bool isMac, Func<string> translationHotkey) : ObservableObject
{
    private AppSettings? _shownSettings;
    private string? _shownHotkey;

    [ObservableProperty] private IReadOnlyList<RuleSection> _sections = [];

    public void Apply(AppSettings settings)
    {
        var hotkey = translationHotkey();
        if (settings != _shownSettings || hotkey != _shownHotkey)
        {
            _shownSettings = settings;
            _shownHotkey = hotkey;
            Sections = RuleCatalog.Build(settings, isMac, hotkey);
        }
    }
}
