using System.Windows.Forms;
using AUREON_Keyboard_Switcher.Keyboard;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>Сочетание клавиш: основная клавиша и модификаторы.</summary>
    public readonly record struct Hotkey(Keys Key, bool Ctrl, bool Alt, bool Shift)
    {
        public static Hotkey DefaultLastWord => new(Keys.Pause, false, false, false);

        public static Hotkey DefaultSelection => new(Keys.Pause, false, false, true);

        public static Hotkey DefaultTransliterate => new(Keys.T, true, true, false);

        public bool IsEmpty => Key == Keys.None;

        /// <summary>Совпадает ли нажатие с этим сочетанием (модификаторы должны совпасть точно).</summary>
        public bool Matches(KeyEvent e) =>
            !IsEmpty &&
            e.Key == Key &&
            e.Ctrl == Ctrl &&
            e.Alt == Alt &&
            e.Shift == Shift;

        /// <summary>Запись для файла настроек, например «Ctrl+Alt+Z» или «Shift+Pause».</summary>
        public string ToSettingString()
        {
            if (IsEmpty)
                return string.Empty;

            List<string> parts = new();

            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");

            parts.Add(Key.ToString());

            return string.Join("+", parts);
        }

        /// <summary>Запись для показа пользователю, например «Ctrl + Alt + Z».</summary>
        public override string ToString()
        {
            if (IsEmpty)
                return string.Empty;

            List<string> parts = new();

            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");

            parts.Add(KeyName(Key));

            return string.Join(" + ", parts);
        }

        public static Hotkey ParseOr(string? text, Hotkey fallback) =>
            TryParse(text, out Hotkey hotkey) ? hotkey : fallback;

        public static bool TryParse(string? text, out Hotkey hotkey)
        {
            hotkey = default;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            bool ctrl = false, alt = false, shift = false;
            Keys key = Keys.None;

            foreach (string token in text.Split(
                         '+',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    ctrl = true;
                }
                else if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    alt = true;
                }
                else if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    shift = true;
                }
                else if (key == Keys.None &&
                         Enum.TryParse(token, ignoreCase: true, out Keys parsed) &&
                         parsed != Keys.None)
                {
                    key = parsed;
                }
                else
                {
                    return false;
                }
            }

            if (key == Keys.None)
                return false;

            Hotkey candidate = new(key, ctrl, alt, shift);

            if (!IsAllowed(candidate, out _))
                return false;

            hotkey = candidate;
            return true;
        }

        /// <summary>Можно ли назначить такое сочетание (и почему нельзя).</summary>
        public static bool IsAllowed(Hotkey hotkey, out string reason)
        {
            reason = string.Empty;

            if (hotkey.IsEmpty)
            {
                reason = "Сполучення не задано";
                return false;
            }

            if (IsModifierKey(hotkey.Key))
            {
                reason = "Потрібна ще й звичайна клавіша, не лише Ctrl, Alt або Shift";
                return false;
            }

            if (hotkey.Key is Keys.Escape or Keys.Tab)
            {
                reason = "Ця клавіша зарезервована";
                return false;
            }

            if (hotkey.Alt && hotkey.Key == Keys.F4)
            {
                reason = "Alt+F4 закриває вікна";
                return false;
            }

            // Клавіші, що не друкують символів, можна без модифікаторів
            bool special =
                (hotkey.Key >= Keys.F1 && hotkey.Key <= Keys.F24) ||
                hotkey.Key is Keys.Pause or Keys.Scroll;

            if (!special)
            {
                int modifiers = (hotkey.Ctrl ? 1 : 0) + (hotkey.Alt ? 1 : 0) + (hotkey.Shift ? 1 : 0);

                if (modifiers < 2)
                {
                    reason = "Для літер і цифр потрібні щонайменше два модифікатори, наприклад Ctrl+Alt+Z";
                    return false;
                }
            }

            return true;
        }

        public static bool IsModifierKey(Keys key) =>
            key is Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
                or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
                or Keys.Menu or Keys.LMenu or Keys.RMenu
                or Keys.LWin or Keys.RWin;

        private static string KeyName(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
                return ((char)('0' + (key - Keys.D0))).ToString();

            return key switch
            {
                Keys.Scroll => "Scroll Lock",
                Keys.Prior => "Page Up",
                Keys.Next => "Page Down",
                Keys.Return => "Enter",
                Keys.Capital => "Caps Lock",
                Keys.Oemtilde => "`",
                Keys.OemMinus => "-",
                Keys.Oemplus => "=",
                Keys.OemOpenBrackets => "[",
                Keys.Oem6 => "]",
                Keys.Oem5 => "\\",
                Keys.Oem1 => ";",
                Keys.Oem7 => "'",
                Keys.Oemcomma => ",",
                Keys.OemPeriod => ".",
                Keys.OemQuestion => "/",
                _ => key.ToString()
            };
        }
    }
}