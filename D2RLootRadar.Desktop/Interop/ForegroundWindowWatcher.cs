using System.Runtime.InteropServices;

namespace D2RLootRadar.Desktop.Interop;

/// <summary>
/// Wraps a system-wide <c>SetWinEventHook</c> subscription that fires whenever any window becomes the foreground window,
/// so callers can react to focus change without polling -
/// matching the "no polling loop" philosophy already used elsewhere in the app
/// (see <c>Application.Monitoring.LootMonitoringService</c>'s class remarks).
/// </summary>
public sealed class ForegroundWindowWatcher : IDisposable
{
  private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
  private const uint WINEVENT_OUTOFCONTEXT = 0x0000;

  private delegate void WinEventDelegate(
    IntPtr hWinEventHook,
    uint eventType,
    IntPtr hwnd,
    int idObject,
    int idChild,
    uint dwEventThread,
    uint dwmsEventTime
  );

  [DllImport("user32.dll")]
  private static extern IntPtr SetWinEventHook(
    uint eventMin,
    uint eventMax,
    IntPtr hmodWinEventProc,
    WinEventDelegate lpfnWinEventProc,
    uint idProcess,
    uint idThread,
    uint dwFlags
  );

  [DllImport("user32.dll")]
  private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

  /// <summary>
  /// Kept as a field, not a local variable in Start().
  /// The unmanaged hook holds a raw function pointer into this delegate for as long as the hook is installed;
  /// if nothing on the managed side keeps a reference, the GC is free to collect it,
  /// and the next time Windows fires the event it calls into freed memory.
  /// </summary>
  private readonly WinEventDelegate _callback;
  private IntPtr _hook = IntPtr.Zero;

  /// <summary>
  /// Raised whenever any window becomes the foreground window.
  /// The is the newly-foreground window.
  /// 
  /// Fires on the thread <see cref="Start"/> was called from -
  /// WINEVENT_OUOFCONTEXT delivers the callback through that thread's own message queue rather than a worker thread,
  /// so on the WPF UI thread this arrives already on the UI thread with no extra dispatch required.
  /// </summary>
  public event Action<IntPtr>? ForegroundChanged;

  public ForegroundWindowWatcher()
    => _callback = OnWinEvent;

  /// <summary>
  /// Installs the hook.
  /// Must be called from a thread with a running message loop (in practice, the WPF UI thread) -
  /// see <see cref="ForegroundChanged"/>.
  /// A second call while already started is a no-op.
  /// </summary>
  public void Start()
  {
    if (_hook != IntPtr.Zero)
      return;

    _hook = SetWinEventHook(
      EVENT_SYSTEM_FOREGROUND,
      EVENT_SYSTEM_FOREGROUND,
      IntPtr.Zero,
      _callback,
      idProcess: 0, // 0 = all processes, not just this one - we need to see every window gain focus
      idThread: 0, // 0 = all threads
      WINEVENT_OUTOFCONTEXT
    );
  }

  private void OnWinEvent(
    IntPtr hWinEventHook,
    uint eventType,
    IntPtr hwnd,
    int idObject,
    int idChild,
    uint dwEventThread,
    uint dwmsEventTime
  )
  {
    if (eventType == EVENT_SYSTEM_FOREGROUND)
      ForegroundChanged?.Invoke(hwnd);
  }

  public void Dispose()
  {
    if (_hook == IntPtr.Zero)
      return;

    UnhookWinEvent(_hook);
    _hook = IntPtr.Zero;
  }
}
