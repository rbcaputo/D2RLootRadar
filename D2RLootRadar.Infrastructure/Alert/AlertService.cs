using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Application.Settings;
using System.Media;

namespace D2RLootRadar.Infrastructure.Alert;

/// <summary>
/// Emits an audible alert as a generated sinw-wave tone
/// 
/// The alert is synthesized as 16-bit PCM and played via SoundPlayer.
/// Volume is literally the amplitude of the generated waveform:
/// 0 produces true silence with no playback attempt, not a quiet beep.
/// 
/// A short linear fade-in/out (5 ms) avoids the audible "click" a
/// hard-edged tone envelope produces at start and end.
/// 
/// The beep is fired on a thread-pool thread so it never blocks the UI or the detection pipeline.
/// An interlocked guard prevents overlapping playback when two matches arrive in quick succession.
/// </summary>
public sealed class AlertService(ISettingsStore settingsStore) : IAlertService
{
  private readonly ISettingsStore _settingsStore = settingsStore;
  private int _isPlaying; // interlocked: 0 = idle, 1 = playing

  private const int SampleRateHz = 44_100;
  private const int FadeMs = 5;

  /// <inheritdoc />
  public Task AlertAsync(CancellationToken cToken)
    => AlertAsync(_settingsStore.Load(), cToken);

  /// <inheritdoc />
  public Task AlertAsync(UserSettings settings, CancellationToken cToken)
    => PlayAsync(
      settings.BeepFrequencyHz,
      settings.BeepVolume,
      settings.BeepDurationMs
    );

  /// <inheritdoc />
  public Task PlayTestSoundAsync(
    int frequencyHz,
    int volumePercent,
    CancellationToken cToken
  ) =>
    // Fixed short duration - the user is previewing pitch and loudness,
    // not the actual production alert length.
    PlayAsync(frequencyHz, volumePercent, durationMs: 300);

  /// <summary>
  /// Synthesizes and plays the tone on a thread-pool thread.
  /// Returns immediately - playback happens fire-and-forget, since the caller
  /// (detection pipeline or UI) should never wait on audio I/O.
  /// Silently no-ops on mute, or if a beep is already in flight.
  /// </summary>
  private Task PlayAsync(int frequencyHz, int volumePercent, int durationMs)
  {
    if (volumePercent <= 0)
      return Task.CompletedTask; // mute - true silence, no playback attempt

    if (Interlocked.CompareExchange(ref _isPlaying, 1, 0) != 0)
      return Task.CompletedTask; // already playing - skip overlapping alert

    byte[] wav = GenerateToneWav(frequencyHz, volumePercent, durationMs);

    ThreadPool.QueueUserWorkItem(_ =>
    {
      try
      {
        using MemoryStream stream = new(wav);
        using SoundPlayer player = new(stream);

        player.PlaySync(); // blocks this pool thread only, not the caller
      }
      catch
      {
        // suppress - some environments lack audio ouyput
      }
      finally
      {
        Interlocked.Exchange(ref _isPlaying, 0);
      }
    });

    return Task.CompletedTask;
  }

  /// <summary>
  /// Synthesizes a mono 16-bit PCM sine wave with a short fade envelope,
  /// wrapped in a minimal valid WAV header.
  /// </summary>
  private static byte[] GenerateToneWav(
    int frequencyHz,
    int volumePercent,
    int durationMs
  )
  {
    int sampleCount = SampleRateHz * durationMs / 1_000;
    int fadeSamples
      = Math.Min(sampleCount / 2, SampleRateHz * FadeMs / 1_000);
    double amplitude
      = Math.Clamp(volumePercent, 0, 100) / 100.0 * short.MaxValue;

    short[] samples = new short[sampleCount];

    for (int i = 0; i < sampleCount; i++)
    {
      double t = (double)i / SampleRateHz;
      double wave = Math.Sin(2 * Math.PI * frequencyHz * t);
      double envelope = 1.0;

      if (i < fadeSamples)
        envelope = (double)i / fadeSamples;
      else if (i > sampleCount - fadeSamples)
        envelope = (double)(sampleCount - i) / fadeSamples;

      samples[i] = (short)(wave * amplitude * envelope);
    }

    return BuildWavFile(samples);
  }

  /// <summary>
  /// Wraps raw PCM samples in a minimal 44-byte canonical WAV header
  /// (RIFF/WAVE, single "fmt " chunk, single "data" chunk).
  /// </summary>
  private static byte[] BuildWavFile(short[] samples)
  {
    const int channels = 1;
    const int bitsPerSample = 16;

    int byteRate = SampleRateHz * channels * bitsPerSample / 8;
    int blockAlign = channels * bitsPerSample / 8;
    int dataSize = samples.Length * blockAlign;

    using MemoryStream stream = new();
    using BinaryWriter writer = new(stream);

    writer.Write("RIFF"u8);
    writer.Write(36 + dataSize);
    writer.Write("WAVE"u8);

    writer.Write("fmt "u8);
    writer.Write(16);
    writer.Write((short)1); // PCM
    writer.Write((short)channels);
    writer.Write(SampleRateHz);
    writer.Write(byteRate);
    writer.Write((short)blockAlign);
    writer.Write((short)bitsPerSample);

    writer.Write("data"u8);
    writer.Write(dataSize);

    foreach (short sample in samples)
      writer.Write(sample);

    writer.Flush();

    return stream.ToArray();
  }
}
