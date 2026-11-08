namespace AUREON_Keyboard_Switcher.UI
{
    // Друга частина вікна налаштувань: сторінка «Словник»
    public sealed partial class SettingsForm
    {
        private ListBox _listWords = null!;
        private TextBox _txtWord = null!;
        private ToggleSwitch _toggleLearning = null!;

        private void BuildWordsPage()
        {
            FlowLayoutPanel cards = CreatePage("words", "Словник");

            cards.Controls.Add(new Label
            {
                Text = "Слова, які програма не виправляє ніколи. " +
                       "Пишіть слово так, як воно виглядає на екрані, наприклад: db або Харків.",
                Font = Theme.CreateFont(9.5F),
                ForeColor = Theme.Muted,
                AutoSize = false,
                Height = 46,
                Margin = new Padding(0, 0, 0, 8)
            });

            // --- Самонавчання ---
            CardPanel cardLearn = CreateCard(90);
            AddCardTexts(cardLearn,
                "Самонавчання",
                "Якщо ви скасували автовиправлення клавішею Pause, слово додається до словника");

            _toggleLearning = new ToggleSwitch { Checked = _engine.Config.SelfLearningEnabled };
            _toggleLearning.CheckedChanged += (_, _) =>
            {
                if (_syncing)
                    return;

                _engine.Config.SelfLearningEnabled = _toggleLearning.Checked;
                _engine.ApplySettings();
            };

            PlaceRight(cardLearn, _toggleLearning, 20, 33);
            cards.Controls.Add(cardLearn);

            // --- Список слів ---
            var cardList = new CardPanel
            {
                Height = 176,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 10)
            };

            _listWords = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Card,
                ForeColor = Theme.Text,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 32,
                SelectionMode = SelectionMode.MultiExtended,
                IntegralHeight = false
            };
            _listWords.DrawItem += WordsList_DrawItem;
            _listWords.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    RemoveSelectedWords();
                    e.Handled = true;
                }
            };

            cardList.Controls.Add(_listWords);
            cards.Controls.Add(cardList);

            // --- Рядок введення та кнопки ---
            var row = new TableLayoutPanel
            {
                Height = 44,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = Theme.Back,
                Margin = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106F));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _txtWord = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Field,
                ForeColor = Theme.Text,
                Font = Theme.CreateFont(10.5F),
                PlaceholderText = "Слово, наприклад: db",
                Margin = new Padding(0, 0, 8, 0)
            };
            _txtWord.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddTypedWords();
                    e.SuppressKeyPress = true;
                }
            };

            var buttonAdd = new Button { Text = "Додати", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 6, 3) };
            Theme.StyleButton(buttonAdd, primary: true);
            buttonAdd.Click += (_, _) => AddTypedWords();

            var buttonRemove = new Button { Text = "Видалити", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
            Theme.StyleButton(buttonRemove);
            buttonRemove.Click += (_, _) => RemoveSelectedWords();

            row.Controls.Add(_txtWord, 0, 0);
            row.Controls.Add(buttonAdd, 1, 0);
            row.Controls.Add(buttonRemove, 2, 0);

            cards.Controls.Add(row);

            RefreshWordsList();
        }

        private void WordsList_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;

            bool selected = (e.State & DrawItemState.Selected) != 0;

            using (var back = new SolidBrush(selected ? Color.FromArgb(46, 66, 106) : Theme.Card))
            {
                e.Graphics.FillRectangle(back, e.Bounds);
            }

            var textBounds = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height);

            TextRenderer.DrawText(
                e.Graphics,
                _listWords.Items[e.Index]?.ToString() ?? string.Empty,
                Theme.CreateFont(10.5F),
                textBounds,
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // Викликається і з обробника зміни стану движка (наприклад, коли слово додало самонавчання)
        private void RefreshWordsList(string? select = null)
        {
            if (_listWords is null)
                return;

            _listWords.BeginUpdate();
            _listWords.Items.Clear();

            foreach (string word in _engine.Config.UserWords
                         .Select(w => w.Trim())
                         .Where(w => w.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(w => w, StringComparer.OrdinalIgnoreCase))
            {
                int index = _listWords.Items.Add(word);

                if (select != null && string.Equals(word, select, StringComparison.OrdinalIgnoreCase))
                    _listWords.SelectedIndex = index;
            }

            _listWords.EndUpdate();
        }

        private void AddTypedWords()
        {
            string[] words = _txtWord.Text.Split(
                new[] { ' ', ',', ';', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

            bool changed = false;
            string? last = null;

            foreach (string raw in words)
            {
                string word = raw.Trim().ToLowerInvariant();

                if (word.Length == 0)
                    continue;

                last = word;

                bool exists = _engine.Config.UserWords.Any(w =>
                    string.Equals(w.Trim(), word, StringComparison.OrdinalIgnoreCase));

                if (exists)
                    continue;

                _engine.Config.UserWords.Add(word);
                changed = true;
            }

            if (changed)
                _engine.ApplySettings();

            RefreshWordsList(last);

            _txtWord.Clear();
            _txtWord.Focus();
        }

        private void RemoveSelectedWords()
        {
            var selected = _listWords.SelectedItems
                .Cast<object>()
                .Select(o => o.ToString() ?? string.Empty)
                .ToList();

            if (selected.Count == 0)
                return;

            _engine.Config.UserWords.RemoveAll(w =>
                selected.Any(s => string.Equals(w.Trim(), s, StringComparison.OrdinalIgnoreCase)));

            _engine.ApplySettings();
            RefreshWordsList();
        }
    }
}