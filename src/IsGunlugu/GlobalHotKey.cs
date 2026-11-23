using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace IsGunlugu;

/// <summary>
/// Win+Shift+N anywhere in Windows. It lives on a hidden message window of its own, so it works while the main
/// window is in the tray and was never shown.
/// </summary>
public sealed class GlobalHotKey : IDisposable
{
    private const int Id = 1;
    private const int WmHotKey = 0x0312;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint KeyN = 0x4E;

    private readonly HwndSource _source;
    private readonly Action _pressed;

    public GlobalHotKey(Action pressed)
    {
        _pressed = pressed;
        _source = new HwndSource(new HwndSourceParameters("IsGunlugu.HotKey") { ParentWindow = new IntPtr(-3), WindowStyle = 0 });
        _source.AddHook(Hook);
        IsWorking = RegisterHotKey(_source.Handle, Id, ModWin | ModShift, KeyN);
    }

    public const string Keys = "Win+Shift+N";

    /// <summary>False when another program already took the keys.</summary>
    public bool IsWorking { get; }

    public void Dispose()
    {
        if (IsWorking) UnregisterHotKey(_source.Handle, Id);
        _source.RemoveHook(Hook);
        _source.Dispose();
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == Id)
        {
            _pressed();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
