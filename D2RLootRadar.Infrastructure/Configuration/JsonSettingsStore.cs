using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Application.Settings;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace D2RLootRadar.Infrastructure.Configuration;

/// <summary>
/// Persists user settings as JSON alongside the executable.
/// </summary>
public sealed class JsonSettingsStore(ILogger<JsonSettingsStore> logger) : ISettingsStore
{
  private readonly ILogger<JsonSettingsStore> _logger = logger;

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
    catch (JsonException ex)
    {
      _logger.LogWarning(
        ex,
        "settings.json is corrupt or unreadable at '{Path}'. Falling back to defaults.",
        FilePath
      );

      return new();
    }
    catch (IOException ex)
    {
      _logger.LogWarning(
        ex,
        "Failed to read settings.json at '{Path}' Fallig back to defaults.",
        FilePath
      );

      return new();
    }
  }

  /// <summary>
  /// Persists the given settings to disk, overwriting any previous file.
  /// Logs (rather than silently swallowing or crashing the app) if the write fails
  /// e.g. the app is running from a read-only location, or the disk is full.
  /// </summary>
  /// <param name="settings"></param>
  public void Save(UserSettings settings)
  {
    ArgumentNullException.ThrowIfNull(settings);

    string json
      = JsonSerializer.Serialize(settings, SerializerOptions);

    try
    {
      File.WriteAllText(FilePath, json);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
      _logger.LogError(
        ex,
        "Failed to write settings.json at '{Path}. Changes will not persist to the next launch.",
        FilePath
      );
    }
  }
}
