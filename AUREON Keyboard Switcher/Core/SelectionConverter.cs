using System.Diagnostics;
using System.Runtime.InteropServices;
using AUREON_Keyboard_Switcher.Keyboard;
using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>
    /// Перетворення виділеного тексту (зміна розкладки, транслітерація) через буфер обміну.
    /// Викликати з потоку інтерфейсу.
    /// </summary>
    public sealed class SelectionConverter
    {
        private const ushort VK_C = 0x43;
        private const ushort VK_V = 0x56;

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        private static readonly char[] WordTrimChars =
            { '.', ',', '!', '?', ';', ':', '"', '(', ')', '«', '»', '-' };

        private readonly WordDictionary _dictionary;
        private bool _busy;

        /// <summary>Яку мову віддати перевагу, якщо у словниках виходить порівну.</summary>
        public KeyboardLanguage Preferred { get; set; } = KeyboardLanguage.Ukrainian;

        public SelectionConverter(WordDictionary dictionary)
        {
            _dictionary = dictionary;
        }

        /// <summary>Перетворює виділений текст в іншу розкладку.</summary>
        /// <param name="triggerKey">Код клавіші сполучення (щоб дочекатися її відпускання).</param>
        public Task ConvertAsync(int triggerKey = 0x13) => RunAsync(triggerKey, ConvertText);

        /// <summary>Транслітерує виділений текст (кирилиця → латиниця).</summary>
        public Task TransliterateAsync(int triggerKey = 0x13) => RunAsync(triggerKey, Transliterate);

        private async Task RunAsync(
            int triggerKey,
            Func<string, (string Text, KeyboardLanguage? Target)?> transform)
        {
            if (_busy)
                return;

            _busy = true;

            DataObject? backup = null;
            bool clipboardChanged = false;

            try
            {
                // 1. Чекаємо, поки користувач відпустить клавіші сполучення,
                //    інакше наше Ctrl+C перетворилося б на Ctrl+Shift+C
                if (!await WaitAsync(() => KeyboardInjector.AreHotkeyKeysReleased(triggerKey), 1000))
                {
                    Debug.WriteLine("AUREON | виділення: клавіші не відпущено, скасовано");
                    return;
                }

                // 2. Запам'ятовуємо буфер обміну
                backup = BackupClipboard();

                // 3. Копіюємо виділене
                uint before = GetClipboardSequenceNumber();
                KeyboardInjector.SendCtrlKey(VK_C);

                if (!await WaitAsync(() => GetClipboardSequenceNumber() != before, 600))
                {
                    Debug.WriteLine("AUREON | виділення: нічого не виділено");
                    return;
                }

                clipboardChanged = true;

                string? text = Retry(() => Clipboard.GetText(TextDataFormat.UnicodeText));

                if (string.IsNullOrEmpty(text))
                {
                    Debug.WriteLine("AUREON | виділення: у виділеному немає тексту");
                    return;
                }

                // 4. Перетворюємо
                (string Text, KeyboardLanguage? Target)? result = transform(text);

                if (result == null)
                {
                    Debug.WriteLine("AUREON | виділення: нічого перетворювати");
                    return;
                }

                // 5. Вставляємо результат на місце виділеного
                string converted = result.Value.Text;

                if (!Retry(() => { Clipboard.SetText(converted); return true; }))
                {
                    Debug.WriteLine("AUREON | виділення: буфер обміну зайнятий іншою програмою");
                    return;
                }

                await Task.Delay(30);
                KeyboardInjector.SendCtrlKey(VK_V);

                // Даємо програмі час обробити вставку, перш ніж повернути буфер
                await Task.Delay(300);

                if (result.Value.Target is KeyboardLanguage target)
                    LayoutSwitcher.SwitchTo(target);

                // Сам текст у журнал не пишемо
                AppLog.Info("Виділений текст перетворено");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AUREON | помилка перетворення виділеного: " + ex.Message);
                AppLog.Error("Помилка перетворення виділеного тексту", ex);
            }
            finally
            {
                if (clipboardChanged)
                    RestoreClipboard(backup);

                _busy = false;
            }
        }

        // ==================== ТРАНСЛІТЕРАЦІЯ ====================

        private static (string Text, KeyboardLanguage? Target)? Transliterate(string text)
        {
            if (!Transliterator.ContainsCyrillic(text))
                return null;

            string latin = Transliterator.ToLatin(text);

            // Розкладку не змінюємо: результат лише записується на місце виділеного
            return string.Equals(latin, text, StringComparison.Ordinal)
                ? null
                : (latin, null);
        }

        // ==================== ЗМІНА РОЗКЛАДКИ ====================

        private (string Text, KeyboardLanguage? Target)? ConvertText(string text)
        {
            int latin = 0, cyrillic = 0, ukrainianOnly = 0, russianOnly = 0, germanOnly = 0;

            foreach (char c in text)
            {
                if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
                {
                    latin++;
                }
                else if ("äöüßÄÖÜ".IndexOf(c) >= 0)
                {
                    latin++;
                    germanOnly++;
                }
                else if (c is >= '\u0400' and <= '\u04FF')
                {
                    cyrillic++;

                    if ("іїєґІЇЄҐ".IndexOf(c) >= 0)
                        ukrainianOnly++;
                    else if ("ыэъёЫЭЪЁ".IndexOf(c) >= 0)
                        russianOnly++;
                }
            }

            if (latin == 0 && cyrillic == 0)
                return null;

            List<KeyboardLanguage> installed = InstalledLayouts.GetInstalled();

            KeyboardLanguage source;
            List<KeyboardLanguage> targets;

            if (cyrillic > latin)
            {
                // Кирилиця: переводимо в латинську розкладку (англійську або німецьку)
                source = ukrainianOnly > russianOnly
                    ? KeyboardLanguage.Ukrainian
                    : KeyboardLanguage.Russian;

                targets = new() { KeyboardLanguage.English };

                if (installed.Contains(KeyboardLanguage.German))
                    targets.Add(KeyboardLanguage.German);
            }
            else
            {
                // Латиниця: літери ä ö ü ß означають, що текст набрано на німецькій розкладці
                source = germanOnly > 0 && installed.Contains(KeyboardLanguage.German)
                    ? KeyboardLanguage.German
                    : KeyboardLanguage.English;

                targets = installed.Where(l => l != source).ToList();
            }

            KeyboardLanguage bestLanguage = source;
            string bestText = string.Empty;
            int bestScore = -1;

            // Бажану мову перевіряємо першою: за рівного рахунку перемагає вона
            foreach (KeyboardLanguage language in targets.OrderByDescending(l => l == Preferred))
            {
                string converted = KeyboardLayoutConverter.Convert(text, source, language);

                // Нічого не змінилося (наприклад, англійська ↔ німецька для слів без y/z і умлаутів)
                if (string.Equals(converted, text, StringComparison.Ordinal))
                    continue;

                int score = CountKnownWords(language, converted);

                if (score > bestScore)
                {
                    bestLanguage = language;
                    bestText = converted;
                    bestScore = score;
                }
            }

            return bestScore < 0 ? null : (bestText, bestLanguage);
        }

        private int CountKnownWords(KeyboardLanguage language, string text)
        {
            int count = 0;

            foreach (string raw in text.Split(
                         new[] { ' ', '\t', '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string word = raw.Trim(WordTrimChars);

                if (word.Length >= 3 && _dictionary.GetRank(language, word) >= 0)
                    count++;
            }

            return count;
        }

        // ==================== БУФЕР ОБМІНУ ====================

        /// <summary>Копіює вміст буфера обміну, щоб потім повернути його на місце.</summary>
        private static DataObject? BackupClipboard()
        {
            return Retry(() =>
            {
                IDataObject? source = Clipboard.GetDataObject();

                if (source == null)
                    return null;

                DataObject copy = new();

                foreach (string format in source.GetFormats(false))
                {
                    try
                    {
                        object? data = source.GetData(format, false);

                        if (data != null)
                            copy.SetData(format, false, data);
                    }
                    catch
                    {
                        // Екзотичний формат, який не вдалося скопіювати: пропускаємо
                    }
                }

                return copy;
            });
        }

        private static void RestoreClipboard(DataObject? backup)
        {
            try
            {
                if (backup == null || backup.GetFormats(false).Length == 0)
                {
                    Retry(() => { Clipboard.Clear(); return true; });
                    return;
                }

                Retry(() => { Clipboard.SetDataObject(backup, true); return true; });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("AUREON | не вдалося повернути буфер обміну: " + ex.Message);
            }
        }

        /// <summary>Буфер обміну іноді зайнятий іншою програмою: пробуємо кілька разів.</summary>
        private static T? Retry<T>(Func<T> action)
        {
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    return action();
                }
                catch (ExternalException)
                {
                    Thread.Sleep(25);
                }
            }

            return default;
        }

        // ==================== ОЧІКУВАННЯ ====================

        private static async Task<bool> WaitAsync(Func<bool> condition, int timeoutMs)
        {
            Stopwatch timer = Stopwatch.StartNew();

            while (timer.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                    return true;

                await Task.Delay(20);
            }

            return condition();
        }
    }
}