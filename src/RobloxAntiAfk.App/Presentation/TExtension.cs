using RobloxAntiAfk.Core.Localization;

namespace RobloxAntiAfk.App.Presentation;

/// <summary>Текст в разметке: {l:T 'Настройки'}. При смене языка экран создаётся заново, поэтому следить за языком здесь не нужно.</summary>
public sealed class TExtension(string text)
{
    public string ProvideValue(IServiceProvider serviceProvider) => L.T(text);
}
