using System.Reflection;
using System.Runtime.InteropServices;
using RapidOcrNet;
using RobloxAntiAfk.Core.Abstractions;
using RobloxAntiAfk.Core.Domain;
using SkiaSharp;

namespace RobloxAntiAfk.Ocr;

/// <summary>
/// Распознавание на моделях PP-OCR. Работает без интернета и без языковых пакетов Windows.
/// Модели загружаются при первом использовании, потому что это занимает пару секунд и много памяти.
/// </summary>
public sealed class RapidTextRecognizer : ITextRecognizer, IDisposable
{
    private const string DetectionModel = "ch_PP-OCRv5_mobile_det.onnx";
    private const string ClassifierModel = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";
    private const string RecognitionModel = "latin_PP-OCRv5_rec_mobile_infer.onnx";
    private const string Dictionary = "ppocrv5_latin_dict.txt";
    private const float MinScore = 0.5f;

    /// <summary>Библиотекам распознавания они нужны, но на чистой Windows их может не быть, поэтому они лежат внутри приложения.</summary>
    private static readonly string[] CppRuntime = ["vcruntime140", "vcruntime140_1", "msvcp140", "msvcp140_1"];

    /// <summary>Мелкий шрифт игры модель читает хуже, поэтому маленькие области увеличиваем.</summary>
    private const int UpscaleBelowHeight = 400;
    private const int UpscaleFactor = 2;

    private static readonly RapidOcrOptions Options = RapidOcrOptions.Default with { DoAngle = false };

    private readonly object _gate = new();
    private readonly string _modelFolder;
    private RapidOcr? _engine;

    public RapidTextRecognizer(string? modelFolder = null)
    {
        var version = typeof(RapidTextRecognizer).Assembly.GetName().Version;
        _modelFolder = modelFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox AntiAFK",
            $"models-{version}");
    }

    public IReadOnlyList<RecognizedLine> Recognize(PixelImage image, CancellationToken cancellation)
    {
        lock (_gate)
        {
            var engine = _engine ??= LoadEngine();
            var scale = image.Height < UpscaleBelowHeight ? UpscaleFactor : 1;
            using var bitmap = ToBitmap(image, scale);
            var result = engine.Detect(bitmap, Options);

            var lines = new List<RecognizedLine>();
            foreach (var block in result.TextBlocks)
            {
                cancellation.ThrowIfCancellationRequested();
                if (block.Text is not { Length: > 0 } text || (block.CharScores is { Length: > 0 } scores && scores.Average() < MinScore))
                {
                    continue;
                }

                var left = block.BoxPoints.Min(point => point.X);
                var top = block.BoxPoints.Min(point => point.Y);
                var right = block.BoxPoints.Max(point => point.X);
                var bottom = block.BoxPoints.Max(point => point.Y);
                lines.Add(new RecognizedLine(
                    text,
                    (double)left / bitmap.Width,
                    (double)top / bitmap.Height,
                    (double)(right - left) / bitmap.Width,
                    (double)(bottom - top) / bitmap.Height));
            }

            return lines;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _engine?.Dispose();
            _engine = null;
        }
    }

    private RapidOcr LoadEngine()
    {
        ExtractModels();
        if (OperatingSystem.IsWindows())
        {
            // Уже загруженную копию система и подставит библиотекам распознавания вместо поиска по компьютеру.
            foreach (var library in CppRuntime)
            {
                NativeLibrary.TryLoad(library, typeof(RapidTextRecognizer).Assembly, null, out _);
            }
        }

        var engine = new RapidOcr();
        engine.InitModels(
            Path.Combine(_modelFolder, DetectionModel),
            Path.Combine(_modelFolder, ClassifierModel),
            Path.Combine(_modelFolder, RecognitionModel),
            Path.Combine(_modelFolder, Dictionary));
        return engine;
    }

    /// <summary>Библиотека читает модели только из файлов, а приложение хранит их внутри себя.</summary>
    private void ExtractModels()
    {
        Directory.CreateDirectory(_modelFolder);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in new[] { DetectionModel, ClassifierModel, RecognitionModel, Dictionary })
        {
            var target = Path.Combine(_modelFolder, name);
            using var source = assembly.GetManifestResourceStream($"models/{name}")
                ?? throw new FileNotFoundException($"В приложении нет модели {name}.");
            if (File.Exists(target) && new FileInfo(target).Length == source.Length)
            {
                continue;
            }

            // Сначала во временный файл: оборванная запись не должна оставить битую модель.
            var temporary = target + ".tmp";
            using (var output = File.Create(temporary))
            {
                source.CopyTo(output);
            }

            File.Move(temporary, target, overwrite: true);
        }
    }

    private static SKBitmap ToBitmap(PixelImage image, int scale)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var source = new SKBitmap();
        unsafe
        {
            fixed (byte* pixels = image.Bgra)
            {
                source.InstallPixels(info, (nint)pixels, info.RowBytes);
                // Цветовой формат приводим к тому, с которым работает библиотека.
                var converted = new SKBitmap(image.Width * scale, image.Height * scale, SKColorType.Rgba8888, SKAlphaType.Opaque);
                source.ScalePixels(converted, new SKSamplingOptions(SKCubicResampler.Mitchell));
                return converted;
            }
        }
    }
}
