using System.Text;
using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>
    /// Зворотна транслітерація: латиниця → кирилиця.
    ///
    /// Спочатку слово шукається в індексі, побудованому зі словника: для кожного кириличного слова
    /// обчислюється його латинський запис, і за ним відновлюється найчастіше слово. Так повертаються
    /// м'який знак («Lviv» → «Львів») та правильні і/ї/й («Kyiv» → «Київ»).
    /// Якщо слова в індексі немає (прізвища, рідкісні слова), працюють правила, обернені до
    /// офіційної української системи (постанова КМУ № 55 від 2010 р.) або до спрощеної російської.
    /// </summary>
    public sealed class ReverseTransliterator
    {
        private const string UkrainianVowels = "аеєиіїоуюя";
        private const string RussianVowels = "аеёиоуыэюя";

        // Двобуквені сполучення, спільні для обох абеток
        private static readonly (string Latin, char Cyrillic)[] CommonPairs =
        {
            ("kh", 'х'), ("zh", 'ж'), ("ts", 'ц'), ("ch", 'ч'), ("sh", 'ш')
        };

        // Українська: сполучення на початку слова
        private static readonly (string Latin, char Cyrillic)[] UkrainianInitialPairs =
        {
            ("ye", 'є'), ("yi", 'ї'), ("yu", 'ю'), ("ya", 'я')
        };

        // Українська: сполучення всередині слова
        private static readonly (string Latin, char Cyrillic)[] UkrainianInnerPairs =
        {
            ("ie", 'є'), ("iu", 'ю'), ("ia", 'я')
        };

        // Російська
        private static readonly (string Latin, char Cyrillic)[] RussianPairs =
        {
            ("yo", 'ё'), ("yu", 'ю'), ("ya", 'я')
        };

        // Окремі літери. Літери i та y залежать від контексту й обробляються окремо
        private static readonly Dictionary<char, string> UkrainianLetters = new()
        {
            ['a'] = "а",
            ['b'] = "б",
            ['c'] = "ц",
            ['d'] = "д",
            ['e'] = "е",
            ['f'] = "ф",
            ['g'] = "ґ",
            ['h'] = "г",
            ['j'] = "й",
            ['k'] = "к",
            ['l'] = "л",
            ['m'] = "м",
            ['n'] = "н",
            ['o'] = "о",
            ['p'] = "п",
            ['q'] = "к",
            ['r'] = "р",
            ['s'] = "с",
            ['t'] = "т",
            ['u'] = "у",
            ['v'] = "в",
            ['w'] = "в",
            ['x'] = "кс",
            ['z'] = "з"
        };

        private static readonly Dictionary<char, string> RussianLetters = new()
        {
            ['a'] = "а",
            ['b'] = "б",
            ['c'] = "ц",
            ['d'] = "д",
            ['e'] = "е",
            ['f'] = "ф",
            ['g'] = "г",
            ['h'] = "х",
            ['i'] = "и",
            ['j'] = "й",
            ['k'] = "к",
            ['l'] = "л",
            ['m'] = "м",
            ['n'] = "н",
            ['o'] = "о",
            ['p'] = "п",
            ['q'] = "к",
            ['r'] = "р",
            ['s'] = "с",
            ['t'] = "т",
            ['u'] = "у",
            ['v'] = "в",
            ['w'] = "в",
            ['x'] = "кс",
            ['z'] = "з"
        };

        private readonly WordDictionary _dictionary;
        private readonly object _lock = new();

        // Для кожної абетки: латинський запис слова → найчастіше слово з таким записом
        private readonly Dictionary<CyrillicVariant, Dictionary<string, (string Word, int Rank)>> _indexes = new();

        public ReverseTransliterator(WordDictionary dictionary)
        {
            _dictionary = dictionary;
        }

        public static bool IsLatinLetter(char c) =>
            c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

        public static bool ContainsLatin(string text) => text.Any(IsLatinLetter);

        // ==================== ВИБІР АБЕТКИ ====================

        /// <summary>
        /// Вибирає, українською чи російською записано латинський текст: за кількістю слів,
        /// знайдених у словнику. Якщо порівну (або слів немає), береться бажана абетка.
        /// </summary>
        public CyrillicVariant ChooseVariant(string text, CyrillicVariant preferred)
        {
            List<string> words = LatinWords(text);

            if (words.Count == 0)
                return preferred;

            int preferredHits = CountHits(words, preferred);

            // Усі слова знайшлися в бажаній абетці: другу перевіряти не потрібно
            if (preferredHits == words.Count)
                return preferred;

            CyrillicVariant other = preferred == CyrillicVariant.Ukrainian
                ? CyrillicVariant.Russian
                : CyrillicVariant.Ukrainian;

            return CountHits(words, other) > preferredHits ? other : preferred;
        }

        private int CountHits(List<string> words, CyrillicVariant variant)
        {
            var index = GetIndex(variant);

            return words.Count(w => index.ContainsKey(w.ToLowerInvariant()));
        }

        private static List<string> LatinWords(string text)
        {
            List<string> words = new();
            int i = 0;

            while (i < text.Length)
            {
                if (!IsLatinLetter(text[i]))
                {
                    i++;
                    continue;
                }

                int start = i;

                while (i < text.Length && IsLatinLetter(text[i]))
                    i++;

                words.Add(text.Substring(start, i - start));
            }

            return words;
        }

        // ==================== ПЕРЕТВОРЕННЯ ====================

        /// <summary>Перетворює латинські слова тексту на кириличні. Усе інше лишається без змін.</summary>
        public string ToCyrillic(string text, CyrillicVariant variant)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var index = GetIndex(variant);
            StringBuilder result = new(text.Length);

            int i = 0;

            while (i < text.Length)
            {
                if (!IsLatinLetter(text[i]))
                {
                    result.Append(text[i]);
                    i++;
                    continue;
                }

                int start = i;

                while (i < text.Length && IsLatinLetter(text[i]))
                    i++;

                string word = text.Substring(start, i - start);
                string lower = word.ToLowerInvariant();

                // Спочатку словник, потім правила
                string cyrillic = index.TryGetValue(lower, out var hit)
                    ? hit.Word
                    : variant == CyrillicVariant.Russian
                        ? RulesRussian(lower)
                        : RulesUkrainian(lower);

                result.Append(ApplyCase(word, cyrillic));
            }

            return result.ToString();
        }

        // Переносить регістр латинського слова на кириличне: KHARKIV → ХАРКІВ, Kharkiv → Харків
        private static string ApplyCase(string source, string cyrillic)
        {
            if (cyrillic.Length == 0)
                return cyrillic;

            if (source.Length >= 2 && source.All(char.IsUpper))
                return cyrillic.ToUpperInvariant();

            if (char.IsUpper(source[0]))
                return char.ToUpperInvariant(cyrillic[0]) + cyrillic[1..];

            return cyrillic;
        }

        // ==================== ІНДЕКС ЗІ СЛОВНИКА ====================

        // Будується один раз для кожної абетки. Викликати з фонового потоку:
        // для великого словника це триває до секунди.
        private Dictionary<string, (string Word, int Rank)> GetIndex(CyrillicVariant variant)
        {
            lock (_lock)
            {
                if (_indexes.TryGetValue(variant, out var ready))
                    return ready;

                KeyboardLanguage language = variant == CyrillicVariant.Russian
                    ? KeyboardLanguage.Russian
                    : KeyboardLanguage.Ukrainian;

                var index = new Dictionary<string, (string Word, int Rank)>(StringComparer.Ordinal);

                foreach (var (word, rank) in _dictionary.EnumerateWords(language))
                {
                    string key = Transliterator.ToLatin(word, variant).ToLowerInvariant();

                    if (key.Length == 0)
                        continue;

                    // Однаковий латинський запис мають різні слова: лишаємо найчастіше
                    if (!index.TryGetValue(key, out var existing) || rank < existing.Rank)
                        index[key] = (word, rank);
                }

                _indexes[variant] = index;
                return index;
            }
        }

        // ==================== ПРАВИЛА (запасний варіант) ====================

        private static bool At(string word, int index, string text) =>
            string.CompareOrdinal(word, index, text, 0, text.Length) == 0;

        private static bool TryPair(
            (string Latin, char Cyrillic)[] pairs,
            string word,
            int index,
            out char cyrillic)
        {
            foreach (var (latin, cyr) in pairs)
            {
                if (At(word, index, latin))
                {
                    cyrillic = cyr;
                    return true;
                }
            }

            cyrillic = '\0';
            return false;
        }

        private static string RulesUkrainian(string word)
        {
            StringBuilder result = new(word.Length + 2);
            int i = 0;

            while (i < word.Length)
            {
                bool initial = i == 0;

                if (At(word, i, "shch"))
                {
                    result.Append('щ');
                    i += 4;
                    continue;
                }

                // «зг» передається як «zgh», щоб не плутати з «zh» (ж)
                if (At(word, i, "zgh"))
                {
                    result.Append("зг");
                    i += 3;
                    continue;
                }

                if (TryPair(CommonPairs, word, i, out char pair) ||
                    TryPair(initial ? UkrainianInitialPairs : UkrainianInnerPairs, word, i, out pair))
                {
                    result.Append(pair);
                    i += 2;
                    continue;
                }

                char c = word[i];

                if (c == 'i')
                {
                    // Після голосної: «й» наприкінці слова («Yurii» → «Юрій») і «ї» всередині
                    // («Kyiv» → «Київ», «Mykolaiv» → «Миколаїв»); після приголосної: «і»
                    char previous = result.Length > 0 ? result[^1] : '\0';

                    if (previous != '\0' && UkrainianVowels.Contains(previous))
                        result.Append(i == word.Length - 1 ? 'й' : 'ї');
                    else
                        result.Append('і');
                }
                else if (c == 'y')
                {
                    // На початку слова «й» («Yosyp» → «Йосип»), далі «и»
                    result.Append(initial ? 'й' : 'и');
                }
                else if (UkrainianLetters.TryGetValue(c, out string? letter))
                {
                    result.Append(letter);
                }
                else
                {
                    result.Append(c);
                }

                i++;
            }

            return result.ToString();
        }

        private static string RulesRussian(string word)
        {
            StringBuilder result = new(word.Length + 2);
            int i = 0;

            while (i < word.Length)
            {
                bool initial = i == 0;

                if (At(word, i, "shch"))
                {
                    result.Append('щ');
                    i += 4;
                    continue;
                }

                if (TryPair(CommonPairs, word, i, out char pair) ||
                    TryPair(RussianPairs, word, i, out pair))
                {
                    result.Append(pair);
                    i += 2;
                    continue;
                }

                if (At(word, i, "ye"))
                {
                    result.Append(initial ? "е" : "ье");
                    i += 2;
                    continue;
                }

                char c = word[i];

                if (c == 'y')
                {
                    // Після голосної «й» («moy» → «мой»), після приголосної «ы» («syn» → «сын»)
                    char previous = result.Length > 0 ? result[^1] : '\0';

                    result.Append(previous != '\0' && RussianVowels.Contains(previous) ? 'й' : 'ы');
                }
                else if (RussianLetters.TryGetValue(c, out string? letter))
                {
                    result.Append(letter);
                }
                else
                {
                    result.Append(c);
                }

                i++;
            }

            return result.ToString();
        }
    }
}