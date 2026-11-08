using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>Рішення щодо слова: перемикати розкладку чи ні.</summary>
    /// <param name="OriginalCore">Як слово (без знаків у кінці) виглядало на екрані до виправлення.</param>
    public readonly record struct LayoutAdvice(
        bool ShouldSwitch,
        KeyboardLanguage Target,
        string CorrectedText,
        string Reason,
        string OriginalCore = "");

    public sealed class LayoutAdvisor
    {
        // Слова коротші за 2 символи не аналізуємо (однолітерні занадто неоднозначні)
        private const int MinLength = 2;

        // Дволітерне слово приймаємо як виправлення, лише якщо воно дуже частотне в цільовій мові
        private const int ShortWordLength = 2;
        private const int ShortWordMaxRank = 300;

        // «Правильним у поточній розкладці» коротке слово вважаємо, лише якщо воно справді частотне:
        // у словниках повно сміттєвих токенів (майже всі дволітерні комбінації).
        private const int TwoLetterCurrentMaxRank = 3000;
        private const int ThreeLetterCurrentMaxRank = 20000;

        // Знаки, які можуть стояти в кінці слова як звичайна пунктуація
        private const string TailChars = ".,!?;:\")";

        // Однолітерні слова: їх можна виправляти лише як «супутників» уже виправлюваного слова
        private static readonly Dictionary<KeyboardLanguage, string> SingleLetterWords = new()
        {
            [KeyboardLanguage.English] = "ai",
            [KeyboardLanguage.Ukrainian] = "авізуйоя",
            [KeyboardLanguage.Russian] = "авикосуя",
            [KeyboardLanguage.German] = string.Empty
        };

        private readonly WordDictionary _dictionary;

        /// <summary>Яку мову віддати перевагу, якщо слово однаково частотне в двох мовах.</summary>
        public KeyboardLanguage Preferred { get; set; } = KeyboardLanguage.Ukrainian;

        /// <summary>Чи виправляти дволітерні слова (Як, на, до…).</summary>
        public bool FixTwoLetterWords { get; set; } = true;

        /// <summary>Слова користувача (як вони виглядають на екрані), які чіпати не можна.</summary>
        public ISet<string> UserWords { get; set; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public LayoutAdvisor(WordDictionary dictionary)
        {
            _dictionary = dictionary;
        }

        /// <param name="englishWord">Слово в «англійському» записі (те, що віддає WordBuffer).</param>
        /// <param name="current">Поточна розкладка.</param>
        /// <param name="installed">Встановлені в користувача розкладки.</param>
        public LayoutAdvice Evaluate(
            string englishWord,
            KeyboardLanguage current,
            IReadOnlyList<KeyboardLanguage> installed)
        {
            LayoutAdvice Keep(string reason) =>
                new(false, current, string.Empty, reason);

            if (englishWord.Length < MinLength)
                return Keep("занадто коротке слово");

            // Що користувач бачить на екрані зараз
            string typed = KeyboardLayoutConverter.Convert(
                englishWord, KeyboardLanguage.English, current);

            if (typed.Any(char.IsDigit))
                return Keep("у слові є цифри");

            if (HasInnerUpper(typed))
                return Keep("змішаний регістр (ім'я, код)");

            int tailLength = CountTail(typed);

            if (tailLength >= typed.Length)
                return Keep("у слові немає літер");

            string typedCore = tailLength > 0 ? typed[..^tailLength] : typed;

            // Слово зі словника користувача: не чіпаємо ніколи
            if (UserWords.Contains(typedCore.Trim()))
                return Keep("слово є у словнику користувача");

            // Слово нормальне в поточній розкладці: нічого не робимо
            if (GetCurrentRank(current, typed) >= 0 ||
                (tailLength > 0 && GetCurrentRank(current, typedCore) >= 0))
            {
                return Keep("слово є у словнику поточної розкладки");
            }

            string tail = tailLength > 0 ? typed[^tailLength..] : string.Empty;
            string coreEnglish = tailLength > 0 ? englishWord[..^tailLength] : englishWord;

            bool found = false;
            int bestRank = int.MaxValue;
            KeyboardLanguage bestLanguage = current;
            string bestText = string.Empty;

            foreach (KeyboardLanguage language in installed)
            {
                if (language == current)
                    continue;

                string? text = null;
                int rank = -1;

                // Варіант 1: все слово цілком
                string whole = KeyboardLayoutConverter.Convert(
                    englishWord, KeyboardLanguage.English, language);

                int wholeRank = GetCandidateRank(language, whole);

                if (wholeRank >= 0 && !Same(whole, typed))
                {
                    text = whole;
                    rank = wholeRank;
                }
                else if (tailLength > 0)
                {
                    // Варіант 2: без знака пунктуації в кінці
                    string core = KeyboardLayoutConverter.Convert(
                        coreEnglish, KeyboardLanguage.English, language);

                    int coreRank = GetCandidateRank(language, core);

                    if (coreRank >= 0 && !Same(core + tail, typed))
                    {
                        text = core + tail;
                        rank = coreRank;
                    }
                }

                if (text == null)
                    continue;

                bool better =
                    !found ||
                    rank < bestRank ||
                    (rank == bestRank && language == Preferred);

                if (better)
                {
                    found = true;
                    bestRank = rank;
                    bestLanguage = language;
                    bestText = text;
                }
            }

            if (!found)
                return Keep("слова немає в жодному словнику");

            return new LayoutAdvice(
                true,
                bestLanguage,
                bestText,
                $"слово є у словнику {bestLanguage}, а в {current} його немає",
                typedCore);
        }

        /// <summary>
        /// Перевірка для ПОПЕРЕДНІХ слів фрази: чи можна виправити слово разом із сусіднім,
        /// яке вже визнано набраним не в тій розкладці.
        /// Слово виправляється, лише якщо в поточній розкладці це не слово,
        /// а в цільовій це слово зі словника (або однолітерне слово з короткого списку).
        /// </summary>
        public bool TryConvertCompanion(
            string englishWord,
            KeyboardLanguage current,
            KeyboardLanguage target,
            out string corrected)
        {
            corrected = string.Empty;

            if (englishWord.Length == 0)
                return false;

            string typed = KeyboardLayoutConverter.Convert(
                englishWord, KeyboardLanguage.English, current);

            if (typed.Any(char.IsDigit) || HasInnerUpper(typed))
                return false;

            int tailLength = CountTail(typed);

            if (tailLength >= typed.Length)
                return false;

            string typedCore = tailLength > 0 ? typed[..^tailLength] : typed;

            // Слово користувача та нормальне слово поточної розкладки чіпати не можна
            if (UserWords.Contains(typedCore.Trim()))
                return false;

            if (GetCurrentRank(current, typed) >= 0 ||
                (tailLength > 0 && GetCurrentRank(current, typedCore) >= 0))
            {
                return false;
            }

            if (typedCore.Length == 1 && IsSingleLetterWord(current, typedCore))
                return false;

            string tail = tailLength > 0 ? typed[^tailLength..] : string.Empty;
            string coreEnglish = tailLength > 0 ? englishWord[..^tailLength] : englishWord;

            string whole = KeyboardLayoutConverter.Convert(
                englishWord, KeyboardLanguage.English, target);

            if (IsCompanionWord(target, whole) && !Same(whole, typed))
            {
                corrected = whole;
                return true;
            }

            if (tailLength > 0)
            {
                string core = KeyboardLayoutConverter.Convert(
                    coreEnglish, KeyboardLanguage.English, target);

                if (IsCompanionWord(target, core) && !Same(core + tail, typed))
                {
                    corrected = core + tail;
                    return true;
                }
            }

            return false;
        }

        private bool IsCompanionWord(KeyboardLanguage language, string text)
        {
            string word = text.Trim();

            if (word.Length == 1)
                return IsSingleLetterWord(language, word);

            return GetCandidateRank(language, word) >= 0;
        }

        private static bool IsSingleLetterWord(KeyboardLanguage language, string letter) =>
            SingleLetterWords.TryGetValue(language, out string? letters) &&
            letters.Contains(char.ToLowerInvariant(letter[0]));

        private static bool Same(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>Ранг слова в поточній розкладці; для коротких слів лише частотні вважаються «правильними».</summary>
        private int GetCurrentRank(KeyboardLanguage language, string text)
        {
            string word = text.Trim();

            if (word.Length < MinLength)
                return -1;

            int rank = _dictionary.GetRank(language, word);

            if (rank < 0)
                return -1;

            int limit = word.Length switch
            {
                2 => TwoLetterCurrentMaxRank,
                3 => ThreeLetterCurrentMaxRank,
                _ => int.MaxValue
            };

            return rank <= limit ? rank : -1;
        }

        /// <summary>Ранг слова як кандидата на виправлення; короткі слова лише дуже частотні.</summary>
        private int GetCandidateRank(KeyboardLanguage language, string text)
        {
            string word = text.Trim();

            if (word.Length < MinLength)
                return -1;

            int rank = _dictionary.GetRank(language, word);

            if (word.Length == ShortWordLength && !FixTwoLetterWords)
                return -1;

            if (rank >= 0 && word.Length <= ShortWordLength && rank > ShortWordMaxRank)
                return -1;

            return rank;
        }

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

        /// <summary>Великі літери всередині слова (iPhone, MyClass), але не слово цілком капсом.</summary>
        private static bool HasInnerUpper(string text)
        {
            bool allUpper = text.Where(char.IsLetter).All(char.IsUpper);

            if (allUpper)
                return false;

            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]))
                    return true;
            }

            return false;
        }
    }
}