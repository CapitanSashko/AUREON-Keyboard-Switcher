using System;
using System.Collections.Generic;
using System.Text;

namespace AUREON_Keyboard_Switcher.Languages
{
    public enum KeyboardLanguage
    {
        English,
        Russian,
        Ukrainian,
        German
    }

    public static class KeyboardLayoutConverter
    {
        // Порядок фізичних клавіш (однаковий для всіх розкладок):
        //   ряд 0:  ` 1 2 3 4 5 6 7 8 9 0 - =        (13 клавіш)
        //   ряд 1:  q w e r t y u i o p [ ] \        (13)
        //   ряд 2:  a s d f g h j k l ; '            (11)
        //   ряд 3:  z x c v b n m , . /              (10)
        // Спочатку 47 символів без Shift, потім 47 з Shift. Разом 94.

        private const int KeyCount = 47;
        private const int ExpectedLength = KeyCount * 2;

        private const string EnglishLayout =
            "`1234567890-=" + "qwertyuiop[]\\" + "asdfghjkl;'" + "zxcvbnm,./" +
            "~!@#$%^&*()_+" + "QWERTYUIOP{}|" + "ASDFGHJKL:\"" + "ZXCVBNM<>?";

        private const string RussianLayout =
            "ё1234567890-=" + "йцукенгшщзхъ\\" + "фывапролджэ" + "ячсмитьбю." +
            "Ё!\"№;%:?*()_+" + "ЙЦУКЕНГШЩЗХЪ/" + "ФЫВАПРОЛДЖЭ" + "ЯЧСМИТЬБЮ,";

        // Стандартна українська розкладка Windows:
        // апостроф на клавіші `, літера ґ на клавіші \
        private const string UkrainianLayout =
            "'1234567890-=" + "йцукенгшщзхїґ" + "фівапролджє" + "ячсмитьбю." +
            "₴!\"№;%:?*()_+" + "ЙЦУКЕНГШЩЗХЇҐ" + "ФІВАПРОЛДЖЄ" + "ЯЧСМИТЬБЮ,";

        // Німецька розкладка QWERTZ (Німеччина, Австрія).
        // Клавіші ^ та ´ є мертвими, тут вони записані як звичайні символи.
        private const string GermanLayout =
            "^1234567890ß´" + "qwertzuiopü+#" + "asdfghjklöä" + "yxcvbnm,.-" +
            "°!\"§$%&/()=?`" + "QWERTZUIOPÜ*'" + "ASDFGHJKLÖÄ" + "YXCVBNM;:_";

        // Скан-коди фізичних клавіш у тому ж порядку, що й у таблицях розкладок.
        // Скан-код залежить лише від положення клавіші, а не від розкладки.
        private static readonly uint[] ScanCodes =
        {
            0x29, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, // ` 1 2 3 4 5 6 7 8 9 0 - =
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x2B, // q w e r t y u i o p [ ] \ 
            0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28,             // a s d f g h j k l ; '
            0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35                    // z x c v b n m , . /
        };

        private static readonly Dictionary<KeyboardLanguage, string> Layouts = new()
        {
            [KeyboardLanguage.English] = EnglishLayout,
            [KeyboardLanguage.Russian] = RussianLayout,
            [KeyboardLanguage.Ukrainian] = UkrainianLayout,
            [KeyboardLanguage.German] = GermanLayout
        };

        // символ -> номер фізичної клавіші (для кожної розкладки)
        private static readonly Dictionary<KeyboardLanguage, Dictionary<char, int>> Indexes =
            BuildIndexes();

        /// <summary>Номер фізичної клавіші (0..46) за скан-кодом або false, якщо це не клавіша основного блоку.</summary>
        public static bool TryGetKeyIndex(uint scanCode, out int index)
        {
            index = Array.IndexOf(ScanCodes, scanCode);
            return index >= 0;
        }

        /// <summary>Символ, який дає фізична клавіша на англійській (US) розкладці.</summary>
        public static char GetPhysicalChar(int keyIndex, bool shift) =>
            EnglishLayout[keyIndex + (shift ? KeyCount : 0)];

        /// <summary>Символ, який дає фізична клавіша (0..46) на розкладці вказаної мови.</summary>
        public static char GetLayoutChar(KeyboardLanguage language, int keyIndex, bool shift) =>
            Layouts[language][keyIndex + (shift ? KeyCount : 0)];

        public static string Convert(
            string text,
            KeyboardLanguage source,
            KeyboardLanguage target)
        {
            if (string.IsNullOrEmpty(text) || source == target)
                return text;

            Dictionary<char, int> sourceIndex = Indexes[source];
            string targetLayout = Layouts[target];

            StringBuilder result = new(text.Length);

            foreach (char ch in text)
            {
                result.Append(
                    sourceIndex.TryGetValue(ch, out int index)
                        ? targetLayout[index]
                        : ch);   // невідомий символ залишаємо як є
            }

            return result.ToString();
        }

        private static Dictionary<KeyboardLanguage, Dictionary<char, int>> BuildIndexes()
        {
            var all = new Dictionary<KeyboardLanguage, Dictionary<char, int>>();

            foreach (var pair in Layouts)
            {
                if (pair.Value.Length != ExpectedLength)
                {
                    throw new InvalidOperationException(
                        $"Розкладка {pair.Key}: довжина {pair.Value.Length}, очікувалося {ExpectedLength}. " +
                        "Перевірте рядок розкладки: десь пропущено або зайвий символ.");
                }

                var map = new Dictionary<char, int>();

                for (int i = 0; i < pair.Value.Length; i++)
                    map.TryAdd(pair.Value[i], i);

                all[pair.Key] = map;
            }

            return all;
        }
    }
}