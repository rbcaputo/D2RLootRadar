using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Domain.Loot;
using D2RLootRadar.Infrastructure.Processes;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace D2RLootRadar.Infrastructure.Capture;

/// <summary>
/// Captures the D2R game window using <c>PrintWindow(PW_RENDERFULLCONTENT)</c>,
/// which asks the DWM compositor to blit the DirectX swap-chain into a GDI DC.
/// 
/// Returns the frame as PNG-encoded bytes so the OCR service receives a lossless,
/// self-contained image with no temp-file I/O.
/// 
/// <para>
/// <strong>tThis method is synchronous under the hood.</strong>
/// Window lookup, PrintWindow, and PNG encoding are all CPU-bound GDI/Win32 calls with no
/// genuine I/O to await - the is no thread hand-off happening here despite the
/// <see cref="Task{TResult}"/> return type.
/// It returns a <c>Task</c> only to satisfy <see cref="IGameCaptureService"/>, so callers can
/// await it uniformly alongside the other (actually asynchronous) pipeline stages.
/// Callers that need this off the calling thread should wrap the call in
/// <see cref="Task.Run(Func{Task})"/> themselves.
/// </para>
/// </summary>
public sealed class GameCaptureService : IGameCaptureService
{
  private static readonly string[] ProcessNames
    = D2RProcess.Names;

  /// <inheritdoc />
  public Task<CaptureFrame> CaptureAsync(CancellationToken cToken)
  {
    cToken.ThrowIfCancellationRequested();

    IntPtr hwnd = FindD2RWindow();
    if (hwnd == IntPtr.Zero)
      return Task.FromResult(CaptureFrame.Empty);

    if (!Win32Methods.GetWindowRect(hwnd, out Win32Methods.RECT rect))
      return Task.FromResult(CaptureFrame.Empty);

    int width = rect.Right - rect.Left;
    int height = rect.Bottom - rect.Top;
    if (width <= 0 || height <= 0)
      return Task.FromResult(CaptureFrame.Empty);

    using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
    using (Graphics graphics = Graphics.FromImage(bitmap))
    {
      IntPtr hdc = graphics.GetHdc();

      try
      {
        Win32Methods.PrintWindow(hwnd, hdc, Win32Methods.PW_RENDERFULLCONTENT);
      }
      finally
      {
        graphics.ReleaseHdc();
      }
    }

    using MemoryStream stream = new();
    bitmap.Save(stream, ImageFormat.Png);

    PixelRect windowBounds = new(rect.Left, rect.Top, width, height);

    // Return synchronously - bitmap encoding is CPU-bound and very fast.
    return Task.FromResult(new CaptureFrame(stream.ToArray(), windowBounds));
  }

  /// <summary>
  /// Locates the D2R main window handle by process name, trying each known
  /// execultable/window title variant in turn.
  /// Returns <see cref="IntPtr.Zero"/> if the game isn't running.
  /// </summary>
  private static IntPtr FindD2RWindow()
  {
    foreach (string name in ProcessNames)
    {
      Process[] matches = Process.GetProcessesByName(name);

      try
      {
        if (matches.Length > 0 && matches[0].MainWindowHandle != IntPtr.Zero)
          return matches[0].MainWindowHandle;
      }
      finally
      {
        foreach (Process process in matches)
          process.Dispose();
      }
    }

    return IntPtr.Zero;
  }
}
