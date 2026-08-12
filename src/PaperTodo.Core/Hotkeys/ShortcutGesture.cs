namespace PaperTodo;

[Flags]
internal enum ShortcutModifiers : byte
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

// Values are Windows virtual-key codes, which keeps Core independent from both WPF and Avalonia
// while giving the Windows registrar a reflection-free, AOT-safe registration value.
internal enum ShortcutKey : uint
{
    None = 0,
    Cancel = 0x03,
    Back = 0x08,
    Tab = 0x09,
    LineFeed = 0x0A,
    Clear = 0x0C,
    Enter = 0x0D,
    Return = Enter,
    Pause = 0x13,
    Capital = 0x14,
    CapsLock = Capital,
    HangulMode = 0x15,
    KanaMode = HangulMode,
    JunjaMode = 0x17,
    FinalMode = 0x18,
    HanjaMode = 0x19,
    KanjiMode = HanjaMode,
    Escape = 0x1B,
    ImeConvert = 0x1C,
    ImeNonConvert = 0x1D,
    ImeAccept = 0x1E,
    ImeModeChange = 0x1F,
    Space = 0x20,
    PageUp = 0x21,
    Prior = PageUp,
    Next = 0x22,
    PageDown = Next,
    End = 0x23,
    Home = 0x24,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    Select = 0x29,
    Print = 0x2A,
    Execute = 0x2B,
    PrintScreen = 0x2C,
    Snapshot = PrintScreen,
    Insert = 0x2D,
    Delete = 0x2E,
    Help = 0x2F,
    D0 = 0x30,
    D1 = 0x31,
    D2 = 0x32,
    D3 = 0x33,
    D4 = 0x34,
    D5 = 0x35,
    D6 = 0x36,
    D7 = 0x37,
    D8 = 0x38,
    D9 = 0x39,
    A = 0x41,
    B = 0x42,
    C = 0x43,
    D = 0x44,
    E = 0x45,
    F = 0x46,
    G = 0x47,
    H = 0x48,
    I = 0x49,
    J = 0x4A,
    K = 0x4B,
    L = 0x4C,
    M = 0x4D,
    N = 0x4E,
    O = 0x4F,
    P = 0x50,
    Q = 0x51,
    R = 0x52,
    S = 0x53,
    T = 0x54,
    U = 0x55,
    V = 0x56,
    W = 0x57,
    X = 0x58,
    Y = 0x59,
    Z = 0x5A,
    LWin = 0x5B,
    RWin = 0x5C,
    Apps = 0x5D,
    Sleep = 0x5F,
    NumPad0 = 0x60,
    NumPad1 = 0x61,
    NumPad2 = 0x62,
    NumPad3 = 0x63,
    NumPad4 = 0x64,
    NumPad5 = 0x65,
    NumPad6 = 0x66,
    NumPad7 = 0x67,
    NumPad8 = 0x68,
    NumPad9 = 0x69,
    Multiply = 0x6A,
    Add = 0x6B,
    Separator = 0x6C,
    Subtract = 0x6D,
    Decimal = 0x6E,
    Divide = 0x6F,
    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
    F13 = 0x7C,
    F14 = 0x7D,
    F15 = 0x7E,
    F16 = 0x7F,
    F17 = 0x80,
    F18 = 0x81,
    F19 = 0x82,
    F20 = 0x83,
    F21 = 0x84,
    F22 = 0x85,
    F23 = 0x86,
    F24 = 0x87,
    NumLock = 0x90,
    Scroll = 0x91,
    LeftShift = 0xA0,
    RightShift = 0xA1,
    LeftCtrl = 0xA2,
    RightCtrl = 0xA3,
    LeftAlt = 0xA4,
    RightAlt = 0xA5,
    BrowserBack = 0xA6,
    BrowserForward = 0xA7,
    BrowserRefresh = 0xA8,
    BrowserStop = 0xA9,
    BrowserSearch = 0xAA,
    BrowserFavorites = 0xAB,
    BrowserHome = 0xAC,
    VolumeMute = 0xAD,
    VolumeDown = 0xAE,
    VolumeUp = 0xAF,
    MediaNextTrack = 0xB0,
    MediaPreviousTrack = 0xB1,
    MediaStop = 0xB2,
    MediaPlayPause = 0xB3,
    LaunchMail = 0xB4,
    SelectMedia = 0xB5,
    LaunchApplication1 = 0xB6,
    LaunchApplication2 = 0xB7,
    Oem1 = 0xBA,
    OemSemicolon = Oem1,
    OemPlus = 0xBB,
    OemComma = 0xBC,
    OemMinus = 0xBD,
    OemPeriod = 0xBE,
    Oem2 = 0xBF,
    OemQuestion = Oem2,
    Oem3 = 0xC0,
    OemTilde = Oem3,
    AbntC1 = 0xC1,
    AbntC2 = 0xC2,
    Oem4 = 0xDB,
    OemOpenBrackets = Oem4,
    Oem5 = 0xDC,
    OemPipe = Oem5,
    Oem6 = 0xDD,
    OemCloseBrackets = Oem6,
    Oem7 = 0xDE,
    OemQuotes = Oem7,
    Oem8 = 0xDF,
    Oem102 = 0xE2,
    OemBackslash = Oem102,
    ImeProcessed = 0x10000,
    System = 0x10001,
    DbeAlphanumeric = 0xF0,
    OemAttn = DbeAlphanumeric,
    DbeKatakana = 0xF1,
    OemFinish = DbeKatakana,
    DbeHiragana = 0xF2,
    OemCopy = DbeHiragana,
    DbeSbcsChar = 0xF3,
    OemAuto = DbeSbcsChar,
    DbeDbcsChar = 0xF4,
    OemEnlw = DbeDbcsChar,
    DbeRoman = 0xF5,
    OemBackTab = DbeRoman,
    Attn = 0xF6,
    DbeNoRoman = Attn,
    CrSel = 0xF7,
    DbeEnterWordRegisterMode = CrSel,
    DbeEnterImeConfigureMode = 0xF8,
    ExSel = DbeEnterImeConfigureMode,
    DbeFlushString = 0xF9,
    EraseEof = DbeFlushString,
    DbeCodeInput = 0xFA,
    Play = DbeCodeInput,
    DbeNoCodeInput = 0xFB,
    Zoom = DbeNoCodeInput,
    DbeDetermineString = 0xFC,
    NoName = DbeDetermineString,
    DbeEnterDialogConversionMode = 0xFD,
    Pa1 = DbeEnterDialogConversionMode,
    OemClear = 0xFE,
    DeadCharProcessed = 0x10002
}

internal readonly record struct ShortcutGesture(ShortcutKey Key, ShortcutModifiers Modifiers)
{
    public static ShortcutGesture ForEdgeOrdinal(ShortcutModifiers modifiers, int ordinal)
    {
        if (ordinal is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        return new ShortcutGesture((ShortcutKey)((uint)ShortcutKey.D0 + ordinal), modifiers);
    }

    public static bool HasExactlyTwoModifiers(ShortcutModifiers modifiers) =>
        CountSupportedModifiers(modifiers) == 2;

    public static bool HasEdgePrefixModifiers(ShortcutModifiers modifiers)
    {
        var count = CountSupportedModifiers(modifiers);
        return count is >= 2 and <= 3;
    }

    public static int CountSupportedModifiers(ShortcutModifiers modifiers)
    {
        const ShortcutModifiers supported = ShortcutModifiers.Control |
            ShortcutModifiers.Alt |
            ShortcutModifiers.Shift |
            ShortcutModifiers.Windows;
        if ((modifiers & ~supported) != ShortcutModifiers.None)
        {
            return 0;
        }

        var count = 0;
        if (modifiers.HasFlag(ShortcutModifiers.Control)) count++;
        if (modifiers.HasFlag(ShortcutModifiers.Alt)) count++;
        if (modifiers.HasFlag(ShortcutModifiers.Shift)) count++;
        if (modifiers.HasFlag(ShortcutModifiers.Windows)) count++;
        return count;
    }

    public static bool IsEdgeOrdinalKey(ShortcutKey key, int ordinal) =>
        ordinal is >= 1 and <= 9 &&
        (key == (ShortcutKey)((uint)ShortcutKey.D0 + ordinal) ||
            key == (ShortcutKey)((uint)ShortcutKey.NumPad0 + ordinal));

    public static bool IsAnyEdgeOrdinalKey(ShortcutKey key) =>
        key is (>= ShortcutKey.D1 and <= ShortcutKey.D9) or
            (>= ShortcutKey.NumPad1 and <= ShortcutKey.NumPad9);

    public bool IsDigitKey =>
        Key is (>= ShortcutKey.D0 and <= ShortcutKey.D9) or
            (>= ShortcutKey.NumPad0 and <= ShortcutKey.NumPad9);

    public ShortcutGesture NormalizeNumpadDigit()
    {
        if (Key is >= ShortcutKey.NumPad0 and <= ShortcutKey.NumPad9)
        {
            var ordinal = (int)Key - (int)ShortcutKey.NumPad0;
            return new ShortcutGesture((ShortcutKey)((uint)ShortcutKey.D0 + ordinal), Modifiers);
        }

        return this;
    }

    public IEnumerable<ShortcutGesture> RegistrationGestures(bool includeDigitAlias)
    {
        yield return this;
        if (!includeDigitAlias)
        {
            yield break;
        }

        if (Key is >= ShortcutKey.D0 and <= ShortcutKey.D9)
        {
            var ordinal = (int)Key - (int)ShortcutKey.D0;
            yield return new ShortcutGesture(
                (ShortcutKey)((uint)ShortcutKey.NumPad0 + ordinal),
                Modifiers);
        }
        else if (Key is >= ShortcutKey.NumPad0 and <= ShortcutKey.NumPad9)
        {
            var ordinal = (int)Key - (int)ShortcutKey.NumPad0;
            yield return new ShortcutGesture(
                (ShortcutKey)((uint)ShortcutKey.D0 + ordinal),
                Modifiers);
        }
    }

    public string ToEdgePrefixDisplayString()
    {
        if (!HasEdgePrefixModifiers(Modifiers))
        {
            return "";
        }

        return string.Join('+', ModifierParts());
    }

    public string ToEdgeSequenceDisplayString()
    {
        var prefix = ToEdgePrefixDisplayString();
        return string.IsNullOrEmpty(prefix) ? "" : $"{prefix}+1–9";
    }

    public static bool TryParse(string? text, out ShortcutGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        var modifiers = ShortcutModifiers.None;
        var key = ShortcutKey.None;
        foreach (var part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ShortcutModifiers.Control;
                    continue;
                case "alt":
                    modifiers |= ShortcutModifiers.Alt;
                    continue;
                case "shift":
                    modifiers |= ShortcutModifiers.Shift;
                    continue;
                case "win":
                case "windows":
                    modifiers |= ShortcutModifiers.Windows;
                    continue;
            }

            if (key != ShortcutKey.None || !TryParseKey(part, out key))
            {
                return false;
            }
        }

        if (modifiers == ShortcutModifiers.None || IsModifierKey(key) || key == ShortcutKey.None)
        {
            return false;
        }

        gesture = new ShortcutGesture(key, modifiers);
        return true;
    }

    public string ToStorageString()
    {
        if (Key == ShortcutKey.None)
        {
            return "";
        }

        var parts = ModifierParts();
        parts.Add(StorageKeyName(Key));
        return string.Join('+', parts);
    }

    public string ToDisplayString()
    {
        if (Key == ShortcutKey.None)
        {
            return "";
        }

        var parts = ModifierParts();
        parts.Add(DisplayKeyName(Key));
        return string.Join('+', parts);
    }

    private List<string> ModifierParts()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(ShortcutModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ShortcutModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ShortcutModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ShortcutModifiers.Windows)) parts.Add("Win");
        return parts;
    }

    private static bool TryParseKey(string text, out ShortcutKey key)
    {
        if (text.Length == 1 && text[0] is >= '0' and <= '9')
        {
            key = (ShortcutKey)((uint)ShortcutKey.D0 + text[0] - '0');
            return true;
        }

        if (text.Length == 1 && text[0] is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
        {
            key = (ShortcutKey)((uint)ShortcutKey.A + char.ToUpperInvariant(text[0]) - 'A');
            return true;
        }

        return Enum.TryParse(text, ignoreCase: true, out key);
    }

    private static string StorageKeyName(ShortcutKey key)
    {
        if (key is >= ShortcutKey.D0 and <= ShortcutKey.D9)
        {
            return ((int)key - (int)ShortcutKey.D0).ToString();
        }

        // Match System.Windows.Input.Key.ToString() for aliased WPF enum values so existing
        // data.json bindings keep their exact canonical spelling after normalization.
        return key switch
        {
            ShortcutKey.Enter => "Enter",
            ShortcutKey.Capital => "Capital",
            ShortcutKey.HangulMode => "HangulMode",
            ShortcutKey.HanjaMode => "HanjaMode",
            ShortcutKey.PageUp => "PageUp",
            ShortcutKey.Next => "Next",
            ShortcutKey.PrintScreen => "PrintScreen",
            ShortcutKey.Oem1 => "Oem1",
            ShortcutKey.Oem2 => "Oem2",
            ShortcutKey.Oem3 => "Oem3",
            ShortcutKey.Oem4 => "Oem4",
            ShortcutKey.Oem5 => "Oem5",
            ShortcutKey.Oem6 => "Oem6",
            ShortcutKey.Oem7 => "Oem7",
            ShortcutKey.Oem102 => "Oem102",
            ShortcutKey.DbeAlphanumeric => "DbeAlphanumeric",
            ShortcutKey.DbeKatakana => "DbeKatakana",
            ShortcutKey.DbeHiragana => "DbeHiragana",
            ShortcutKey.DbeSbcsChar => "DbeSbcsChar",
            ShortcutKey.DbeDbcsChar => "DbeDbcsChar",
            ShortcutKey.DbeRoman => "DbeRoman",
            ShortcutKey.Attn => "Attn",
            ShortcutKey.CrSel => "CrSel",
            ShortcutKey.DbeEnterImeConfigureMode => "DbeEnterImeConfigureMode",
            ShortcutKey.DbeFlushString => "DbeFlushString",
            ShortcutKey.DbeCodeInput => "DbeCodeInput",
            ShortcutKey.DbeNoCodeInput => "DbeNoCodeInput",
            ShortcutKey.DbeDetermineString => "DbeDetermineString",
            ShortcutKey.DbeEnterDialogConversionMode => "DbeEnterDialogConversionMode",
            _ => key.ToString()
        };
    }

    private static string DisplayKeyName(ShortcutKey key)
    {
        if (key is >= ShortcutKey.D0 and <= ShortcutKey.D9)
        {
            return ((int)key - (int)ShortcutKey.D0).ToString();
        }

        if (key is >= ShortcutKey.NumPad0 and <= ShortcutKey.NumPad9)
        {
            return $"Num {(int)key - (int)ShortcutKey.NumPad0}";
        }

        return key switch
        {
            ShortcutKey.OemPlus => "+",
            ShortcutKey.OemMinus => "-",
            ShortcutKey.OemComma => ",",
            ShortcutKey.OemPeriod => ".",
            ShortcutKey.OemQuestion => "/",
            ShortcutKey.OemSemicolon => ";",
            ShortcutKey.OemQuotes => "'",
            ShortcutKey.OemOpenBrackets => "[",
            ShortcutKey.OemCloseBrackets => "]",
            ShortcutKey.OemPipe => "\\",
            ShortcutKey.OemTilde => "`",
            _ => StorageKeyName(key)
        };
    }

    public static bool IsModifierKey(ShortcutKey key) =>
        key is ShortcutKey.LeftCtrl or ShortcutKey.RightCtrl or
            ShortcutKey.LeftAlt or ShortcutKey.RightAlt or
            ShortcutKey.LeftShift or ShortcutKey.RightShift or
            ShortcutKey.LWin or ShortcutKey.RWin;
}

internal enum GlobalShortcutRegistrationFailure
{
    None,
    SystemOccupied,
    RegistrationFailed
}

internal interface IGlobalHotkeyRegistrar : IDisposable
{
    event Action<string>? Invoked;

    IReadOnlyDictionary<string, string> ActiveBindings { get; }

    bool TryApply(
        IReadOnlyDictionary<string, string> desiredBindings,
        IReadOnlyCollection<string> activeCommandIds,
        bool distinguishNumpadDigits,
        out string? failedCommandId,
        out GlobalShortcutRegistrationFailure failure);

    bool ProcessWindowMessage(int message, nint wParam);
}
