using System.Text;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>Яку абетку мав на увазі автор тексту (від цього залежить, як читається «и»).</summary>
    public enum CyrillicVariant
    {
        Ukrainian,
        Russian
    }

    /// <summary>
    /// Транслітерація кирилиці латиницею.
    /// Українська: офіційна система (постанова Кабінету Міністрів України № 55 від 27.01.2010).
    /// Російська: спрощена система без діакритики (ы → y, э → e, ъ та ь пропускаються).
    /// </summary>
    public static class Transliterator
    {
        // Для кожної літери: (звичайна форма, форма на початку слова)
        private static readonly Dictionary<char, (string Normal, string Initial)> UkrainianMap = new()
        {
            ['а'] = ("a", "a"),
            ['б'] = ("b", "b"),
            ['в'] = ("v", "v"),
            ['г'] = ("h", "h"),
            ['ґ'] = ("g", "g"),
            ['д'] = ("d", "d"),
            ['е'] = ("e", "e"),
            ['є'] = ("ie", "ye"),
            ['ж'] = ("zh", "zh"),
            ['з'] = ("z", "z"),
            ['и'] = ("y", "y"),
            ['і'] = ("i", "i"),
            ['ї'] = ("i", "yi"),
            ['й'] = ("i", "y"),
            ['к'] = ("k", "k"),
            ['л'] = ("l", "l"),
            ['м'] = ("m", "m"),
            ['н'] = ("n", "n"),
            ['о'] = ("o", "o"),
            ['п'] = ("p", "p"),
            ['р'] = ("r", "r"),
            ['с'] = ("s", "s"),
            ['т'] = ("t", "t"),
            ['у'] = ("u", "u"),
            ['ф'] = ("f", "f"),
            ['х'] = ("kh", "kh"),
            ['ц'] = ("ts", "ts"),
            ['ч'] = ("ch", "ch"),
            ['ш'] = ("sh", "sh"),
            ['щ'] = ("shch", "shch"),
            ['ь'] = ("", ""),
            ['ю'] = ("iu", "yu"),
            ['я'] = ("ia", "ya")
        };

        // Російська таблиця: українська з відмінностями
        private static readonly Dictionary<char, (string Normal, string Initial)> RussianMap = BuildRussianMap();

        private static Dictionary<char, (string Normal, string Initial)> BuildRussianMap()
        {
            var map = new Dictionary<char, (string Normal, string Initial)>(UkrainianMap)
            {
                ['г'] = ("g", "g"),
                ['и'] = ("i", "i"),
                ['й'] = ("y", "y"),
                ['ё'] = ("yo", "yo"),
                ['ъ'] = ("", ""),
                ['ы'] = ("y", "y"),
                ['э'] = ("e", "e"),
                ['ю'] = ("yu", "yu"),
                ['я'] = ("ya", "ya")
            };

            return map;
        }

        /// <summary>Чи є в тексті кириличні літери.</summary>
        public static bool ContainsCyrillic(string text) =>
            text.Any(c => c is >= '\u0400' and <= '\u04FF');

        /// <summary>
        /// Визначає абетку за літерами, яких немає в іншій: і, ї, є, ґ означають українську,
        /// ы, э, ъ, ё російську. Якщо їх немає, вважаємо текст українським.
        /// </summary>
        public static CyrillicVariant DetectVariant(string text)
        {
            int ukrainianOnly = text.Count(c => "іїєґІЇЄҐ".Contains(c));
            int russianOnly = text.Count(c => "ыэъёЫЭЪЁ".Contains(c));

            return russianOnly > ukrainianOnly ? CyrillicVariant.Russian : CyrillicVariant.Ukrainian;
        }

        private static bool IsApostrophe(char c) => c is '\'' or '’' or 'ʼ';

        /// <summary>Перетворює кирилицю на латиницю. Усе інше (цифри, латиниця, знаки) лишається як є.</summary>
        public static string ToLatin(string text, CyrillicVariant? variant = null)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            bool russian = (variant ?? DetectVariant(text)) == CyrillicVariant.Russian;
            var map = russian ? RussianMap : UkrainianMap;

            StringBuilder result = new(text.Length * 2);

            int i = 0;

            while (i < text.Length)
            {
                // Усе, що не входить до слова (пробіли, цифри, знаки), копіюємо без змін
                if (!char.IsLetter(text[i]) && !IsApostrophe(text[i]))
                {
                    result.Append(text[i]);
                    i++;
                    continue;
                }

                // Слово: послідовність літер та апострофів
                int start = i;

                while (i < text.Length && (char.IsLetter(text[i]) || IsApostrophe(text[i])))
                    i++;

                AppendWord(text.Substring(start, i - start), map, russian, result);
            }

            return result.ToString();
        }

        private static void AppendWord(
            string word,
            Dictionary<char, (string Normal, string Initial)> map,
            bool russian,
            StringBuilder result)
        {
            // Слово, написане повністю великими літерами (ХАРКІВ), транслітеруємо великими (KHARKIV)
            int letters = 0;
            bool allUpper = true;

            foreach (char c in word)
            {
                if (!map.ContainsKey(char.ToLowerInvariant(c)))
                    continue;

                letters++;

                if (!char.IsUpper(c))
                    allUpper = false;
            }

            allUpper = allUpper && letters >= 2;

            int j = 0;

            while (j < word.Length)
            {
                char c = word[j];

                // Апостроф не передається
                if (IsApostrophe(c))
                {
                    j++;
                    continue;
                }

                char lower = char.ToLowerInvariant(c);

                if (!map.TryGetValue(lower, out var pair))
                {
                    // Не кирилиця (наприклад, латинська літера всередині слова): лишаємо як є
                    result.Append(c);
                    j++;
                    continue;
                }

                string piece;

                if (!russian && lower == 'з' && j + 1 < word.Length &&
                    char.ToLowerInvariant(word[j + 1]) == 'г')
                {
                    // «зг» передається як «zgh», щоб не плутати з «zh» (ж)
                    piece = "zgh";
                    j += 2;
                }
                else
                {
                    // є, ї, й, ю, я на початку слова читаються інакше, ніж усередині
                    piece = j == 0 ? pair.Initial : pair.Normal;
                    j++;
                }

                if (char.IsUpper(c))
                    piece = allUpper ? piece.ToUpperInvariant() : Capitalize(piece);

                result.Append(piece);
            }
        }

        private static string Capitalize(string text) =>
            text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }
}