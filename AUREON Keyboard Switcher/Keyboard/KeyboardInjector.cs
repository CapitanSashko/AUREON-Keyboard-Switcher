using System.Runtime.InteropServices;

namespace AUREON_Keyboard_Switcher.Keyboard
{
    /// <summary>
    /// Программный ввод: стирание символов, печать текста и сочетания Ctrl+клавиша через SendInput.
    /// </summary>
    public static class KeyboardInjector
    {
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        private const ushort VK_BACK = 0x08;
        private const ushort VK_SPACE = 0x20;
        private const ushort VK_CONTROL_KEY = 0x11;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;   // Alt
        private const int VK_PAUSE = 0x13;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public nint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public nint dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(
            uint nInputs,
            INPUT[] pInputs,
            int cbSize);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>Зажаты ли сейчас Ctrl или Alt.</summary>
        public static bool IsCtrlOrAltHeld() =>
            (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 ||
            (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

        /// <summary>Отпущены ли Shift, Ctrl, Alt и Pause (после горячей клавиши).</summary>
        public static bool AreHotkeyKeysReleased(int triggerKey = VK_PAUSE)
        {
            int[] keys = { VK_SHIFT, VK_CONTROL, VK_MENU, triggerKey };

            foreach (int key in keys)
            {
                if ((GetAsyncKeyState(key) & 0x8000) != 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Стирает backspaceCount символов и печатает text.
        /// Пробелы отправляются как клавиша Space, остальное как Unicode-символы
        /// (поэтому текущая раскладка не важна).
        /// </summary>
        public static bool ReplaceText(int backspaceCount, string text)
        {
            List<INPUT> inputs = new();

            for (int i = 0; i < backspaceCount; i++)
                AddVirtualKey(inputs, VK_BACK);

            foreach (char c in text)
            {
                if (c == ' ')
                    AddVirtualKey(inputs, VK_SPACE);
                else
                    AddUnicodeChar(inputs, c);
            }

            return Send(inputs);
        }

        /// <summary>Нажимает Ctrl+клавиша (например, Ctrl+C или Ctrl+V).</summary>
        public static bool SendCtrlKey(ushort virtualKey)
        {
            List<INPUT> inputs = new()
            {
                CreateKey(VK_CONTROL_KEY, 0, 0),
                CreateKey(virtualKey, 0, 0),
                CreateKey(virtualKey, 0, KEYEVENTF_KEYUP),
                CreateKey(VK_CONTROL_KEY, 0, KEYEVENTF_KEYUP)
            };

            return Send(inputs);
        }

        private const ushort VK_CAPITAL_KEY = 0x14;

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        /// <summary>Чи ввімкнено зараз CapsLock.</summary>
        public static bool IsCapsLockOn() => (GetKeyState(VK_CAPITAL_KEY) & 0x0001) != 0;

        /// <summary>Натискає й відпускає CapsLock (перемикає його стан).</summary>
        public static bool TapCapsLock()
        {
            List<INPUT> inputs = new();

            AddVirtualKey(inputs, VK_CAPITAL_KEY);

            return Send(inputs);
        }

        private static bool Send(List<INPUT> inputs)
        {
            if (inputs.Count == 0)
                return true;

            uint sent = SendInput(
                (uint)inputs.Count,
                inputs.ToArray(),
                Marshal.SizeOf<INPUT>());

            return sent == inputs.Count;
        }

        private static void AddVirtualKey(List<INPUT> list, ushort vk)
        {
            list.Add(CreateKey(vk, 0, 0));
            list.Add(CreateKey(vk, 0, KEYEVENTF_KEYUP));
        }

        private static void AddUnicodeChar(List<INPUT> list, char c)
        {
            list.Add(CreateKey(0, c, KEYEVENTF_UNICODE));
            list.Add(CreateKey(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
        }

        private static INPUT CreateKey(ushort vk, ushort scan, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = scan,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = nint.Zero
                    }
                }
            };
        }
    }
}