using D2RLootRadar.Application.Contracts;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace D2RLootRadar.Infrastructure.Input;

/// <summary>
/// Global low-level keyboard hook (WH_KEYBOARD_LL).
/// 
/// Detects Left ALT presses even when D2R is focused, since the hook intercepts
/// keyboard input system-wide rather than relying on window focus/message routing.
/// </summary>
public sealed class GlobalKeyboardMonitor : IKeyboardMonitor
{
  // Kept as a field (not a local) so the delegate is never garbage-collected while the
  // hook is installed - Windows holds an unmanaged reference to it that the GC can't see.
  private NativeMethods.HookProc? _hookProc;
  private IntPtr _hookHandle;

  /// <inheritdoc />
  public event EventHandler? AltPressed;

  /// <inheritdoc />
  public event EventHandler? AltReleased;

  /// <summary>
  /// Installs the low-level keyboard hook on the calling thread.
  /// Idempotent - calling this while already started is a no-op.
  /// Must be called from a thread that pumps a Windows message loop (e.g. the WPF UI thread) for
  /// the hook to actually receive events.
  /// </summary>
  public void Start()
  {
    if (_hookHandle != IntPtr.Zero)
      return;

    _hookProc = HookCallback;

    using Process process = Process.GetCurrentProcess();
    using ProcessModule? module = process.MainModule;

    nint moduleHandle = NativeMethods.GetModuleHandle(module?.ModuleName); ;

    _hookHandle = NativeMethods.SetWindowsHookEx(
      NativeMethods.WH_KEYBOARD_LL,
      _hookProc,
      moduleHandle,
      dwThreadId: 0
    );
  }

  /// <summary>
  /// Removes the hook.
  /// Idempotent - calling this while already stopped is a no-op.
  /// </summary>
  public void Stop()
  {
    if (_hookHandle == IntPtr.Zero)
      return;

    NativeMethods.UnhookWindowsHookEx(_hookHandle);

    _hookHandle = IntPtr.Zero;
  }

  /// <summary>
  /// Raw hook procedure invoked by Windows for every low-level keyboard event system-wide.
  /// Filters for the Left ALT key's up/down transitions and raises the
  /// corresponding event asynchronously via <see cref="Task.Run(Action)"/>,
  /// so that slow subscriber code can never delay this callback -
  /// a low-level hook that blocks for too long gets silently unregistered by Windows.
  /// </summary>
  private IntPtr HookCallback(
    int nCode,
    IntPtr wParam,
    IntPtr lParam
  )
  {
    if (nCode >= 0)
    {
      int keyCode = Marshal.ReadInt32(lParam);

      if (
        wParam == NativeMethods.WM_KEYDOWN ||
        wParam == NativeMethods.WM_SYSKEYDOWN
      )
      {
        if (keyCode == NativeMethods.VK_LMENU)
          Task.Run(() => AltPressed?.Invoke(this, EventArgs.Empty));
      }
      else if (
        wParam == NativeMethods.WM_KEYUP ||
        wParam == NativeMethods.WM_SYSKEYUP
      )
        if (keyCode == NativeMethods.VK_LMENU)
          Task.Run(() => AltReleased?.Invoke(this, EventArgs.Empty));
    }

    return NativeMethods.CallNextHookEx(
      _hookHandle,
      nCode,
      wParam,
      lParam
    );
  }

  /// <inheritdoc />
  public void Dispose()
  {
    Stop();
    GC.SuppressFinalize(this);
  }
}
