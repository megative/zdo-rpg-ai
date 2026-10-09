namespace ZdoRpgAi.Client.Hotkey;

// The push-to-talk key as the game sees it: the mod watches the key and reports presses through its
// log. Works on every OS and under Wayland, and needs no global keyboard hook.
public class ModHotkeyListener : IHotkeyListener {
    private bool _pressed;

    public event Action? KeyPressed;
    public event Action? KeyReleased;

    public void SetPressed(bool pressed) {
        if (pressed == _pressed) {
            return;
        }

        _pressed = pressed;
        (pressed ? KeyPressed : KeyReleased)?.Invoke();
    }

    // Nothing to poll: presses arrive with the mod's messages
    public Task RunAsync(CancellationToken ct) => Task.CompletedTask;

    public void Dispose() {
    }

    // Config key names, including the aliases the old OS listeners accepted, as openmw.input.KEY names
    public static string ToGameKeyName(string key) {
        var name = key.Trim();
        var upper = name.ToUpperInvariant();
        if (upper.Length == 1 && char.IsAsciiLetterUpper(upper[0])) {
            return upper;
        }
        if (upper.Length == 1 && char.IsAsciiDigit(upper[0])) {
            return "_" + upper;
        }
        if (upper.Length > 1 && upper[0] == 'F' && int.TryParse(upper[1..], out var f) && f is >= 1 and <= 12) {
            return upper;
        }

        return upper switch {
            "SPACE" => "Space",
            "TAB" => "Tab",
            "ENTER" or "RETURN" => "Enter",
            "ESCAPE" or "ESC" => "Escape",
            "CAPSLOCK" => "CapsLock",
            "BACKSPACE" => "Backspace",
            "LEFTSHIFT" or "LSHIFT" => "LeftShift",
            "RIGHTSHIFT" or "RSHIFT" => "RightShift",
            "LEFTCONTROL" or "LCONTROL" or "LEFTCTRL" or "LCTRL" => "LeftCtrl",
            "RIGHTCONTROL" or "RCONTROL" or "RIGHTCTRL" or "RCTRL" => "RightCtrl",
            "LEFTALT" or "LALT" => "LeftAlt",
            "RIGHTALT" or "RALT" => "RightAlt",
            "LEFTMETA" or "LMETA" or "LEFTSUPER" or "LSUPER" => "LeftSuper",
            "RIGHTMETA" or "RMETA" or "RIGHTSUPER" or "RSUPER" => "RightSuper",
            // Any other openmw.input.KEY name passes through as written
            _ => name,
        };
    }
}
