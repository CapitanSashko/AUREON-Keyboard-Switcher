using System.Runtime.InteropServices;
using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Keyboard
{
    public static class InstalledLayouts
    {
        [DllImport("user32.dll")]
        private static extern int GetKeyboardLayoutList(
            int nBuff,
            [Out] nint[]? lpList);

        /// <summary>
        /// Установленные раскладки: язык -> дескриптор раскладки (HKL).
        /// Поддерживаются английская, русская, украинская и немецкая.
        /// </summary>
        public static Dictionary<KeyboardLanguage, nint> GetHandles()
        {
            var result = new Dictionary<KeyboardLanguage, nint>();

            int count = GetKeyboardLayoutList(0, null);
            if (count <= 0)
                return result;

            nint[] list = new nint[count];
            int received = GetKeyboardLayoutList(count, list);

            for (int i = 0; i < received; i++)
            {
                ushort languageId = unchecked((ushort)((long)list[i] & 0xFFFF));
                ushort primary = (ushort)(languageId & 0x03FF);

                KeyboardLanguage? language = primary switch
                {
                    0x09 => KeyboardLanguage.English,
                    0x19 => KeyboardLanguage.Russian,
                    0x22 => KeyboardLanguage.Ukrainian,
                    0x07 => KeyboardLanguage.German,
                    _ => null
                };

                if (language != null)
                    result.TryAdd(language.Value, list[i]);
            }

            return result;
        }

        public static List<KeyboardLanguage> GetInstalled() =>
            GetHandles().Keys.ToList();
    }
}