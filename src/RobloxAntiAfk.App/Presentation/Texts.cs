using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Core.Localization;

namespace RobloxAntiAfk.App.Presentation;

/// <summary>Все тексты, которые зависят от состояния.</summary>
public static class Texts
{
    public static string Minutes(int count) => Plural(count, "минуту", "минуты", "минут", "minute", "minutes");

    public static string Actions(int count) => Plural(count, "действие", "действия", "действий", "action", "actions");

    private static string Plural(int count, string one, string few, string many, string englishOne, string englishMany)
    {
        if (L.Current == AppLanguage.English)
        {
            return $"{count} {(count == 1 ? englishOne : englishMany)}";
        }

        var last = count % 10;
        var teens = count % 100 is >= 11 and <= 14;
        if (!teens && last == 1)
        {
            return $"{count} {one}";
        }

        return $"{count} {(!teens && last is >= 2 and <= 4 ? few : many)}";
    }

    public static string Duration(TimeSpan time)
    {
        var hours = (int)time.TotalHours;
        return hours > 0 ? $"{hours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
    }

    public static string KeyName(GameKey key) => key switch
    {
        GameKey.Space => L.T("Пробел"),
        GameKey.One => "1",
        GameKey.Two => "2",
        GameKey.Three => "3",
        GameKey.Four => "4",
        GameKey.Five => "5",
        GameKey.Six => "6",
        GameKey.Seven => "7",
        GameKey.Eight => "8",
        GameKey.Nine => "9",
        GameKey.Zero => "0",
        _ => key.ToString(),
    };

    public static string ActionName(AfkAction action) => action switch
    {
        AfkAction.RightClick => L.T("Правый клик"),
        AfkAction.LeftClick => L.T("Левый клик"),
        AfkAction.Key => L.T("Клавиша"),
        AfkAction.Walk => L.T("Шаг W → S"),
        AfkAction.Zoom => L.T("Зум I → O"),
        AfkAction.Macro => L.T("Свой макрос"),
        _ => action.ToString(),
    };

    public static string ActionSummary(AppSettings settings) =>
        settings.Action == AfkAction.Key ? L.F("Клавиша {0}", KeyName(settings.Key)) : ActionName(settings.Action);

    public static string ActionDescription(AppSettings settings) => settings.Action switch
    {
        AfkAction.Key => L.T("Нажимает выбранную клавишу в окне Roblox."),
        AfkAction.Walk => L.T("Нажимает W, а затем S в окне Roblox."),
        AfkAction.Zoom => L.T("Нажимает I, а затем O в окне Roblox."),
        AfkAction.Macro => L.T("Повторяет записанные тобой нажатия и движения в окне Roblox."),
        _ when settings.ClickPoint is not null => L.T("Кликает в выбранную точку окна Roblox."),
        _ => L.T("Кликает по центру окна Roblox."),
    };

    public static (string Title, string Text, bool IsWarning) Effect(AfkAction action) => action switch
    {
        AfkAction.LeftClick => (L.T("Может задеть игру"),
            L.T("Левый клик может нажать кнопку на экране, выстрелить или ударить. Курсор сразу возвращается на место."), true),
        AfkAction.Key => (L.T("Может задеть игру"),
            L.T("Клавиша нажимается по-настоящему: W, A, S, D двигают персонажа, Пробел прыгает, цифры меняют предмет. Если открыт чат, символ попадёт в него."), true),
        AfkAction.Walk => (L.T("Персонаж двигается"),
            L.T("Персонаж делает короткий шаг вперёд и назад. Это может помочь в играх со своим таймером, но не гарантия. Рядом с препятствием он может немного сместиться. Если открыт чат, буквы попадут в него."), true),
        AfkAction.Zoom => (L.T("Двигается только камера"),
            L.T("Камера немного приближается и отдаляется обратно, персонаж стоит на месте. Если открыт чат, буквы попадут в него."), false),
        AfkAction.Macro => (L.T("Может задеть игру"),
            L.T("Запись повторяется как есть. Если персонаж стоит в другом месте или открыт чат, результат будет другим. Поворот камеры может повториться неточно."), true),
        _ => (L.T("Не мешает игре"),
            L.T("Правый клик не нажимает кнопки, не стреляет и не двигает персонажа. Курсор сразу возвращается на место."), false),
    };

    public static string Activity(ActivityEntry entry) => entry.Code switch
    {
        ActivityCode.ActionDone => ActionDone(entry),
        ActivityCode.SkippedWhilePlaying => L.T("Пропуск: идёт игра"),
        ActivityCode.SwitchFailed => L.T("Не удалось переключиться на Roblox"),
        ActivityCode.WindowUnavailable => L.T("Окно Roblox свёрнуто, разверни его"),
        ActivityCode.PointSaved => L.T("Точка сохранена"),
        ActivityCode.PointPickCancelled => L.T("Выбор точки отменён"),
        ActivityCode.PointPickFailed => L.T("Не удалось начать выбор точки"),
        ActivityCode.RobloxNotRunning => L.T("Сначала запусти Roblox"),
        ActivityCode.PermissionMissing => L.T("Сначала выдай разрешение"),
        ActivityCode.CharacterReset => L.T("Персонаж сброшен"),
        ActivityCode.MacroSaved => L.T("Макрос записан"),
        ActivityCode.MacroEmpty => L.T("В окне Roblox ничего не нажато, макрос не изменён"),
        ActivityCode.MacroMissing => L.T("Сначала запиши макрос в настройках"),
        ActivityCode.MacroRecordFailed => L.T("Не удалось начать запись"),
        ActivityCode.RobloxClosed => L.T("Окно Roblox закрылось"),
        ActivityCode.Disconnected => L.T("Roblox отключился от сервера"),
        ActivityCode.Reconnecting => L.T("Захожу в игру снова"),
        ActivityCode.Reconnected => L.T("Снова в игре"),
        ActivityCode.ReconnectGaveUp => L.T("Не получилось зайти за 5 попыток"),
        _ => L.T("Что-то пошло не так, попробую в следующий раз"),
    };

    public static string TranslationStatusText(TranslationSnapshot snapshot) => snapshot.Status switch
    {
        TranslationStatus.Disabled => L.T("Перевод выключен."),
        TranslationStatus.NeedArea => L.T("Сначала выбери область."),
        TranslationStatus.Ready => L.F("Готово. Нажми {0} в игре, когда появится текст.", snapshot.HotkeyLabel),
        TranslationStatus.ReadyToSelect => L.F("Готово. Нажми {0} в игре и обведи текст.", snapshot.HotkeyLabel),
        TranslationStatus.SelectingArea => L.T("Жду, пока ты обведёшь текст."),
        TranslationStatus.Reading => L.T("Читаю текст..."),
        TranslationStatus.Shown => L.F("Перевод показан. Чтобы убрать, кликни левой или правой кнопкой в окне Roblox или нажми {0}.", snapshot.HotkeyLabel),
        TranslationStatus.ShownOriginal => L.T("Текст прочитан, но перевод сейчас недоступен. Проверь интернет."),
        TranslationStatus.NoText => L.T("В выбранной области текст не найден."),
        TranslationStatus.AreaTooSmall => L.T("Область слишком маленькая, выбери её заново."),
        TranslationStatus.RobloxMissing => L.T("Сначала запусти Roblox."),
        TranslationStatus.RobloxNotInFront => L.T("Не удалось вывести Roblox вперёд. Открой его и нажми ещё раз."),
        TranslationStatus.InputPermissionMissing => L.T("Сначала выдай разрешение «Универсальный доступ»."),
        TranslationStatus.ScreenPermissionMissing => L.T("Нужно разрешение «Запись экрана»: Системные настройки → Конфиденциальность и безопасность → Запись экрана. Включи BloxPilot и открой приложение заново."),
        TranslationStatus.EmptyCapture => L.T("Снимок окна получился пустым. Переключи Roblox в оконный режим и попробуй ещё раз."),
        TranslationStatus.SelectionFailed => L.T("Не удалось начать выбор области."),
        _ => L.T("Не удалось прочитать текст."),
    };

    public static bool IsTranslationProblem(TranslationStatus status) => status is not (TranslationStatus.Disabled
        or TranslationStatus.NeedArea
        or TranslationStatus.Ready
        or TranslationStatus.ReadyToSelect
        or TranslationStatus.SelectingArea
        or TranslationStatus.Reading
        or TranslationStatus.Shown);

    /// <summary>Только то, ради чего стоит написать в Telegram.</summary>
    public static bool IsWorthNotifying(ActivityCode code) => code is ActivityCode.RobloxClosed
        or ActivityCode.Disconnected
        or ActivityCode.Reconnecting
        or ActivityCode.Reconnected
        or ActivityCode.ReconnectGaveUp;

    private static string ActionDone(ActivityEntry entry)
    {
        var text = entry.Action switch
        {
            AfkAction.LeftClick => L.T("Левый клик сделан"),
            AfkAction.Key => L.F("Нажата клавиша {0}", KeyName(entry.Key)),
            AfkAction.Walk => L.T("Шаг W → S сделан"),
            AfkAction.Zoom => L.T("Зум I → O сделан"),
            AfkAction.Macro => L.T("Макрос выполнен"),
            _ => L.T("Правый клик сделан"),
        };
        return entry.WindowsTotal > 1 ? L.F("{0} (окон: {1} из {2})", text, entry.WindowsDone, entry.WindowsTotal) : text;
    }
}
