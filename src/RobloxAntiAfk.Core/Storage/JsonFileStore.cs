using System.Text.Json;
using System.Text.Json.Serialization;

namespace RobloxAntiAfk.Core.Storage;

public interface IDataStore<T>
{
    T Load();

    void Save(T value);
}

/// <summary>Хранит значение в JSON-файле. Повреждённый или отсутствующий файл заменяется значением по умолчанию.</summary>
public sealed class JsonFileStore<T>(string path, Func<T> createDefault) : IDataStore<T>
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public T Load()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? createDefault();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return createDefault();
        }
    }

    public void Save(T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Не сохранилось: приложение продолжит работать со значениями в памяти.
        }
    }
}
