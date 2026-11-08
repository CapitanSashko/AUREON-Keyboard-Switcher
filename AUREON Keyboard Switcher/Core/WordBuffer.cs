using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AUREON_Keyboard_Switcher.Keyboard;
using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>Завершённое слово фразы: физические клавиши и раскладка, в которой оно набрано.</summary>
    public readonly record struct PhraseWord(string Word, KeyboardLanguage? Layout);

    /// <summary>
    /// Накапливает клавиши текущего слова и помнит несколько предыдущих слов фразы.
    /// Хранит ФИЗИЧЕСКИЕ клавиши в «английской» записи (клавиша Ы = 's', Shift+2 = '@' и т.д.),
    /// поэтому слово можно показать в любой раскладке через KeyboardLayoutConverter.
    /// </summary>
    public sealed class WordBuffer
    {
        private const int MaxHistory = 4;

        private readonly List<char> _keys = new();
        private readonly List<PhraseWord> _history = new();

        private string? _lastWord;
        private Keys _lastDelimiter;

        public int Length => _keys.Count;

        /// <summary>Слово, которое набирается прямо сейчас (в английской записи).</summary>
        public string CurrentWord => new string(_keys.ToArray());

        /// <summary>
        /// Последнее завершённое слово (в английской записи) или null.
        /// Пока вы не нажали ничего другого, оно ещё стоит на экране прямо перед курсором.
        /// </summary>
        public string? LastWord => _lastWord;

        /// <summary>Чем закончилось последнее слово: Space, Enter или Tab.</summary>
        public Keys LastDelimiter => _lastDelimiter;

        /// <summary>
        /// Предыдущие слова текущей фразы (старые первыми). Каждое отделено от следующего ровно одним пробелом.
        /// Во время события WordCompleted здесь ещё нет слова, которое только что закончилось.
        /// </summary>
        public IReadOnlyList<PhraseWord> History => _history;

        /// <summary>
        /// Слово закончилось (пробел, Enter или Tab).
        /// Параметры: слово в английской записи и клавиша-разделитель.
        /// </summary>
        public event Action<string, Keys>? WordCompleted;

        /// <summary>Полный сброс: набираемое слово, история фразы и память о последнем слове.</summary>
        public void Clear()
        {
            _keys.Clear();
            _history.Clear();
            _lastWord = null;
        }

        /// <summary>Слово так, как оно выглядит на экране в раскладке current.</summary>
        public string GetText(KeyboardLanguage current) =>
            KeyboardLayoutConverter.Convert(
                new string(_keys.ToArray()),
                KeyboardLanguage.English,
                current);

        /// <param name="e">Нажатая клавиша.</param>
        /// <param name="layout">Текущая раскладка (нужна только для клавиш-разделителей, иначе null).</param>
        public void Process(KeyEvent e, KeyboardLanguage? layout = null)
        {
            // Сами по себе клавиши-модификаторы буфер не трогают
            // (иначе нажатие Ctrl перед горячей клавишей стёрло бы слово)
            if (e.Key is Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey
                      or Keys.ControlKey or Keys.LControlKey or Keys.RControlKey
                      or Keys.Menu or Keys.LMenu or Keys.RMenu
                      or Keys.CapsLock or Keys.LWin or Keys.RWin)
            {
                return;
            }

            // Ctrl/Alt (и AltGr) вместе с обычной клавишей: это горячая клавиша, а не набор текста
            if (e.Ctrl || e.Alt)
            {
                Clear();
                return;
            }

            switch (e.Key)
            {
                case Keys.Back:
                    if (_keys.Count > 0)
                    {
                        // Правка внутри набираемого слова
                        _keys.RemoveAt(_keys.Count - 1);
                        _lastWord = null;
                    }
                    else
                    {
                        // Стёрт пробел или символ перед курсором: фраза больше не актуальна
                        Clear();
                    }
                    return;

                case Keys.Space:
                case Keys.Enter:
                case Keys.Tab:
                    if (_keys.Count > 0)
                    {
                        string word = new string(_keys.ToArray());

                        // Обработчик видит историю БЕЗ этого слова
                        WordCompleted?.Invoke(word, e.Key);

                        _keys.Clear();

                        _lastWord = word;
                        _lastDelimiter = e.Key;

                        if (e.Key == Keys.Space)
                        {
                            _history.Add(new PhraseWord(word, layout));

                            if (_history.Count > MaxHistory)
                                _history.RemoveAt(0);
                        }
                        else
                        {
                            // Enter/Tab: новая строка или поле, фраза закончилась
                            _history.Clear();
                        }
                    }
                    else
                    {
                        // Двойной пробел и т.п.: на экране больше не «слово + один пробел»
                        Clear();
                    }
                    return;

                // Сами по себе модификаторы буфер не трогают
                case Keys.ShiftKey:
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                case Keys.ControlKey:
                case Keys.LControlKey:
                case Keys.RControlKey:
                case Keys.Menu:
                case Keys.LMenu:
                case Keys.RMenu:
                case Keys.CapsLock:
                case Keys.LWin:
                case Keys.RWin:
                    return;
            }

            if (TryGetChar(e, out char ch))
            {
                _lastWord = null;
                _keys.Add(ch);
            }
            else
            {
                // Стрелки, Home/End, Esc, F-клавиши и т.п.:
                // курсор мог переместиться, слово больше не актуально.
                Clear();
            }
        }

        /// <summary>
        /// Переводит нажатую клавишу в символ американской (английской) раскладки,
        /// то есть определяет, какая ФИЗИЧЕСКАЯ клавиша нажата.
        /// </summary>
        private static bool TryGetChar(KeyEvent e, out char ch)
        {
            Keys key = e.Key;

            // Цифры на цифровой клавиатуре одинаковы во всех раскладках
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                ch = (char)('0' + (key - Keys.NumPad0));
                return true;
            }

            // Остальные клавиши цифровой клавиатуры слово не продолжают
            // (у «/» на цифровой клавиатуре тот же скан-код, что и у основного блока)
            if (key is Keys.Divide or Keys.Multiply or Keys.Subtract or Keys.Add or Keys.Decimal)
            {
                ch = '\0';
                return false;
            }

            // 1. Основной блок: клавиша определяется по скан-коду, то есть по её положению.
            //    Это работает одинаково для любой раскладки, включая немецкую (Y/Z, ö, ä, ü, ß).
            if (KeyboardLayoutConverter.TryGetKeyIndex(e.ScanCode, out int index))
            {
                char normal = KeyboardLayoutConverter.GetPhysicalChar(index, shift: false);

                if (char.IsLetter(normal))
                {
                    // Буквы: Shift и CapsLock вместе отменяют друг друга
                    ch = (e.Shift ^ e.CapsLock) ? char.ToUpperInvariant(normal) : normal;
                }
                else
                {
                    ch = KeyboardLayoutConverter.GetPhysicalChar(index, e.Shift);
                }

                return true;
            }

            // 2. Запасной путь (виртуальные клавиатуры без скан-кода): по коду клавиши для US-раскладки
            if (key >= Keys.A && key <= Keys.Z)
            {
                char letter = (char)('a' + (key - Keys.A));
                ch = (e.Shift ^ e.CapsLock) ? char.ToUpperInvariant(letter) : letter;
                return true;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int digit = key - Keys.D0;
                ch = e.Shift ? ")!@#$%^&*("[digit] : (char)('0' + digit);
                return true;
            }

            var (normalChar, shiftedChar) = key switch
            {
                Keys.Oemtilde => ('`', '~'),
                Keys.OemMinus => ('-', '_'),
                Keys.Oemplus => ('=', '+'),
                Keys.OemOpenBrackets => ('[', '{'),
                Keys.Oem6 => (']', '}'),
                Keys.Oem5 => ('\\', '|'),
                Keys.Oem1 => (';', ':'),
                Keys.Oem7 => ('\'', '"'),
                Keys.Oemcomma => (',', '<'),
                Keys.OemPeriod => ('.', '>'),
                Keys.OemQuestion => ('/', '?'),
                _ => ('\0', '\0')
            };

            ch = e.Shift ? shiftedChar : normalChar;
            return normalChar != '\0';
        }
    }
}