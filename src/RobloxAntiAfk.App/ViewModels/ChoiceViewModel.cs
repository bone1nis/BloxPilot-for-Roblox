using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RobloxAntiAfk.App.ViewModels;

/// <summary>Один вариант в группе кнопок выбора.</summary>
public sealed partial class ChoiceViewModel<T>(T value, Func<string> label, Action<T> select) : ChoiceViewModel(label, new RelayCommand(() => select(value)))
{
    public T Value { get; } = value;
}

public abstract partial class ChoiceViewModel(Func<string> label, ICommand select) : ObservableObject
{
    public string Label => label();
    public ICommand Select { get; } = select;

    [ObservableProperty] private bool _isSelected;
}
