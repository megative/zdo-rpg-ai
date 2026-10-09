using System.Runtime.InteropServices;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Client.Hotkey;

// Linux counterpart of MacosHotkeyListener. Polls the global key state via X11 XQueryKeymap.
// Works while an X11/XWayland window (e.g. the game running under Proton) has focus.
public class LinuxHotkeyListener : IHotkeyListener {
    private static readonly ILog Log = Logger.Get<LinuxHotkeyListener>();

    private readonly nuint _keySym;
    private readonly string _keyName;
    private readonly int _pollIntervalMs;
    private volatile bool _wasPressed;
    private CancellationTokenSource? _cts;

    public event Action? KeyPressed;
    public event Action? KeyReleased;

    public LinuxHotkeyListener(string keyName, int pollIntervalMs = 30) {
        _keyName = keyName;
        _keySym = MapKeySym(keyName);
        _pollIntervalMs = pollIntervalMs;
    }

    public async Task RunAsync(CancellationToken ct) {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _cts = linked;
        var token = linked.Token;

        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero) {
            Log.Error("Cannot open X11 display (DISPLAY={Display}); push-to-talk disabled",
                Environment.GetEnvironmentVariable("DISPLAY") ?? "unset");
            return;
        }

        var keycode = XKeysymToKeycode(display, _keySym);
        if (keycode == 0) {
            Log.Error("Key '{Key}' (keysym 0x{Sym:X}) maps to no keycode on this keyboard; push-to-talk disabled",
                _keyName, (ulong)_keySym);
            XCloseDisplay(display);
            return;
        }

        Log.Info("Global hotkey: {Key} (X11 keycode={Code}, polling every {Ms}ms)",
            _keyName, keycode, _pollIntervalMs);

        var keys = new byte[32];
        try {
            while (!token.IsCancellationRequested) {
                XQueryKeymap(display, keys);
                var isPressed = (keys[keycode >> 3] & (1 << (keycode & 7))) != 0;

                if (isPressed && !_wasPressed) {
                    _wasPressed = true;
                    Log.Debug("Hotkey pressed: {Key}", _keyName);
                    KeyPressed?.Invoke();
                }
                else if (!isPressed && _wasPressed) {
                    _wasPressed = false;
                    Log.Debug("Hotkey released: {Key}", _keyName);
                    KeyReleased?.Invoke();
                }

                await Task.Delay(_pollIntervalMs, token);
            }
        }
        catch (OperationCanceledException) {
            // Normal shutdown
        }
        finally {
            XCloseDisplay(display);
        }

        Log.Debug("Hotkey polling stopped");
    }

    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    // Fills a 32-byte (256-bit) vector; bit N reflects whether keycode N is currently held.
    [DllImport("libX11.so.6")]
    private static extern int XQueryKeymap(IntPtr display, byte[] keysReturn);

    [DllImport("libX11.so.6")]
    private static extern byte XKeysymToKeycode(IntPtr display, nuint keysym);

    // X11 keysyms (X11/keysymdef.h). Letters/digits map straight to their ASCII keysym.
    private static nuint MapKeySym(string keyName) {
        var upper = keyName.ToUpperInvariant();
        if (upper.Length == 1) {
            var c = upper[0];
            if (c is >= 'A' and <= 'Z') {
                return (nuint)('a' + (c - 'A')); // XK_a..XK_z (lowercase)
            }
            if (c is >= '0' and <= '9') {
                return (nuint)c; // XK_0..XK_9
            }
        }

        return upper switch {
            "SPACE" => 0x0020,
            "TAB" => 0xFF09,
            "ENTER" or "RETURN" => 0xFF0D,
            "ESCAPE" or "ESC" => 0xFF1B,
            "CAPSLOCK" => 0xFFE5,
            "F1" => 0xFFBE,
            "F2" => 0xFFBF,
            "F3" => 0xFFC0,
            "F4" => 0xFFC1,
            "F5" => 0xFFC2,
            "F6" => 0xFFC3,
            "F7" => 0xFFC4,
            "F8" => 0xFFC5,
            "F9" => 0xFFC6,
            "F10" => 0xFFC7,
            "F11" => 0xFFC8,
            "F12" => 0xFFC9,
            "LEFTSHIFT" or "LSHIFT" => 0xFFE1,
            "RIGHTSHIFT" or "RSHIFT" => 0xFFE2,
            "LEFTCONTROL" or "LCONTROL" or "LEFTCTRL" or "LCTRL" => 0xFFE3,
            "RIGHTCONTROL" or "RCONTROL" or "RIGHTCTRL" or "RCTRL" => 0xFFE4,
            "LEFTALT" or "LALT" => 0xFFE9,
            "RIGHTALT" or "RALT" => 0xFFEA,
            "LEFTMETA" or "LMETA" or "LEFTSUPER" or "LSUPER" => 0xFFEB,
            "RIGHTMETA" or "RMETA" or "RIGHTSUPER" or "RSUPER" => 0xFFEC,
            _ => throw new ArgumentException(
                $"Unknown key '{keyName}'. Supported: A-Z, 0-9, F1-F12, Space, Tab, Escape, Enter, modifier keys")
        };
    }

    public void Dispose() {
        _cts?.Cancel();
    }
}
