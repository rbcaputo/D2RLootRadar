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
        = GetBoundingBox(iterator, PageIteratorLevel.Word, topCropPixels, out Rect upscaledBox);
      LabelRarity rarity = SampleRarity(upscaled, masked, upscaledBox, raw);

      results.Add(new(raw, raw.ToLowerInvariant(), confidence / 100f, box, rarity));
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
        = GetBoundingBox(iterator, PageIteratorLevel.TextLine, topCropPixels, out Rect upscaledBox);
      LabelRarity rarity = SampleRarity(upscaled, masked, upscaledBox, raw);

      results.Add(new(raw, normalized, confidence / 100f, box, rarity));
    }
    while (iterator.Next(PageIteratorLevel.TextLine));

    return results;
  }

  /// <summary>
  /// Maps a bounding box from the processed (cropped + upscaled) image's pixel space back to
  /// the original captured frame's pixel space:
  /// divide by the upscale factor, then re-add the crop's top offset.
  /// </summary>
  /// <param name="upscaledBox">
  /// The raw, unmapped box in upscaled pixel scene -
  /// the same coordinate space as the upscaled color frame and its text mask -
  /// so callers can sample label color from the exact region Tesseract recognized text in,
  /// without re-deriving it.
  /// </param>
  private static PixelRect GetBoundingBox(
    ResultIterator iterator,
    PageIteratorLevel level,
    int topCropPixels,
    out Rect upscaledBox
  )
  {
    if (!iterator.TryGetBoundingBox(level, out upscaledBox))
      return default;

    return new(
      X: (int)(upscaledBox.X1 / UpscaleFactor),
      Y: (int)(upscaledBox.Y1 / UpscaleFactor) + topCropPixels,
      Width: (int)(upscaledBox.Width / UpscaleFactor),
      Height: (int)(upscaledBox.Height / UpscaleFactor)
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
  /// Decides whether a detection is a Unique label by averaging the color of every
  /// foreground (text) pixel inside <paramref name="upscaledBox"/>.
  /// 
  /// <para>
  /// Samples from <paramref name="colorSource"/> - the still-colored, pre-mask upscaled frame -
  /// restricted to the pixels <paramref name="mask"/> flagged as text (see <see cref="AdaptiveTextMask"/>).
  /// This deliberately excludes the label panel's dark background from the average,
  /// which a naive whole-box sample would not.
  /// </para>
  /// 
  /// <para>
  /// Returns <see cref="LabelRarity.Unknown"/> for a degenerate box (clipped fully outside the frame)
  /// or when no foreground pixel is found within it -
  /// callers must not treat Unknown as equivalent to <see cref="LabelRarity.Unique"/> or
  /// <see cref="LabelRarity.Other"/>.
  /// </para>
  /// </summary>
  private static unsafe LabelRarity SampleRarity(
    Bitmap colorSource,
    Bitmap mask,
    Rect upscaledBox,
    string? debugLabel = null
  )
  {
    int x1 = Math.Max(0, upscaledBox.X1);
    int y1 = Math.Max(0, upscaledBox.Y1);
    int x2 = Math.Min(colorSource.Width, upscaledBox.X1 + upscaledBox.Width);
    int y2 = Math.Min(colorSource.Height, upscaledBox.Y1 + upscaledBox.Height);

    if (x2 <= x1 || y2 <= y1)
      return LabelRarity.Unknown;

    Rectangle region = new(x1, y1, x2 - x1, y2 - y1);

    BitmapData colorData = colorSource.LockBits(
      region,
      ImageLockMode.ReadOnly,
      PixelFormat.Format32bppArgb
    );
    BitmapData maskData = mask.LockBits(
      region,
      ImageLockMode.ReadOnly,
      PixelFormat.Format32bppArgb
    );

    try
    {
      byte* colorPtr = (byte*)colorData.Scan0;
      byte* maskPtr = (byte*)maskData.Scan0;

      long sumR = 0, sumG = 0, sumB = 0;
      int count = 0;

      for (int y = 0; y < region.Height; y++)
      {
        byte* colorRow = colorPtr + y * colorData.Stride;
        byte* maskRow = maskPtr + y * maskData.Stride;

        for (int x = 0; x < region.Width; x++)
        {
          // AdaptiveTextMask writes 0 (black) for text, 255 (white) for background.
          if (maskRow[x * 4] != 0)
            continue;

          sumB += colorRow[x * 4];
          sumG += colorRow[x * 4 + 1];
          sumR += colorRow[x * 4 + 2];

          count++;
        }
      }

      if (count == 0)
      {
#if DEBUG
        System.Diagnostics.Debug.WriteLine(
          $"[RarityDebug] \"{debugLabel}\" box=({x1},{y1},{x2 - x1}x{y2 - y1}) " +
          "no foreground pixels found -> Unknown"
        );
#endif

        return LabelRarity.Unknown;
      }

      byte avgR = (byte)(sumR / count);
      byte avgG = (byte)(sumG / count);
      byte avgB = (byte)(sumB / count);
      LabelRarity classified = LabelRarityClassifier.Classify(avgR, avgG, avgB);

#if DEBUG
      System.Diagnostics.Debug.WriteLine(
        $"[RarityDebug] \"{debugLabel}\" avgRGB=({avgR},{avgG},{avgB}) n={count} -> {classified}"
      );
#endif

      return classified;
    }
    finally
    {
      colorSource.UnlockBits(colorData);
      mask.UnlockBits(maskData);
    }
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
