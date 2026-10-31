using RobloxAntiAfk.Core.Domain;

namespace RobloxAntiAfk.Core.Services;

/// <summary>Порядок чтения и расположение подписи. Здесь только расчёты, поэтому всё проверяется тестами.</summary>
public static class TextLayout
{
    private const double SameRowShare = 0.5;
    private const double ParagraphGapShare = 0.9;
    private const int MinLetters = 2;

    /// <summary>
    /// Склеивает строки одного абзаца: переводчику нужно предложение целиком, а игра могла перенести его на две строки.
    /// Обрывки без букв, например счётчики монет, отбрасываются.
    /// </summary>
    public static string ToText(IReadOnlyList<RecognizedLine> lines)
    {
        var ordered = lines
            .Where(line => line.Text.Count(char.IsLetter) >= MinLetters)
            .OrderBy(line => line.Y)
            .ToList();
        ordered = SortRows(ordered);

        var paragraphs = new List<List<RecognizedLine>>();
        foreach (var line in ordered)
        {
            if (paragraphs.Count > 0 && Continues(paragraphs[^1][^1], line))
            {
                paragraphs[^1].Add(line);
            }
            else
            {
                paragraphs.Add([line]);
            }
        }

        return string.Join('\n', paragraphs.Select(paragraph => string.Join(' ', paragraph.Select(line => line.Text.Trim()))));
    }

    /// <summary>Сначала ниже, если места хватает, потому что диалоги обычно начинаются сверху. Иначе выше, иначе внутри окна.</summary>
    public static ScreenRect PlacePanel(ScreenRect area, ScreenRect window, double width, double height, double gap)
    {
        var fittedWidth = Math.Min(width, window.Width);
        var x = Math.Clamp(area.X, window.X, window.X + window.Width - fittedWidth);
        var fittedHeight = Math.Min(height, window.Height);
        double y;
        if (area.Y + area.Height + gap + fittedHeight <= window.Y + window.Height)
        {
            y = area.Y + area.Height + gap;
        }
        else if (area.Y - gap - fittedHeight >= window.Y)
        {
            y = area.Y - gap - fittedHeight;
        }
        else
        {
            y = window.Y + window.Height - fittedHeight;
        }

        return new ScreenRect(x, y, fittedWidth, fittedHeight);
    }

    public static bool Overlaps(ScreenRect first, ScreenRect second) =>
        first.X < second.X + second.Width && second.X < first.X + first.Width
        && first.Y < second.Y + second.Height && second.Y < first.Y + first.Height;

    /// <summary>Строки на одной высоте идут слева направо, даже если распознаватель вернул их иначе.</summary>
    private static List<RecognizedLine> SortRows(List<RecognizedLine> lines)
    {
        var sorted = new List<RecognizedLine>(lines.Count);
        var index = 0;
        while (index < lines.Count)
        {
            var first = lines[index];
            var row = lines.Skip(index).TakeWhile(line => line.Y - first.Y < first.Height * SameRowShare).ToList();
            sorted.AddRange(row.OrderBy(line => line.X));
            index += row.Count;
        }

        return sorted;
    }

    private static bool Continues(RecognizedLine previous, RecognizedLine next)
    {
        var gap = next.Y - (previous.Y + previous.Height);
        var overlapsHorizontally = next.X < previous.X + previous.Width && previous.X < next.X + next.Width;
        return gap >= -previous.Height * SameRowShare
            && gap < previous.Height * ParagraphGapShare
            && overlapsHorizontally
            && next.Y - previous.Y >= previous.Height * SameRowShare;
    }
}
