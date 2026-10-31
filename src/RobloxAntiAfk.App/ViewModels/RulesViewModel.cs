using CommunityToolkit.Mvvm.ComponentModel;
using RobloxAntiAfk.App.Presentation;
using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.App.ViewModels;

public sealed partial class RulesViewModel(bool isMac) : ObservableObject
{
    private AppSettings? _shownSettings;

    [ObservableProperty] private IReadOnlyList<RuleSection> _sections = [];

    public void Apply(AppSettings settings)
    {
        if (settings != _shownSettings)
        {
            _shownSettings = settings;
            Sections = RuleCatalog.Build(settings, isMac);
        }
    }
}
