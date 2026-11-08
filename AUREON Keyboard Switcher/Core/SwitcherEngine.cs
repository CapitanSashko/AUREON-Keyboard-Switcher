using System.Diagnostics;
using System.Text;
using AUREON_Keyboard_Switcher.Keyboard;
using AUREON_Keyboard_Switcher.Languages;
using AUREON_Keyboard_Switcher.Settings;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>
    /// Робоча частина програми: перехоплення клавіатури та миші, буфер слова,
    /// автовиправлення розкладки й регістру, гарячі клавіші. Інтерфейсу не містить.
    /// </summary>
    public sealed class SwitcherEngine : IDisposable
    {
        // Скільки попередніх слів фрази можна виправити разом із поточним
        private const int MaxPreviousWords = 2;

        // Скільки мілісекунд після автовиправлення гаряча клавіша вважається «скасуванням»
        private const long UndoWindowMs = 20000;

        /// <summary>Останнє автовиправлення: потрібне, щоб гаряча клавіша могла його скасувати.</summary>
        private sealed record AutoCorrection(
            List<string> EnglishWords,           // слова блоку (фізичні клавіші), останнє = поточне слово
            KeyboardLanguage OriginalLayout,     // розкладка, в якій слова було набрано
            KeyboardLanguage TargetLayout,       // розкладка, в яку виправили
            string LearnWord,                    // слово для самонавчання (порожнє, якщо виправлялося більше одного)
            long Tick);

        // Контекст потоку інтерфейсу: через нього відкладаємо дії
        // до моменту, коли колбек хука вже завершився
        private readonly SynchronizationContext _ui;

        private readonly WordBuffer _wordBuffer = new();
        private readonly WordDictionary _dictionary = new();
        private readonly LayoutAdvisor _advisor;
        private readonly SelectionConverter _selectionConverter;
        private readonly CaseTracker _caseTracker = new();                     // НОВЕ

        private KeyboardHook? _keyboardHook;
        private MouseHook? _mouseHook;
        private HookWatchdog? _watchdog;

        private long _lastHotkeyTick;   // захист від автоповтору гарячих клавіш
        private nint _lastWindow;       // вікно, в якому набиралося поточне слово
        private int _correctionCount;   // скільки виправлень за сеанс (для журналу)

        private Hotkey _hotkeyLastWord = Hotkey.DefaultLastWord;
        private Hotkey _hotkeySelection = Hotkey.DefaultSelection;
        private Hotkey _hotkeyTransliterate = Hotkey.DefaultTransliterate;     // НОВЕ

        private AutoCorrection? _lastAutoCorrection;

        /// <summary>Налаштування програми (зберігаються в %AppData%).</summary>
        public AppSettings Config { get; private set; } = AppSettings.Load();

        /// <summary>Змінився стан або налаштування (для оновлення значка й вікна).</summary>
        public event Action? StateChanged;

        /// <summary>Коротке повідомлення для користувача (спливаюча підказка в треї).</summary>
        public event Action<string>? Notify;

        /// <summary>Перехоплення клавіатури встановлено (за даними програми).</summary>
        public bool KeyboardHookActive => _keyboardHook?.IsInstalled == true;

        /// <summary>Скільки разів сторож відновлював хуки після мовчазного зняття Windows.</summary>
        public int HookRecoveries => _watchdog?.RestartCount ?? 0;

        public SwitcherEngine(SynchronizationContext ui)
        {
            _ui = ui;
            _advisor = new LayoutAdvisor(_dictionary);
            _selectionConverter = new SelectionConverter(_dictionary);

            ApplySettingsToComponents();

            // Дописуємо у файл налаштувань нові поля
            Config.Save();
        }

        // ==================== КЕРУВАННЯ ====================

        /// <summary>Чи ввімкнено автовиправлення. Зміна одразу зберігається.</summary>
        public bool AutoCorrectEnabled
        {
            get => Config.AutoCorrectEnabled;
            set
            {
                if (Config.AutoCorrectEnabled == value)
                    return;

                Config.AutoCorrectEnabled = value;
                Config.Save();

                StateChanged?.Invoke();
            }
        }

        /// <summary>Викликати після зміни Config (винятки, мова, словник, клавіші): збереже й застосує.</summary>
        public void ApplySettings()
        {
            Config.Save();
            ApplySettingsToComponents();

            StateChanged?.Invoke();
        }

        private void ApplySettingsToComponents()
        {
            AppLog.Enabled = Config.LoggingEnabled;

            KeyboardLanguage preferred =
                Enum.TryParse(Config.PreferredLanguage, true, out KeyboardLanguage parsed) &&
                parsed != KeyboardLanguage.English
                    ? parsed
                    : KeyboardLanguage.Ukrainian;

            _advisor.Preferred = preferred;
            _selectionConverter.Preferred = preferred;

            _advisor.FixTwoLetterWords = Config.FixTwoLetterWords;

            _advisor.UserWords = new HashSet<string>(
                Config.UserWords.Select(w => w.Trim()).Where(w => w.Length > 0),
                StringComparer.OrdinalIgnoreCase);

            // Сполучення клавіш із налаштувань (за помилки у файлі або збігу: стандартні)
            Hotkey lastWord = Hotkey.ParseOr(Config.HotkeyLastWord, Hotkey.DefaultLastWord);
            Hotkey selection = Hotkey.ParseOr(Config.HotkeySelection, Hotkey.DefaultSelection);
            Hotkey transliterate = Hotkey.ParseOr(Config.HotkeyTransliterate, Hotkey.DefaultTransliterate);

            if (lastWord == selection || lastWord == transliterate || selection == transliterate)
            {
                AppLog.Warn("Однакові сполучення клавіш для різних дій: повернуто стандартні");

                lastWord = Hotkey.DefaultLastWord;
                selection = Hotkey.DefaultSelection;
                transliterate = Hotkey.DefaultTransliterate;
            }

            _hotkeyLastWord = lastWord;
            _hotkeySelection = selection;
            _hotkeyTransliterate = transliterate;
        }

        /// <summary>
        /// Завантажує словники та запускає перехоплення.
        /// Повертає список проблем для показу користувачу (порожній список = усе гаразд).
        /// </summary>
        public List<string> Start()
        {
            List<string> problems = new();

            List<string> missing = _dictionary.Load();

            if (missing.Count > 0)
            {
                problems.Add(
                    "Не знайдено файли словників:\n\n" +
                    string.Join("\n", missing) +
                    "\n\nПеревірте, що файли додані до проєкту, а в їхніх властивостях " +
                    "стоїть «Копіювати у вихідний каталог: Копіювати, якщо новіше».");

                AppLog.Error("Не знайдено файли словників");
            }

            _keyboardHook = new KeyboardHook();
            _keyboardHook.KeyPressed += OnKeyPressed;
            _wordBuffer.WordCompleted += OnWordCompleted;

            if (!_keyboardHook.Install())
            {
                problems.Add("Не вдалося запустити перехоплення клавіатури.");

                _keyboardHook.Dispose();
                _keyboardHook = null;
            }

            // Хук миші не критичний: без нього лише не скидається буфер після кліків
            _mouseHook = new MouseHook();
            _mouseHook.ButtonPressed += OnMouseClicked;

            if (!_mouseHook.Install())
            {
                Debug.WriteLine("AUREON | не вдалося встановити хук миші (не критично)");

                _mouseHook.Dispose();
                _mouseHook = null;
            }

            // Сторож стежить, щоб Windows не зняла хуки мовчки
            _watchdog = new HookWatchdog(
                () => _keyboardHook,
                () => _mouseHook,
                () =>
                {
                    _wordBuffer.Clear();
                    _lastAutoCorrection = null;
                });

            _watchdog.HooksRestored += OnHooksRestored;
            _watchdog.Start(_ui);

            AppLog.Info(
                $"Запуск: хук клавіатури {(_keyboardHook != null ? "встановлено" : "НЕ встановлено")}, " +
                $"хук миші {(_mouseHook != null ? "встановлено" : "НЕ встановлено")}");

            return problems;
        }

        private void OnHooksRestored(HookRestoreReason reason, bool ok)
        {
            string why = reason switch
            {
                HookRestoreReason.InputWithoutEvents => "Windows зняла хук (введення є, подій немає)",
                HookRestoreReason.ResumeFromSleep => "вихід зі сну",
                _ => "розблокування або вхід у сеанс"
            };

            if (!ok)
                AppLog.Error($"Не вдалося перевстановити хуки: {why}");
            else if (reason == HookRestoreReason.InputWithoutEvents)
                AppLog.Warn($"Хуки перевстановлено: {why}");
            else
                AppLog.Info($"Хуки перевстановлено: {why}");

            StateChanged?.Invoke();
        }

        public void Dispose()
        {
            if (_watchdog != null)
            {
                _watchdog.HooksRestored -= OnHooksRestored;
                _watchdog.Dispose();
                _watchdog = null;
            }

            _wordBuffer.WordCompleted -= OnWordCompleted;

            if (_keyboardHook != null)
                _keyboardHook.KeyPressed -= OnKeyPressed;

            if (_mouseHook != null)
                _mouseHook.ButtonPressed -= OnMouseClicked;

            _keyboardHook?.Dispose();
            _keyboardHook = null;

            _mouseHook?.Dispose();
            _mouseHook = null;
        }

        // ==================== ПЕРЕХОПЛЕННЯ ====================

        // Клік міг перемістити курсор введення, набране слово більше не актуальне
        private void OnMouseClicked()
        {
            if (_wordBuffer.Length > 0 || _wordBuffer.LastWord != null)
            {
                _wordBuffer.Clear();
                _lastAutoCorrection = null;
                Debug.WriteLine("AUREON | буфер скинуто: клік миші");
            }
        }

        // Літерна клавіша в поточній розкладці (на ній набирається буква, а не цифра чи знак)
        private static bool IsLetterKey(KeyEvent e, KeyboardLanguage layout)
        {
            if (!KeyboardLayoutConverter.TryGetKeyIndex(e.ScanCode, out int index))
                return false;

            return char.IsLetter(KeyboardLayoutConverter.GetLayoutChar(layout, index, shift: false));
        }

        // Викликається ВСЕРЕДИНІ колбека хука, тому тут лише швидкі операції.
        private void OnKeyPressed(KeyEvent e)
        {
            // Введення у вікнах самої програми (налаштування, діалоги, запис сполучення клавіш)
            // не обробляємо: інакше виправлявся б текст у її власних полях
            if (ForegroundApp.IsOwnWindow())
                return;

            // Змінилося активне вікно: слово з попереднього вікна більше не актуальне
            nint window = ForegroundApp.GetWindowHandle();

            if (window != 0 && window != _lastWindow)
            {
                _lastWindow = window;

                if (_wordBuffer.Length > 0 || _wordBuffer.LastWord != null)
                {
                    _wordBuffer.Clear();
                    _lastAutoCorrection = null;
                    Debug.WriteLine("AUREON | буфер скинуто: змінилося активне вікно");
                }
            }

            // Гарячі клавіші з налаштувань
            bool isSelection = _hotkeySelection.Matches(e);
            bool isTransliterate = !isSelection && _hotkeyTransliterate.Matches(e);
            bool isLastWord = !isSelection && !isTransliterate && _hotkeyLastWord.Matches(e);

            if (isSelection || isTransliterate || isLastWord)
            {
                // Захист від автоповтору, коли клавішу тримають
                long now = Environment.TickCount64;

                if (now - _lastHotkeyTick < 400)
                    return;

                _lastHotkeyTick = now;

                int triggerKey = (int)e.Key;

                if (isLastWord)
                {
                    _ui.Post(async _ => await ConvertLastWordAsync(triggerKey), null);
                    return;
                }

                // Дії над виділеним ідуть через Ctrl+C / Ctrl+V.
                // У терміналах Ctrl+C перериває програму, тому у винятках забороняємо.
                string? app = ForegroundApp.GetProcessName();

                if (app == null || Config.IsExcluded(app))
                {
                    Debug.WriteLine(
                        $"AUREON | дії над виділеним вимкнено для програми {app ?? "(не визначена)"}");
                    return;
                }

                if (isSelection)
                    _ui.Post(async _ => await _selectionConverter.ConvertAsync(triggerKey), null);
                else
                    _ui.Post(async _ => await _selectionConverter.TransliterateAsync(triggerKey), null);

                return;
            }

            // Розкладка, в якій натиснуто цю клавішу
            KeyboardLanguage? layout =
                KeyboardLayoutDetector.TryGetCurrentLayout(out KeyboardLanguage detected)
                    ? detected
                    : null;

            // НОВЕ: стежимо, як натискалися літери слова (випадковий CapsLock, подвійна велика)
            if (_wordBuffer.Length == 0)
                _caseTracker.Reset();

            if (layout != null && !e.Ctrl && !e.Alt && IsLetterKey(e, layout.Value))
                _caseTracker.AddLetterKey(e.Shift, e.CapsLock);

            _wordBuffer.Process(e, layout);
        }

        // ==================== РУЧНЕ ВИПРАВЛЕННЯ ОСТАННЬОГО СЛОВА ====================

        // Спочатку чекаємо, поки відпустять клавіші сполучення. Інакше, наприклад, при Ctrl+Alt+Z
        // наші Backspace перетворилися б на Ctrl+Alt+Backspace.
        private async Task ConvertLastWordAsync(int triggerKey)
        {
            try
            {
                Stopwatch timer = Stopwatch.StartNew();

                while (!KeyboardInjector.AreHotkeyKeysReleased(triggerKey))
                {
                    if (timer.ElapsedMilliseconds > 1000)
                    {
                        Debug.WriteLine("AUREON | виправлення слова: клавіші не відпущено, скасовано");
                        return;
                    }

                    await Task.Delay(20);
                }

                ConvertLastWord();
            }
            catch (Exception ex)
            {
                AppLog.Error("Помилка ручного виправлення слова", ex);
            }
        }

        private void ConvertLastWord()
        {
            // Гаряча клавіша одразу після автовиправлення: повертаємо все як було (і вчимо словник)
            if (TryUndoAutoCorrection())
                return;

            string englishWord;
            bool withSpace;

            if (_wordBuffer.Length > 0)
            {
                // Слово ще набирається: пробілу після нього немає
                englishWord = _wordBuffer.CurrentWord;
                withSpace = false;
            }
            else if (_wordBuffer.LastWord != null && _wordBuffer.LastDelimiter == Keys.Space)
            {
                // Слово щойно завершено пробілом
                englishWord = _wordBuffer.LastWord;
                withSpace = true;
            }
            else
            {
                Debug.WriteLine("AUREON | ручне виправлення: немає слова для виправлення");
                return;
            }

            if (!KeyboardLayoutDetector.TryGetCurrentLayout(out KeyboardLanguage layout))
                return;

            if (KeyboardInjector.IsCtrlOrAltHeld())
                return;

            List<KeyboardLanguage> installed = InstalledLayouts.GetInstalled();

            // Спочатку питаємо радника: він враховує словники та розділові знаки
            LayoutAdvice advice = _advisor.Evaluate(englishWord, layout, installed);

            KeyboardLanguage target;
            string corrected;

            if (advice.ShouldSwitch)
            {
                target = advice.Target;
                corrected = advice.CorrectedText;
            }
            else
            {
                // Радник вважає, що все гаразд (або слово йому невідоме).
                // Раз ви натиснули гарячу клавішу, перемикаємо примусово: англійська <-> бажана.
                target = FallbackTarget(layout, installed);

                if (target == layout)
                    return;

                corrected = KeyboardLayoutConverter.Convert(
                    englishWord, KeyboardLanguage.English, target);
            }

            int backspaces = englishWord.Length + (withSpace ? 1 : 0);
            string replacement = corrected + (withSpace ? " " : "");

            if (!KeyboardInjector.ReplaceText(backspaces, replacement))
            {
                Debug.WriteLine("AUREON | ручне виправлення: не вдалося надіслати введення (можливо, вікно запущено від адміністратора)");
                AppLog.Warn("Ручне виправлення: не вдалося надіслати введення (можливо, вікно запущено від імені адміністратора)");
                return;
            }

            LayoutSwitcher.SwitchTo(target);

            AppLog.Info($"Ручне виправлення слова, {layout} -> {target}");
            Debug.WriteLine($"AUREON | ручне виправлення: {layout} -> {target}");
        }

        // Якщо з англійської: у бажану. З будь-якої іншої: в англійську.
        private KeyboardLanguage FallbackTarget(
            KeyboardLanguage current,
            IReadOnlyList<KeyboardLanguage> installed)
        {
            if (current != KeyboardLanguage.English &&
                installed.Contains(KeyboardLanguage.English))
            {
                return KeyboardLanguage.English;
            }

            if (_advisor.Preferred != current && installed.Contains(_advisor.Preferred))
                return _advisor.Preferred;

            return installed.FirstOrDefault(l => l != current, current);
        }

        // ==================== СКАСУВАННЯ ВИПРАВЛЕННЯ І САМОНАВЧАННЯ ====================

        /// <summary>
        /// Якщо гарячу клавішу натиснуто одразу після автовиправлення: повертає весь блок слів у вихідний вигляд.
        /// Коли виправлялося одне слово, запам'ятовує його у словнику користувача.
        /// </summary>
        private bool TryUndoAutoCorrection()
        {
            AutoCorrection? last = _lastAutoCorrection;

            if (last == null)
                return false;

            bool fresh = Environment.TickCount64 - last.Tick < UndoWindowMs;

            bool stillThere =
                _wordBuffer.Length == 0 &&
                _wordBuffer.LastWord == last.EnglishWords[^1] &&
                _wordBuffer.LastDelimiter == Keys.Space;

            if (!fresh || !stillThere)
            {
                _lastAutoCorrection = null;
                return false;
            }

            // Скасування спрацьовує один раз; повторне натискання працює як звичайне ручне виправлення
            _lastAutoCorrection = null;

            if (KeyboardInjector.IsCtrlOrAltHeld())
                return true;

            // Вихідний вигляд блоку: кожне слово в розкладці, в якій його було набрано
            StringBuilder original = new();
            int backspaces = 0;

            foreach (string word in last.EnglishWords)
            {
                original.Append(KeyboardLayoutConverter.Convert(
                    word, KeyboardLanguage.English, last.OriginalLayout)).Append(' ');

                backspaces += word.Length + 1;
            }

            if (!KeyboardInjector.ReplaceText(backspaces, original.ToString()))
            {
                Debug.WriteLine("AUREON | скасування: не вдалося надіслати введення");
                return true;
            }

            LayoutSwitcher.SwitchTo(last.OriginalLayout);

            AppLog.Info($"Автовиправлення скасовано (слів: {last.EnglishWords.Count})");
            Debug.WriteLine($"AUREON | автовиправлення скасовано ({last.EnglishWords.Count} сл.)");

            LearnWord(last.LearnWord);

            return true;
        }

        private void LearnWord(string word)
        {
            if (!Config.SelfLearningEnabled)
                return;

            word = word.Trim().ToLowerInvariant();

            // Надто короткі слова й слова з цифрами не запам'ятовуємо
            if (word.Length < 2 || word.Any(char.IsDigit))
                return;

            bool exists = Config.UserWords.Any(w =>
                string.Equals(w.Trim(), word, StringComparison.OrdinalIgnoreCase));

            if (exists)
                return;

            Config.UserWords.Add(word);
            ApplySettings();

            // Саме слово в журнал не пишемо
            AppLog.Info("Самонавчання: слово додано до словника користувача");

            Notify?.Invoke($"Слово «{word}» додано до словника: його більше не виправлятимуть");

            Debug.WriteLine($"AUREON | самонавчання: слово додано до словника користувача ({word.Length} симв.)");
        }

        // ==================== АВТОВИПРАВЛЕННЯ ====================

        // Викликається ВСЕРЕДИНІ колбека хука, тому тут лише швидкі операції.
        private void OnWordCompleted(string englishWord, Keys delimiter)
        {
            if (!KeyboardLayoutDetector.TryGetCurrentLayout(out KeyboardLanguage layout))
                return;

            // Перевірка програми: у винятках, а також якщо визначити її не вдалося
            // (наприклад, вікно запущено від адміністратора), нічого не робимо
            string? app = ForegroundApp.GetProcessName();

            if (app == null)
            {
                Debug.WriteLine("AUREON | пропуск: не вдалося визначити програму");
                return;
            }

            if (Config.IsExcluded(app))
            {
                Debug.WriteLine($"AUREON | пропуск: програма {app} у списку винятків");
                return;
            }

            List<KeyboardLanguage> installed = InstalledLayouts.GetInstalled();

            LayoutAdvice advice = _advisor.Evaluate(englishWord, layout, installed);

            bool endedWithSpace = delimiter == Keys.Space;

            // Розкладку виправляємо, лише якщо автовиправлення ввімкнено й слово закінчилося пробілом
            bool switching = advice.ShouldSwitch && Config.AutoCorrectEnabled && endedWithSpace;

            // НОВЕ: слово, яке з'явиться на екрані після виправлення розкладки (або те, що набрано зараз)
            KeyboardLanguage finalLanguage = switching ? advice.Target : layout;

            string finalWord = switching
                ? advice.CorrectedText
                : KeyboardLayoutConverter.Convert(englishWord, KeyboardLanguage.English, layout);

            // НОВЕ: помилки регістру (випадковий CapsLock, подвійна велика літера)
            CaseMistake mistake = Config.FixCapsLock && endedWithSpace
                ? _caseTracker.Detect(Config.FixDoubleCapital)
                : CaseMistake.None;

            bool caseFixed = false;

            if (mistake != CaseMistake.None &&
                CapsFixer.TryFix(finalWord, mistake, finalLanguage, _dictionary, out string capsText))
            {
                // Тут capsText гарантовано заповнено (TryFix повернув true)
                finalWord = capsText;
                caseFixed = true;
            }

            if (!switching && !caseFixed)
            {
                Debug.WriteLine(
                    advice.ShouldSwitch
                        ? $"AUREON | [{app}] ({layout}) -> потрібно {advice.Target}, але автовиправлення вимкнено або слово не закінчилося пробілом"
                        : $"AUREON | [{app}] ({layout}) -> залишити | {advice.Reason}");
                return;
            }

            // Попередні слова фрази, які теж треба виправити разом із цим (якщо це не вимкнено)
            List<(string English, string Corrected)> previous = switching && Config.FixPreviousWords
                ? FindPreviousWordsToFix(layout, advice.Target)
                : new();

            // Стираємо всі слова блоку та пробіли, друкуємо виправлені слова та пробіли
            int backspaces = englishWord.Length + 1;
            StringBuilder replacement = new();
            List<string> block = new();

            foreach (var (english, corrected) in previous)
            {
                backspaces += english.Length + 1;
                replacement.Append(corrected).Append(' ');
                block.Add(english);
            }

            replacement.Append(finalWord).Append(' ');
            block.Add(englishWord);

            // Самонавчання: запам'ятовуємо вихідний вигляд слова, лише якщо виправлялося рівно одне слово
            string learnWord = switching && previous.Count == 0
                ? advice.OriginalCore.Trim().ToLowerInvariant()
                : string.Empty;

            string kind = switching
                ? (caseFixed ? "розкладку й регістр" : "розкладку")
                : "регістр";

            string text = replacement.ToString();
            KeyboardLanguage target = finalLanguage;
            KeyboardLanguage original = layout;
            bool turnCapsOff = caseFixed && mistake == CaseMistake.CapsLockAccident;

            // Відкладаємо до виходу з колбека: на цей момент пробіл уже дійшов до програми
            _ui.Post(_ => ApplyCorrection(
                backspaces, text, target, switching, block, original, learnWord, turnCapsOff, kind), null);
        }

        /// <summary>
        /// Шукає слова перед поточним, які теж набрано не в тій розкладці
        /// (наприклад, однолітерні «я», «в» або короткі слова перед довгим).
        /// Іде назад від найближчого слова й зупиняється на першому, яке чіпати не можна.
        /// </summary>
        private List<(string English, string Corrected)> FindPreviousWordsToFix(
            KeyboardLanguage current,
            KeyboardLanguage target)
        {
            List<(string, string)> result = new();
            IReadOnlyList<PhraseWord> history = _wordBuffer.History;

            for (int i = history.Count - 1; i >= 0 && result.Count < MaxPreviousWords; i--)
            {
                PhraseWord entry = history[i];

                // Слово набрано в іншій розкладці: отже, фраза вже змінювалася
                if (entry.Layout != current)
                    break;

                if (!_advisor.TryConvertCompanion(entry.Word, current, target, out string corrected))
                    break;

                result.Insert(0, (entry.Word, corrected));
            }

            return result;
        }

        private void ApplyCorrection(
            int backspaces,
            string replacement,
            KeyboardLanguage target,
            bool switchLayout,
            List<string> block,
            KeyboardLanguage originalLayout,
            string learnWord,
            bool turnCapsLockOff,
            string kind)
        {
            // Користувач уже почав друкувати наступне слово: не ризикуємо
            if (_wordBuffer.Length > 0)
            {
                Debug.WriteLine("AUREON | виправлення пропущено: уже набрано наступне слово");
                return;
            }

            // Затиснуто Ctrl/Alt: Backspace перетворився б на Ctrl+Backspace
            if (KeyboardInjector.IsCtrlOrAltHeld())
            {
                Debug.WriteLine("AUREON | виправлення пропущено: затиснуто Ctrl/Alt");
                return;
            }

            if (!KeyboardInjector.ReplaceText(backspaces, replacement))
            {
                Debug.WriteLine("AUREON | не вдалося надіслати введення (можливо, вікно запущено від адміністратора)");
                AppLog.Warn("Автовиправлення: не вдалося надіслати введення (можливо, вікно запущено від імені адміністратора)");
                return;
            }

            if (switchLayout)
                LayoutSwitcher.SwitchTo(target);

            // НОВЕ: після випадкового CapsLock вимикаємо його, щоб наступні слова не псувалися
            if (turnCapsLockOff && KeyboardInjector.IsCapsLockOn())
                KeyboardInjector.TapCapsLock();

            _lastAutoCorrection = new AutoCorrection(
                block, originalLayout, target, learnWord, Environment.TickCount64);

            _correctionCount++;

            // Лише факти: що виправлено й скільки слів, самі слова не пишемо
            AppLog.Info(
                $"Автовиправлення ({kind}): слів {block.Count}, {originalLayout} -> {target} " +
                $"(за сеанс: {_correctionCount})");

            Debug.WriteLine($"AUREON | виправлено ({kind}), слів: {block.Count}");
        }
    }
}