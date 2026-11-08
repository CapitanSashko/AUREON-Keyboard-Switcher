using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>
    /// Виправляє регістр слова після помилки з CapsLock або подвійної великої літери:
    /// результат завжди «Перша велика, решта малі».
    /// </summary>
    public static class CapsFixer
    {
        // Знаки, які можуть стояти в кінці слова як звичайна пунктуація
        private const string TailChars = ".,!?;:\")";

        private const int MinLetters = 3;

        /// <param name="word">Слово так, як його буде надруковано (з розділовими знаками в кінці).</param>
        /// <param name="mistake">Яку помилку зафіксовано під час набору.</param>
        /// <param name="language">Мова, в якій слово буде надруковано.</param>
        public static bool TryFix(
            string word,
            CaseMistake mistake,
            KeyboardLanguage language,
            WordDictionary dictionary,
            out string fixedWord)
        {
            fixedWord = word;

            if (mistake == CaseMistake.None)
                return false;

            int tailLength = CountTail(word);

            if (tailLength >= word.Length)
                return false;

            string core = tailLength > 0 ? word[..^tailLength] : word;
            string tail = tailLength > 0 ? word[^tailLength..] : string.Empty;

            if (core.Length < MinLetters || !core.All(char.IsLetter))
                return false;

            // Подвійну велику виправляємо лише у словах кирилицею:
            // у латиниці повно законних абревіатур (VMware, IDs, OSes)
            if (mistake == CaseMistake.DoubleCapital && !core.All(IsCyrillic))
                return false;

            string lower = core.ToLowerInvariant();

            // Невідомі слова (назви, абревіатури, жаргон) не чіпаємо
            if (dictionary.GetRank(language, lower) < 0)
                return false;

            fixedWord = char.ToUpperInvariant(lower[0]) + lower[1..] + tail;
            return true;
        }

        private static bool IsCyrillic(char c) => c is >= '\u0400' and <= '\u04FF';

        private static int CountTail(string text)
        {
            int count = 0;

            while (count < text.Length &&
                   TailChars.IndexOf(text[text.Length - 1 - count]) >= 0)
            {
                count++;
            }

            return count;
        }
    }
}