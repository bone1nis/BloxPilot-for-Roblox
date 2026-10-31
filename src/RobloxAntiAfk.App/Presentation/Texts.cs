using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.App.Presentation;

/// <summary>Все тексты, которые зависят от состояния. Ядро ничего не знает о языке интерфейса.</summary>
public static class Texts
{
    public static string Minutes(int count)
    {
        var last = count % 10;
        var teens = count % 100 is >= 11 and <= 14;
        if (!teens && last == 1)
        {
            return $"{count} минуту";
        }

        return !teens && last is >= 2 and <= 4 ? $"{count} минуты" : $"{count} минут";
    }

    public static string Duration(TimeSpan time)
    {
        var hours = (int)time.TotalHours;
        return hours > 0 ? $"{hours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    }

    public static string KeyName(GameKey key) => key switch
    {
        GameKey.Space => "Пробел",
        GameKey.One => "1",
        GameKey.Two => "2",
        GameKey.Three => "3",
        _ => key.ToString(),
    };

    public static string ActionName(AfkAction action) => action switch
    {
        AfkAction.RightClick => "Правый клик",
        AfkAction.LeftClick => "Левый клик",
        AfkAction.Key => "Клавиша",
        AfkAction.Walk => "Шаг W → S",
        AfkAction.Zoom => "Зум I → O",
        _ => action.ToString(),
    };

    public static string ActionSummary(AppSettings settings) =>
        settings.Action == AfkAction.Key ? $"Клавиша {KeyName(settings.Key)}" : ActionName(settings.Action);

    public static string ActionDescription(AppSettings settings) => settings.Action switch
    {
        AfkAction.Key => "Нажимает выбранную клавишу в окне Roblox.",
        AfkAction.Walk => "Нажимает W, а затем S в окне Roblox.",
        AfkAction.Zoom => "Нажимает I, а затем O в окне Roblox.",
        _ when settings.ClickPoint is not null => "Кликает в выбранную точку окна Roblox.",
        _ => "Кликает по центру окна Roblox.",
    };

    public static (string Title, string Text, bool IsWarning) Effect(AfkAction action) => action switch
    {
        AfkAction.LeftClick => ("Может задеть игру",
            "Левый клик может нажать кнопку на экране, выстрелить или ударить. Курсор сразу возвращается на место.", true),
        AfkAction.Key => ("Может задеть игру",
            "Клавиша нажимается по-настоящему: W, A, S, D двигают персонажа, Пробел прыгает, цифры меняют предмет. Если открыт чат, символ попадёт в него.", true),
        AfkAction.Walk => ("Персонаж двигается",
            "Персонаж делает короткий шаг вперёд и назад. Это может помочь в играх со своим таймером, но не гарантия. Рядом с препятствием он может немного сместиться. Если открыт чат, буквы попадут в него.", true),
        AfkAction.Zoom => ("Двигается только камера",
            "Камера немного приближается и отдаляется обратно, персонаж стоит на месте. Если открыт чат, буквы попадут в него.", false),
        _ => ("Не мешает игре",
            "Правый клик не нажимает кнопки, не стреляет и не двигает персонажа. Курсор сразу возвращается на место.", false),
    };

    public static string Activity(ActivityEntry entry) => entry.Code switch
    {
        ActivityCode.ActionDone => ActionDone(entry),
        ActivityCode.SkippedWhilePlaying => "Пропуск: идёт игра",
        ActivityCode.SwitchFailed => "Не удалось переключиться на Roblox",
        ActivityCode.WindowUnavailable => "Окно Roblox свёрнуто, разверни его",
        ActivityCode.PointSaved => "Точка сохранена",
        ActivityCode.PointPickCancelled => "Выбор точки отменён",
        ActivityCode.PointPickFailed => "Не удалось начать выбор точки",
        ActivityCode.RobloxNotRunning => "Сначала запусти Roblox",
        ActivityCode.PermissionMissing => "Сначала выдай разрешение",
        _ => "Что-то пошло не так, попробую в следующий раз",
    };

    private static string ActionDone(ActivityEntry entry)
    {
        var text = entry.Action switch
        {
            AfkAction.LeftClick => "Левый клик сделан",
            AfkAction.Key => $"Нажата клавиша {KeyName(entry.Key)}",
            AfkAction.Walk => "Шаг W → S сделан",
            AfkAction.Zoom => "Зум I → O сделан",
            _ => "Правый клик сделан",
        };
        return entry.WindowsTotal > 1 ? $"{text} (окон: {entry.WindowsDone} из {entry.WindowsTotal})" : text;
    }
}
