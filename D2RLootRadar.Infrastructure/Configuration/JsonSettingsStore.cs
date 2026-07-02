using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Application.Settings;
using System.Text.Json;

namespace D2RLootRadar.Infrastructure.Configuration;

/// <summary>
/// Persists user settings as JSON alongside the executable.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
  private static readonly string FilePath
    = Path.Combine(AppContext.BaseDirectory, "settings.json");
  private static readonly JsonSerializerOptions SerializerOptions = new()
  {
    WriteIndented = true
  };

  /// <summary>
  /// Reads and deserializes <c>settings.json</c>.
  /// Falls back to <see cref="UserSettings"/> defaults - rather than throwing -
  /// both when the file doesn't exist yet and then it exists but fails to parse
  /// (e.g. truncated by a crash mid-write), since a corrupt settings file should never be
  /// able to prevent the app from starting.
  /// </summary>
  public UserSettings Load()
  {
    if (!File.Exists(FilePath))
      return new();

    try
    {
      string json = File.ReadAllText(FilePath);

      return JsonSerializer.Deserialize<UserSettings>(json, SerializerOptions)
        ?? new();
    }
    catch (JsonException)
    {
      return new();
    }
    catch (IOException)
    {
      return new();
    }
  }

  /// <inheritdoc />
  public void Save(UserSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    string json
      = JsonSerializer.Serialize(settings, SerializerOptions);

    File.WriteAllText(FilePath, json);
  }
}
