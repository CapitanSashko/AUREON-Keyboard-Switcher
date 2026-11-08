using System.Text;
using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>
    /// Словари слов для четырёх языков. Для каждого слова хранится «ранг» (номер строки в файле):
    /// чем меньше число, тем чаще слово встречается в языке.
    /// </summary>
    public sealed class WordDictionary
    {
        private readonly Dictionary<KeyboardLanguage, Dictionary<string, int>> _words = new();

        private static readonly (KeyboardLanguage Language, string FileName)[] Files =
        {
            (KeyboardLanguage.English,   "en.txt"),
            (KeyboardLanguage.Russian,   "ru.txt"),
            (KeyboardLanguage.Ukrainian, "uk.txt"),
            (KeyboardLanguage.German,    "de.txt")
        };

        public static string DefaultFolder =>
            Path.Combine(AppContext.BaseDirectory, "Dictionaries");

        /// <summary>
        /// Загружает словари. Возвращает список файлов, которые не нашлись (пустой список = всё хорошо).
        /// </summary>
        public List<string> Load(string? folder = null)
        {
            folder ??= DefaultFolder;
            List<string> missing = new();

            foreach (var (language, fileName) in Files)
            {
                string path = Path.Combine(folder, fileName);

                if (!File.Exists(path))
                {
                    missing.Add(path);
                    continue;
                }

                var map = new Dictionary<string, int>(StringComparer.Ordinal);
                int rank = 0;

                foreach (string line in File.ReadLines(path, Encoding.UTF8))
                {
                    string word = Normalize(line);

                    if (word.Length == 0)
                        continue;

                    map.TryAdd(word, rank);
                    rank++;
                }

                _words[language] = map;
            }

            return missing;
        }

        /// <summary>Ранг слова в словаре языка или -1, если слова нет.</summary>
        public int GetRank(KeyboardLanguage language, string word)
        {
            if (!_words.TryGetValue(language, out var map))
                return -1;

            return map.TryGetValue(Normalize(word), out int rank) ? rank : -1;
        }

        // «ß» хранится в словарях как «ss» (так устроен источник списков слов)
        private static string Normalize(string word) =>
            word.Trim()
                .ToLowerInvariant()
                .Replace('’', '\'')
                .Replace('ʼ', '\'')
                .Replace("ß", "ss");
    }
}