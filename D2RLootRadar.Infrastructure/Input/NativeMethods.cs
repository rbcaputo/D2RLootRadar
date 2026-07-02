using System.Runtime.InteropServices;

namespace D2RLootRadar.Infrastructure.Input;

/// <summary>
/// Win32 API declarations for installing a global low-level keyboard hook.
/// </summary>
internal static class NativeMethods
{
  /// <summary>
  /// Hook type identifier for a low-level keyboard hook (system-wide, not thread-specific).
  /// </summary>
  internal const int WH_KEYBOARD_LL = 13;

  internal const int WM_KEYDOWN = 0x0100;
  internal const int WM_SYSKEYDOWN = 0x0104; // ALT + any key pressed while ALT is held
  internal const int WM_KEYUP = 0x0101;
  internal const int WM_SYSKEYUP = 0x0105;

  /// <summary>
  /// Virtual-key code for the Left ALT key.
  /// </summary>
  internal const int VK_LMENU = 0xA4;

  /// <summary>
  /// Signature required by <see cref="SetWindowsHookEx"/> for a WH_KEYBOARD_LL hook.
  /// </summary>
  internal delegate IntPtr HookProc(
    int nCode,
    IntPtr wParam,
    IntPtr lParam
  );

  /// <summary>
  /// Installs a system-wide hook of the given type, invoking <paramref name="lpfn"/> for each matching event.
  /// </summary>
  [DllImport("user32.dll")]
  internal static extern IntPtr SetWindowsHookEx(
    int idHook,
    HookProc lpfn,
    IntPtr hMod,
    uint dwThreadId
  );

  /// <summary>
  /// Removes a previously-installed hook.
  /// </summary>
  [DllImport("user32.dll")]
  internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

  /// <summary>
  /// Passes the hook event along to the next hook in the chain.
  /// Must be called from every hook procedure.
  /// </summary>
  [DllImport("user32.dll")]
  internal static extern IntPtr CallNextHookEx(
    IntPtr hhk,
    int nCode,
    IntPtr wParam,
    IntPtr lParam
  );

  /// <summary>
  /// Retrieves a handle to the given module, used as the hook's owning module.
  /// </summary>
  [DllImport("kernel32.dll")]
  internal static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);
}
