using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using RobloxAntiAfk.Platform.Mac.Native;

namespace RobloxAntiAfk.Platform.Mac;

/// <summary>
/// Снимок делает системная утилита screencapture, текст распознаёт встроенный в macOS Vision.
/// Процесс Roblox при этом не затрагивается. Снимок лежит на диске только на время распознавания.
/// </summary>
internal sealed class MacScreenTextReader : IScreenTextReader
{
    private const string SettingsUrl = "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture";

    public bool IsPermissionGranted => CoreGraphics.CGPreflightScreenCaptureAccess() != 0;

    public void RequestPermission() => CoreGraphics.CGRequestScreenCaptureAccess();

    public void OpenPermissionSettings() => Process.Start("open", SettingsUrl)?.Dispose();

    public async Task<TextReadResult> ReadAsync(ScreenRect area, CancellationToken cancellation)
    {
        var capture = await CaptureAsync(area, cancellation);
        if (capture.Snapshot is not { } snapshot)
        {
            return TextReadResult.Of(capture.Status, capture.Detail);
        }

        using (snapshot)
        {
            return await ReadAsync(snapshot, area, cancellation);
        }
    }

    public async Task<TextReadResult> ReadAsync(ScreenSnapshot snapshot, ScreenRect area, CancellationToken cancellation)
    {
        try
        {
            var path = snapshot.ImagePath ?? throw new InvalidOperationException("В снимке нет изображения.");
            var region = WindowArea.FromScreen(area, snapshot.Bounds);
            return new TextReadResult(TextReadStatus.Success, await Task.Run(() => MacVision.Recognize(path, region), CancellationToken.None));
        }
        catch (Exception exception)
        {
            return TextReadResult.Of(TextReadStatus.Failed, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    public async Task<SnapshotResult> CaptureAsync(ScreenRect area, CancellationToken cancellation)
    {
        if (!IsPermissionGranted)
        {
            return new SnapshotResult(TextReadStatus.PermissionMissing, null);
        }

        var path = Path.Combine(Path.GetTempPath(), $"roblox-antiafk-{Guid.NewGuid():N}.png");
        try
        {
            using var capture = Process.Start(new ProcessStartInfo("/usr/sbin/screencapture", CaptureArguments(area, path))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (capture is null)
            {
                return new SnapshotResult(TextReadStatus.Failed, null, "Не удалось запустить screencapture.");
            }

            await capture.WaitForExitAsync(cancellation);
            if (capture.ExitCode == 0 && File.Exists(path))
            {
                return new SnapshotResult(TextReadStatus.Success, new ScreenSnapshot(area, null, path));
            }

            File.Delete(path);
            return new SnapshotResult(TextReadStatus.Failed, null, $"screencapture завершился с кодом {capture.ExitCode}.");
        }
        catch (Exception exception)
        {
            File.Delete(path);
            return new SnapshotResult(TextReadStatus.Failed, null, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    /// <summary>-x выключает звук затвора, -R задаёт область в точках экрана.</summary>
    internal static IReadOnlyList<string> CaptureArguments(ScreenRect area, string path) =>
    [
        "-x",
        "-t",
        "png",
        "-R",
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Round(area.X)},{Math.Round(area.Y)},{Math.Max(1, Math.Round(area.Width))},{Math.Max(1, Math.Round(area.Height))}"),
        path,
    ];
}

internal static class MacVision
{
    private const float MinConfidence = 0.3f;
    private const long AccurateRecognition = 0;

    private static readonly nint Framework = NativeLibrary.Load("/System/Library/Frameworks/Vision.framework/Vision");

    /// <param name="imagePath">Снимок на диске.</param>
    /// <param name="region">Какую часть снимка читать. Результат считается в долях этой части.</param>
    public static IReadOnlyList<RecognizedLine> Recognize(string imagePath, WindowArea? region = null)
    {
        GC.KeepAlive(Framework);
        var pool = ObjC.objc_autoreleasePoolPush();
        var text = CoreFoundation.CreateString(imagePath);
        var language = CoreFoundation.CreateString("en-US");
        nint request = 0;
        nint imageHandler = 0;
        try
        {
            var url = ObjC.Send(ObjC.Class("NSURL"), "fileURLWithPath:", text);
            var options = ObjC.Send(ObjC.Class("NSDictionary"), "dictionary");
            imageHandler = ObjC.Send(ObjC.Send(ObjC.Class("VNImageRequestHandler"), "alloc"), "initWithURL:options:", url, options);
            request = ObjC.Send(ObjC.Send(ObjC.Class("VNRecognizeTextRequest"), "alloc"), "init");
            ObjC.Send(request, "setRecognitionLevel:", (nint)AccurateRecognition);
            ObjC.Send(request, "setUsesLanguageCorrection:", 1);
            ObjC.Send(request, "setRecognitionLanguages:", ObjC.Send(ObjC.Class("NSArray"), "arrayWithObject:", language));

            if (region is { } part)
            {
                // У Vision начало координат внизу слева.
                ObjC.SetRegionOfInterest(request, new CGRect { X = part.X, Y = 1 - part.Y - part.Height, Width = part.Width, Height = part.Height });
            }

            var requests = ObjC.Send(ObjC.Class("NSArray"), "arrayWithObject:", request);
            if (ObjC.Send(imageHandler, "performRequests:error:", requests, 0) == 0)
            {
                throw new InvalidOperationException("Vision не смог прочитать снимок.");
            }

            return ReadResults(ObjC.Send(request, "results"));
        }
        finally
        {
            if (request != 0)
            {
                ObjC.Send(request, "release");
            }

            if (imageHandler != 0)
            {
                ObjC.Send(imageHandler, "release");
            }

            CoreFoundation.CFRelease(language);
            CoreFoundation.CFRelease(text);
            ObjC.objc_autoreleasePoolPop(pool);
        }
    }

    private static List<RecognizedLine> ReadResults(nint results)
    {
        var lines = new List<RecognizedLine>();
        var count = results == 0 ? 0 : ObjC.SendLong(results, "count");
        for (long index = 0; index < count; index++)
        {
            var observation = ObjC.SendIndex(results, "objectAtIndex:", index);
            var candidates = ObjC.Send(observation, "topCandidates:", 1);
            if (ObjC.SendLong(candidates, "count") == 0)
            {
                continue;
            }

            var best = ObjC.SendIndex(candidates, "objectAtIndex:", 0);
            if (ObjC.SendFloat(best, "confidence") < MinConfidence || ObjC.ToText(ObjC.Send(best, "string")) is not { Length: > 0 } value)
            {
                continue;
            }

            // У Vision начало координат внизу слева, а в приложении вверху слева.
            var box = ObjC.SendRect(observation, "boundingBox");
            lines.Add(new RecognizedLine(value, box.X, 1 - box.Y - box.Height, box.Width, box.Height));
        }

        return lines;
    }
}

/// <summary>Чтобы подпись была видна поверх Roblox на весь экран, macOS требует убрать значок из Dock на это время.</summary>
internal sealed class MacOverlaySupport : IOverlaySupport
{
    private const nint StatusWindowLevel = 25;
    private const nint CanJoinAllSpaces = 1 << 0;
    private const nint Stationary = 1 << 4;
    private const nint IgnoresCycle = 1 << 6;
    private const nint FullScreenAuxiliary = 1 << 8;
    private const nint RegularPolicy = 0;
    private const nint AccessoryPolicy = 1;

    private bool _isAccessory;

    public void Prepare(nint handle, bool aboveFullScreen)
    {
        var pool = ObjC.objc_autoreleasePoolPush();
        try
        {
            // Avalonia может отдать и само окно, и его содержимое.
            var window = ObjC.Responds(handle, "window") ? ObjC.Send(handle, "window") : handle;
            ObjC.Send(window, "setIgnoresMouseEvents:", 1);
            ObjC.Send(window, "setHasShadow:", 0);
            ObjC.Send(window, "setLevel:", StatusWindowLevel);
            ObjC.Send(window, "setCollectionBehavior:", CanJoinAllSpaces | Stationary | IgnoresCycle | FullScreenAuxiliary);
            if (aboveFullScreen != _isAccessory)
            {
                SetPolicy(aboveFullScreen ? AccessoryPolicy : RegularPolicy);
                _isAccessory = aboveFullScreen;
            }
        }
        finally
        {
            ObjC.objc_autoreleasePoolPop(pool);
        }
    }

    public void Release()
    {
        if (_isAccessory)
        {
            SetPolicy(RegularPolicy);
            _isAccessory = false;
        }
    }

    public bool IsClickThrough(nint handle, ScreenPoint point)
    {
        var window = ObjC.Responds(handle, "window") ? ObjC.Send(handle, "window") : handle;
        return ObjC.SendBool(window, "ignoresMouseEvents");
    }

    private static void SetPolicy(nint policy)
    {
        var application = ObjC.Send(ObjC.Class("NSApplication"), "sharedApplication");
        ObjC.Send(application, "setActivationPolicy:", policy);
    }
}
