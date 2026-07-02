using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Domain.Loot;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Tesseract;

namespace D2RLootRadar.Infrastructure.Ocr;

/// <summary>
/// Runs Tesseract OCR over a captured game frame and returns all
/// detected text tokens as <see cref="DetectionResult"/> values.
/// 
/// <para>
/// <strong>Preprocessing:</strong>
/// the raw PNG frame is upscaled 2x with nearest-neighbor interpolation before being handed to Tesseract.
/// This gives the LSTM engine larger, crisper letter forms without introducing the blurring that bicubic would cause.
/// </para>
/// 
/// <para>
/// <strong>PSM:</strong>
/// <c>SparseText</c> (11) is used because item labels are disconnected fragments scattered across the viewport -
/// there is no reading order or paragraph structure.
/// </para>
/// 
/// <para>
/// <strong>Thread safety:</strong>
/// <see cref="TesseractEngine"/> is not thread-safe; a <see cref="SemaphoreSlim"/> serializes concurrent callers.
/// In practice only one ALT-triggered pass runs at a time, so there is no throughput cost.
/// </para>
/// </summary>
public sealed class OcrService : IOcrService, IDisposable
{
  private readonly TesseractEngine _engine;
  private readonly SemaphoreSlim _lock = new(1, 1);

  /// <summary>
  /// Directory that contains <c>eng.traineddata</c>.
  /// Place the file from <see href="https://github.com/tesseract-ocr/tessdata_fast">tessdata_fast</see>
  /// next to the executable in a folder named <c>tessdata</c>.
  /// </summary>
  private const string TessDataPath = "tessdata";

  /// <summary>
  /// Factor applied by <see cref="Upscale"/> before OCR.
  /// See class remarks.
  /// </summary>
  private const float UpscaleFactor = 1.5f;

  /// <summary>
  /// Fraction of frame height cropped from the top (title bar) before OCR.
  /// </summary>
  private const double TopCropFraction = 0.03;

  /// <summary>
  /// Fraction of frame height cropped from the bottom (HUD) before OCR.
  /// </summary>
  private const double BottomCropFraction = 0.18;

  /// <summary>
  /// Initializes the Tesseract engine in LSTM-only mode and restricts recognition to
  /// the character set that can legally appear in a D2R item base name,
  /// which meaningfully reduces misreads on stylized in-game fonts.
  /// </summary>
  public OcrService()
  {
    _engine = new(TessDataPath, "eng", EngineMode.LstmOnly);

    // Restrict charset to what can appear in D2R item base names.
    _engine.SetVariable(
      "tessedit_char_whitelist",
      "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz '-"
    );
  }

  /// <summary>
  /// Detects all text tokens in <paramref name="imageData"/>.
  /// Returns an empty collection immediately for an empty frame, without touching the engine lock.
  /// Serialized against concurrent callers - see class remarks.
  /// </summary>
  /// <param name="imageData">A PNG-encoded frame, as produced by <see cref="IGameCaptureService"/>.</param>
  /// <param name="cToken">
  /// Cancels the wait for the engine lock. Does not interrupt an OCR pass already running.
  /// </param>
  /// <returns></returns>
  public async Task<IReadOnlyCollection<DetectionResult>> DetectAsync(
    byte[] imageData,
    CancellationToken cToken
  )
  {
    if (imageData.Length == 0)
      return [];

    cToken.ThrowIfCancellationRequested();

    await _lock.WaitAsync(cToken);

    try
    {
      return RunOcr(imageData);
    }
    finally
    {
      _lock.Release();
    }
  }

  // --- Private helpers -----

  /// <summary>
  /// Runs the full preprocessing + recognition pipeline for a single frame:
  /// crop → upscale → adaptive threshold → Tesseract, then two passes over the resulting page -
  /// word-level (for single-word item names) and line-level (for multi-word names).
  /// Must only be called while holding <see cref="_lock"/>.
  /// </summary>
  private List<DetectionResult> RunOcr(byte[] imageData)
  {
    using MemoryStream stream = new(imageData);
    using Bitmap source = new(stream);

    // Computed once here so the bounding-box transform below uses the
    // exact same offset CropGameArea applies internally.
    int topCropPixels = (int)(source.Height * TopCropFraction);

    using Bitmap cropped = CropGameArea(source);
    using Bitmap upscaled = Upscale(cropped);
    using Bitmap masked = AdaptiveTextMask(upscaled);

    List<DetectionResult> results = [];

    using Pix pix = ToBitmapPix(masked);
    using Page page = _engine.Process(pix, PageSegMode.SparseText);
    using ResultIterator iterator = page.GetIterator();

    // Pass 1 - word level: single-word item names (Monarch, Pike, Shako...)
    iterator.Begin();

    do
    {
      float confidence = iterator.GetConfidence(PageIteratorLevel.Word);
      string? raw = iterator.GetText(PageIteratorLevel.Word)?.Trim();
      if (string.IsNullOrWhiteSpace(raw) || raw.Length < 3)
        continue;

      PixelRect box
        = GetBoundingBox(iterator, PageIteratorLevel.Word, topCropPixels);

      results.Add(new(raw, raw.ToLowerInvariant(), confidence / 100f, box));
    }
    while (iterator.Next(PageIteratorLevel.Word));

    // Pass 2 - line level: multi-word item names (El Rune, Ber Rune, Hand Axe...)
    // Single-word lines are skipped - already captured above.
    iterator.Begin();

    do
    {
      float confidence = iterator.GetConfidence(PageIteratorLevel.TextLine);
      string? raw = iterator.GetText(PageIteratorLevel.TextLine)?.Trim();

      if (string.IsNullOrWhiteSpace(raw) || !raw.Contains(' '))
        continue;

      // Collapse any internal whitespace - Tesseract sometimes inserts exta
      // space between glyphs on the same label.
      string normalized
        = Regex.Replace(raw.ToLowerInvariant().Trim(), @"\s+", " ");
      PixelRect box
        = GetBoundingBox(iterator, PageIteratorLevel.TextLine, topCropPixels);

      results.Add(new(raw, normalized, confidence / 100f, box));
    }
    while (iterator.Next(PageIteratorLevel.TextLine));

    return results;
  }

  /// <summary>
  /// Maps a bounding box from the processed (cropped + upscaled) image's pixel space back to
  /// the original captured frame's pixel space:
  /// divide by the upscale factor, then re-add the crop's top offset.
  /// </summary>
  private static PixelRect GetBoundingBox(
    ResultIterator iterator,
    PageIteratorLevel level,
    int topCropPixels
  )
  {
    if (!iterator.TryGetBoundingBox(level, out Rect box))
      return default;

    return new(
      X: (int)(box.X1 / UpscaleFactor),
      Y: (int)(box.Y1 / UpscaleFactor) + topCropPixels,
      Width: (int)(box.Width / UpscaleFactor),
      Height: (int)(box.Height / UpscaleFactor)
    );
  }

  /// <summary>
  /// Crops the title bar (top 3%) and HUD (bottom 18%) from the captured frame.
  /// Neither region ever contains item floor labels.
  /// Reduces OCR surface area and eliminates persistent HUD noise tokens.
  /// </summary>
  private static Bitmap CropGameArea(Bitmap source)
  {
    int topCrop = (int)(source.Height * TopCropFraction);
    int bottomCrop = (int)(source.Height * BottomCropFraction);
    int newHeight = source.Height - topCrop - bottomCrop;

    if (newHeight <= 0)
      return new(source); // safety guard

    Rectangle region = new(0, topCrop, source.Width, newHeight);
    Bitmap cropped = new(source.Width, newHeight, PixelFormat.Format32bppArgb);

    using Graphics graphics = Graphics.FromImage(cropped);
    graphics.DrawImage(
      source,
      new Rectangle(0, 0, source.Width, newHeight),
      region,
      GraphicsUnit.Pixel
    );

    return cropped;
  }

  private static Bitmap Upscale(Bitmap source)
  {
    int width = (int)(source.Width * UpscaleFactor);
    int height = (int)(source.Height * UpscaleFactor);

    Bitmap result = new(width, height, PixelFormat.Format32bppArgb);

    using Graphics graphics = Graphics.FromImage(result);
    graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
    graphics.PixelOffsetMode = PixelOffsetMode.Half;
    graphics.DrawImage(source, 0, 0, width, height);

    return result;
  }

  /// <summary>
  /// Produces a binary mask via adaptive local thresholding.
  /// A pixel is classified as text (black) if its luminance exceeds its
  /// local neighborhood mean by at least <see cref="Factor"/>.
  /// 
  /// This correctly captures both white labels (very bright on dark dungeon) and
  /// tan unique-item labels (warm-bright on warm-dim stone floor) without
  /// requiring per-rarity color calibration.
  /// 
  /// Uses an integral image for O(1) neighborhood sums -
  /// the full pass over a 2x 1080p frame costs well under 1 ms.
  /// </summary>
  private static unsafe Bitmap AdaptiveTextMask(Bitmap source)
  {
    int width = source.Width;
    int height = source.Height;

    // 1. Extract luminance
    float[] luminance = new float[width * height];

    BitmapData srcData = source.LockBits(
      new(0, 0, width, height),
      ImageLockMode.ReadOnly,
      PixelFormat.Format32bppArgb
    );

    try
    {
      byte* src = (byte*)srcData.Scan0;

      for (int y = 0; y < height; y++)
      {
        byte* row = src + y * srcData.Stride;

        for (int x = 0; x < width; x++)
        {
          byte b = row[x * 4];
          byte g = row[x * 4 + 1];
          byte r = row[x * 4 + 2];
          luminance[y * width + x] = r * 0.299f + g * 0.587f + b * 0.114f;
        }
      }
    }
    finally
    {
      source.UnlockBits(srcData);
    }

    // 2. Build integral image
    int iw = width + 1;
    double[] integral = new double[iw * (height + 1)];

    for (int y = 0; y < height; y++)
    {
      double rowSum = 0;

      for (int x = 0; x < width; x++)
      {
        rowSum += luminance[y * width + x];
        integral[(y + 1) * iw + (x + 1)]
          = rowSum + integral[y * iw + (x + 1)];
      }
    }

    // 3. Threshold: text if pixel > local mean x Factor
    // Radius 25 at 2x gives a ~51x51 nighborhood - large enough to
    // represent background context around each glyph,
    // small enough to follow local lighting changes across the scene.
    const int Radius = 25;
    const float Factor = 1.30f; // 15% brighter than neighborhood
    const float MinLum = 100f; // absolute floor - dark pixels are never text

    Bitmap mask = new(width, height, PixelFormat.Format32bppArgb);
    BitmapData dstData = mask.LockBits(
      new(0, 0, width, height),
      ImageLockMode.WriteOnly,
      PixelFormat.Format32bppArgb
    );

    try
    {
      byte* dst = (byte*)dstData.Scan0;

      for (int y = 0; y < height; y++)
      {
        byte* dstRow = dst + y * dstData.Stride;

        for (int x = 0; x < width; x++)
        {
          int x1 = Math.Max(0, x - Radius);
          int y1 = Math.Max(0, y - Radius);
          int x2 = Math.Min(width - 1, x + Radius);
          int y2 = Math.Min(height - 1, y + Radius);

          double area = (double)(x2 - x1 + 1) * (y2 - y1 + 1);
          double sum = integral[(y2 + 1) * iw + x2 + 1] -
                       integral[y1 * iw + x2 + 1] -
                       integral[(y2 + 1) * iw + x1] +
                       integral[y1 * iw + x1];

          float pixelLum = luminance[y * width + x];
          bool isText
            = pixelLum > MinLum && pixelLum > (sum / area) * Factor;
          byte value = isText ? (byte)0 : (byte)255;
          int px = x * 4;

          dstRow[px] = value; // B
          dstRow[px + 1] = value; // G
          dstRow[px + 2] = value; // R
          dstRow[px + 3] = 255; // A (fully opaque)
        }
      }
    }
    finally
    {
      mask.UnlockBits(dstData);
    }

    return mask;
  }

  /// <summary>
  /// Converts a <see cref="Bitmap"/> to a Tesseract <see cref="Pix"/>.
  /// 
  /// <para>
  /// The source is always the output of <see cref="AdaptiveTextMask"/>,
  /// which writes each channel of every pixel to the same value (0 or 255) -
  /// i.e. it is already a binary black/white image.
  /// Re-deriving luminance with the standard RGB weights here would be redundant work
  /// (the weighted sum of three equal channels always collapses back to that same channel value),
  /// so this copies the blue channel directly instead of recomputing it.
  /// </para>
  /// </summary>
  private static unsafe Pix ToBitmapPix(Bitmap bitmap)
  {
    BitmapData bmpData = bitmap.LockBits(
      new(0, 0, bitmap.Width, bitmap.Height),
      ImageLockMode.ReadOnly,
      PixelFormat.Format32bppArgb // forces BGRA layout regardless of source format
    );

    try
    {
      int width = bitmap.Width;
      int height = bitmap.Height;

      Pix pix = Pix.Create(width, height, depth: 8); // 8bpp grayscale
      PixData pixData = pix.GetData();

      byte* source = (byte*)bmpData.Scan0;

      for (int y = 0; y < height; y++)
      {
        // WordsPerLine is stride in 32-bit words, not bytes.
        uint* dstRow = (uint*)pixData.Data + y * pixData.WordsPerLine;
        byte* srcRow = source + y * bmpData.Stride;

        for (int x = 0; x < width; x++)
        {
          // GDI Format32bppArgb memory layout: [B][G][R][A]
          // B/G/R are already equal on this binary mask, so any one channel is the gray value.
          byte gray = srcRow[x * 4];

          PixData.SetDataByte(dstRow, x, gray);
        }
      }

      return pix;
    }
    finally
    {
      bitmap.UnlockBits(bmpData);
    }
  }

  public void Dispose()
  {
    _engine.Dispose();
    _lock.Dispose();
  }
}
