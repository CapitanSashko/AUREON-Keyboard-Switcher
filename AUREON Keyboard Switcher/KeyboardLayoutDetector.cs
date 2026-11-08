using System;
using System.Runtime.InteropServices;
using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Keyboard
{
    public static class KeyboardLayoutDetector
    {
        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            nint hWnd,
            nint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern nint GetKeyboardLayout(uint idThread);

        public static bool TryGetCurrentLayout(out KeyboardLanguage language)
        {
            language = KeyboardLanguage.English;

            nint foregroundWindow = GetForegroundWindow();

            if (foregroundWindow == 0)
                return false;

            uint threadId = GetWindowThreadProcessId(
                foregroundWindow,
                nint.Zero);

            if (threadId == 0)
                return false;

            nint layout = GetKeyboardLayout(threadId);

            ushort languageId = unchecked(
                (ushort)((long)layout & 0xFFFF));

            ushort primaryLanguage = (ushort)(languageId & 0x03FF);

            switch (primaryLanguage)
            {
                case 0x09: // English
                    language = KeyboardLanguage.English;
                    return true;

                case 0x19: // Russian
                    language = KeyboardLanguage.Russian;
                    return true;

                case 0x22: // Ukrainian
                    language = KeyboardLanguage.Ukrainian;
                    return true;

                case 0x07: // German
                    language = KeyboardLanguage.German;
                    return true;

                default:
                    return false;
            }
        }
    }
}