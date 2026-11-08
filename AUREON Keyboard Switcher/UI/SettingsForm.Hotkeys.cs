using AUREON_Keyboard_Switcher.Core;

namespace AUREON_Keyboard_Switcher.UI
{
    // Частина вікна налаштувань: сторінки «Клавіші» та «Виправлення»
    public sealed partial class SettingsForm
    {
        private readonly List<HotkeyBox> _hotkeyBoxes = new();
        private Label _hotkeyMessage = null!;

        private ToggleSwitch _toggleShortWords = null!;
        private ToggleSwitch _togglePreviousWords = null!;
        private ToggleSwitch _toggleCapsLock = null!;
        private ToggleSwitch _toggleDoubleCapital = null!;

        // ==================== СТОРІНКА «КЛАВІШІ» ====================

        private void BuildHotkeysPage()
        {
            FlowLayoutPanel cards = CreatePage("hotkeys", "Клавіші");

            cards.Controls.Add(new Label
            {
                Text = "Натисніть на поле й введіть сполучення. Esc скасовує запис. " +
                       "Для літер і цифр потрібні щонайменше два модифікатори (Ctrl, Alt, Shift), " +
                       "а клавіші F1–F24, Pause і Scroll Lock можна й без них.",
                Font = Theme.CreateFont(9.5F),
                ForeColor = Theme.Muted,
                AutoSize = false,
                Height = 54,
                Margin = new Padding(0, 0, 0, 8)
            });

            cards.Controls.Add(CreateHotkeyCard(
                "Виправити останнє слово",
                "Повторне натискання повертає слово назад",
                Hotkey.ParseOr(_engine.Config.HotkeyLastWord, Hotkey.DefaultLastWord),
                Hotkey.DefaultLastWord,
                hotkey => _engine.Config.HotkeyLastWord = hotkey.ToSettingString()));

            cards.Controls.Add(CreateHotkeyCard(
                "Перетворити виділений текст",
                "В іншу розкладку. Не працює в програмах зі списку винятків",
                Hotkey.ParseOr(_engine.Config.HotkeySelection, Hotkey.DefaultSelection),
                Hotkey.DefaultSelection,
                hotkey => _engine.Config.HotkeySelection = hotkey.ToSettingString()));

            cards.Controls.Add(CreateHotkeyCard(
                "Транслітерація виділеного тексту",
                "«Харків» → «Kharkiv». Не працює в програмах зі списку винятків",
                Hotkey.ParseOr(_engine.Config.HotkeyTransliterate, Hotkey.DefaultTransliterate),
                Hotkey.DefaultTransliterate,
                hotkey => _engine.Config.HotkeyTransliterate = hotkey.ToSettingString()));

            _hotkeyMessage = new Label
            {
                Font = Theme.CreateFont(9.5F),
                ForeColor = Color.FromArgb(255, 170, 90),
                AutoSize = false,
                Height = 40,
                Margin = new Padding(0)
            };

            cards.Controls.Add(_hotkeyMessage);
        }

        private CardPanel CreateHotkeyCard(
            string title,
            string description,
            Hotkey current,
            Hotkey defaultValue,
            Action<Hotkey> save)
        {
            CardPanel card = CreateCard(92);
            AddCardTexts(card, title, description);

            var hotkeyBox = new HotkeyBox { Value = current };
            _hotkeyBoxes.Add(hotkeyBox);

            Hotkey saved = current;

            // Зберігає нове сполучення, якщо воно не збігається зі сполученням іншої дії
            bool Apply(Hotkey candidate)
            {
                bool taken = _hotkeyBoxes.Any(b => b != hotkeyBox && b.Value == candidate);

                if (taken)
                {
                    _hotkeyMessage.Text = "Це сполучення вже використовується для іншої дії";
                    return false;
                }

                saved = candidate;
                save(candidate);
                _engine.ApplySettings();

                _hotkeyMessage.Text = string.Empty;
                return true;
            }

            hotkeyBox.ValueChanged += (_, _) =>
            {
                if (!Apply(hotkeyBox.Value))
                    hotkeyBox.Value = saved;   // конфлікт: повертаємо попереднє значення
            };

            hotkeyBox.Rejected += reason => _hotkeyMessage.Text = reason;

            var reset = new Button { Text = "Скинути", Size = new Size(90, 32) };
            Theme.StyleButton(reset);
            reset.Click += (_, _) =>
            {
                if (Apply(defaultValue))
                    hotkeyBox.Value = defaultValue;
            };

            PlaceRight(card, reset, 20, 30);
            PlaceRight(card, hotkeyBox, 118, 30);

            return card;
        }

        // ==================== СТОРІНКА «ВИПРАВЛЕННЯ» ====================

        private void BuildCorrectionPage()
        {
            FlowLayoutPanel cards = CreatePage("correction", "Виправлення");

            cards.Controls.Add(CreateToggleCard(
                "Виправляти двобуквенні слова",
                "Наприклад «Як», «на», «до». Вимкніть, якщо скорочення (db, ps) змінюються хибно",
                _engine.Config.FixTwoLetterWords,
                value => _engine.Config.FixTwoLetterWords = value,
                out _toggleShortWords));

            cards.Controls.Add(CreateToggleCard(
                "Виправляти попередні слова фрази",
                "«z cghfdb» → «я справи»: разом із поточним виправляються й слова перед ним",
                _engine.Config.FixPreviousWords,
                value => _engine.Config.FixPreviousWords = value,
                out _togglePreviousWords));

            cards.Controls.Add(CreateToggleCard(
                "Виправляти випадковий CapsLock",
                "«пРИВІТ» → «Привіт»; CapsLock при цьому вимикається",
                _engine.Config.FixCapsLock,
                value => _engine.Config.FixCapsLock = value,
                out _toggleCapsLock));

            cards.Controls.Add(CreateToggleCard(
                "Виправляти подвійну велику літеру",
                "«ПРивіт» → «Привіт»; лише для слів кирилицею, що є у словнику",
                _engine.Config.FixDoubleCapital,
                value => _engine.Config.FixDoubleCapital = value,
                out _toggleDoubleCapital));
        }

        private CardPanel CreateToggleCard(
            string title,
            string description,
            bool value,
            Action<bool> save,
            out ToggleSwitch toggle)
        {
            CardPanel card = CreateCard(84);
            AddCardTexts(card, title, description);

            var toggleSwitch = new ToggleSwitch { Checked = value };
            toggleSwitch.CheckedChanged += (_, _) =>
            {
                if (_syncing)
                    return;

                save(toggleSwitch.Checked);
                _engine.ApplySettings();
            };

            PlaceRight(card, toggleSwitch, 20, 30);

            toggle = toggleSwitch;
            return card;
        }
    }
}