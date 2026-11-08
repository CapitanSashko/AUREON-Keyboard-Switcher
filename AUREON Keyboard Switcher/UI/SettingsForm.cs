using System.Diagnostics;
using System.Reflection;
using AUREON_Keyboard_Switcher.Core;
using AUREON_Keyboard_Switcher.Settings;

namespace AUREON_Keyboard_Switcher.UI
{
    public sealed partial class SettingsForm : Form
    {
        private readonly SwitcherEngine _engine;

        private readonly Panel _content;
        private readonly Dictionary<string, Panel> _pages = new();
        private readonly List<Button> _navButtons = new();
        private string _currentPage = "general";

        private ToggleSwitch _toggleAuto = null!;
        private ToggleSwitch _toggleAutoStart = null!;
        private SegmentedControl _segPreferred = null!;
        private ListBox _listExcluded = null!;
        private TextBox _txtApp = null!;

        // true, поки вікно оновлюється програмно (щоб це не вважалося дією користувача)
        private bool _syncing;

        public SettingsForm(SwitcherEngine engine, Icon? icon)
        {
            _engine = engine;

            SuspendLayout();

            Text = "AUREON Keyboard Switcher";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(800, 600);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.CreateFont(10F);

            if (icon != null)
                Icon = icon;

            // ----- Бічна панель -----
            var sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 190,
                BackColor = Theme.Sidebar
            };

            var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = Theme.Sidebar };

            header.Controls.Add(new Label
            {
                Text = "AUREON",
                Font = Theme.CreateFont(18F, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Theme.Sidebar,
                AutoSize = true,
                Location = new Point(20, 22)
            });

            header.Controls.Add(new Label
            {
                Text = "Keyboard Switcher",
                Font = Theme.CreateFont(9.5F),
                ForeColor = Theme.Muted,
                BackColor = Theme.Sidebar,
                AutoSize = true,
                Location = new Point(22, 56)
            });

            var navFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Theme.Sidebar,
                Margin = Padding.Empty
            };

            AddNavButton(navFlow, "Загальні", "general");
            AddNavButton(navFlow, "Виправлення", "correction");
            AddNavButton(navFlow, "Клавіші", "hotkeys");
            AddNavButton(navFlow, "Винятки", "exclusions");
            AddNavButton(navFlow, "Словник", "words");
            AddNavButton(navFlow, "Про програму", "about");

            // Порядок важливий: спочатку Fill, потім Top
            sidebar.Controls.Add(navFlow);
            sidebar.Controls.Add(header);

            // ----- Область сторінок -----
            _content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Back };

            // Порядок важливий: спочатку Fill, потім Left
            Controls.Add(_content);
            Controls.Add(sidebar);

            BuildGeneralPage();
            BuildCorrectionPage();
            BuildHotkeysPage();
            BuildExclusionsPage();
            BuildWordsPage();
            BuildAboutPage();

            ShowPage("general");

            _engine.StateChanged += OnEngineStateChanged;
            FormClosed += (_, _) => _engine.StateChanged -= OnEngineStateChanged;

            ResumeLayout(false);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.UseDarkTitleBar(this);
        }

        // ==================== НАВІГАЦІЯ ====================

        private void AddNavButton(FlowLayoutPanel flow, string text, string pageKey)
        {
            var button = new Button
            {
                Text = "     " + text,
                Width = 190,
                Height = 46,
                Margin = Padding.Empty,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Theme.Sidebar,
                ForeColor = Theme.Muted,
                Font = Theme.CreateFont(10.5F),
                Cursor = Cursors.Hand,
                Tag = pageKey,
                UseVisualStyleBackColor = false
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 42, 47);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(50, 50, 56);

            button.Click += (_, _) => ShowPage(pageKey);

            // Акцентна смужка ліворуч в активного пункту
            button.Paint += (s, e) =>
            {
                if ((string?)((Button)s!).Tag == _currentPage)
                {
                    using var brush = new SolidBrush(Theme.Accent);
                    e.Graphics.FillRectangle(brush, 0, 10, 3, button.Height - 20);
                }
            };

            flow.Controls.Add(button);
            _navButtons.Add(button);
        }

        private void ShowPage(string key)
        {
            _currentPage = key;

            foreach (var pair in _pages)
                pair.Value.Visible = pair.Key == key;

            foreach (Button button in _navButtons)
            {
                bool active = (string?)button.Tag == key;

                button.BackColor = active ? Color.FromArgb(46, 46, 52) : Theme.Sidebar;
                button.ForeColor = active ? Theme.Text : Theme.Muted;
                button.Invalidate();
            }
        }

        // ==================== ЗАГОТОВКИ ДЛЯ СТОРІНОК ====================

        private FlowLayoutPanel CreatePage(string key, string title)
        {
            var page = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Back,
                Padding = new Padding(28, 20, 28, 16),
                Visible = false
            };

            var cards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Theme.Back
            };

            // Картки завжди на всю ширину сторінки
            cards.Resize += (_, _) => StretchCards(cards);
            cards.ControlAdded += (_, _) => StretchCards(cards);

            var heading = new Label
            {
                Dock = DockStyle.Top,
                Height = 52,
                Text = title,
                Font = Theme.CreateFont(18F, FontStyle.Bold),
                ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Порядок важливий: спочатку Fill, потім Top
            page.Controls.Add(cards);
            page.Controls.Add(heading);

            _content.Controls.Add(page);
            _pages[key] = page;

            return cards;
        }

        private static void StretchCards(FlowLayoutPanel flow)
        {
            int width = flow.ClientSize.Width;

            foreach (Control control in flow.Controls)
                control.Width = Math.Max(200, width - control.Margin.Horizontal);
        }

        private static CardPanel CreateCard(int height) =>
            new CardPanel { Height = height, Margin = new Padding(0, 0, 0, 12) };

        // Заголовок і пояснення всередині картки
        private static void AddCardTexts(CardPanel card, string title, string? description)
        {
            card.Controls.Add(new Label
            {
                Text = title,
                Font = Theme.CreateFont(11F, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                AutoSize = true,
                Location = new Point(18, 14)
            });

            if (!string.IsNullOrEmpty(description))
            {
                card.Controls.Add(new Label
                {
                    Text = description,
                    Font = Theme.CreateFont(9.5F),
                    ForeColor = Theme.Muted,
                    BackColor = Theme.Card,
                    AutoSize = false,
                    Size = new Size(340, 36),
                    Location = new Point(18, 40)
                });
            }
        }

        // Елемент біля правого краю картки. Підписи-описи ліворуч звужуються,
        // щоб ніколи не заходити під цей елемент
        private static void PlaceRight(CardPanel card, Control control, int rightMargin, int top)
        {
            card.Controls.Add(control);

            void Place()
            {
                control.Left = card.ClientSize.Width - control.Width - rightMargin;
                control.Top = top;

                foreach (Control child in card.Controls)
                {
                    if (child is Label { AutoSize: false } label)
                        label.Width = Math.Max(100, control.Left - label.Left - 16);
                }
            }

            card.Resize += (_, _) => Place();
            Place();
        }

        // ==================== СТОРІНКА «ЗАГАЛЬНІ» ====================

        private void BuildGeneralPage()
        {
            FlowLayoutPanel cards = CreatePage("general", "Загальні");

            // --- Автовиправлення ---
            CardPanel cardAuto = CreateCard(82);
            AddCardTexts(cardAuto,
                "Автовиправлення розкладки",
                "Слово виправляється саме, коли ви натискаєте пробіл");

            _toggleAuto = new ToggleSwitch { Checked = _engine.AutoCorrectEnabled };
            _toggleAuto.CheckedChanged += (_, _) =>
            {
                if (!_syncing)
                    _engine.AutoCorrectEnabled = _toggleAuto.Checked;
            };

            PlaceRight(cardAuto, _toggleAuto, 20, 29);
            cards.Controls.Add(cardAuto);

            // --- Автозапуск ---
            CardPanel cardStart = CreateCard(82);
            AddCardTexts(cardStart,
                "Запускати разом із Windows",
                "Програма стартує згорнутою в трей під час входу в систему");

            _toggleAutoStart = new ToggleSwitch { Checked = AutoStart.IsEnabled };
            _toggleAutoStart.CheckedChanged += (_, _) => OnAutoStartToggled();

            PlaceRight(cardStart, _toggleAutoStart, 20, 29);
            cards.Controls.Add(cardStart);

            // --- Бажана мова ---
            CardPanel cardLang = CreateCard(90);

            cardLang.Controls.Add(new Label
            {
                Text = "Якщо слово підходить двом мовам",
                Font = Theme.CreateFont(11F, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Theme.Card,
                AutoSize = true,
                Location = new Point(18, 14)
            });

            cardLang.Controls.Add(new Label
            {
                Text = "Яку розкладку обрати: українську чи російську",
                Font = Theme.CreateFont(9.5F),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = false,
                Size = new Size(310, 20),
                Location = new Point(18, 40)
            });

            _segPreferred = new SegmentedControl
            {
                Items = new[] { "Українська", "Російська" },
                Size = new Size(200, 30),
                SelectedIndex = _engine.Config.PreferredLanguage.Equals("Russian", StringComparison.OrdinalIgnoreCase) ? 1 : 0
            };
            _segPreferred.SelectedIndexChanged += (_, _) =>
            {
                if (_syncing)
                    return;

                _engine.Config.PreferredLanguage = _segPreferred.SelectedIndex == 1 ? "Russian" : "Ukrainian";
                _engine.ApplySettings();
            };

            PlaceRight(cardLang, _segPreferred, 20, 30);
            cards.Controls.Add(cardLang);

           
        }

        private void OnAutoStartToggled()
        {
            if (_syncing)
                return;

            try
            {
                AutoStart.Set(_toggleAutoStart.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не вдалося змінити автозапуск: " + ex.Message,
                    "AUREON Keyboard Switcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            // Показуємо реальний стан (на випадок помилки запису в реєстр)
            _syncing = true;
            _toggleAutoStart.Checked = AutoStart.IsEnabled;
            _syncing = false;
        }

        // Стан змінився десь іще (наприклад, з меню в треї)
        private void OnEngineStateChanged()
        {
            _syncing = true;

            _toggleAuto.Checked = _engine.AutoCorrectEnabled;
            RefreshWordsList();

            _syncing = false;
        }

        // ==================== СТОРІНКА «ВИНЯТКИ» ====================

        private void BuildExclusionsPage()
        {
            FlowLayoutPanel cards = CreatePage("exclusions", "Винятки");

            cards.Controls.Add(new Label
            {
                Text = "У цих програмах автовиправлення та Shift+Pause вимкнені. " +
                       "Клавіша Pause для останнього слова працює скрізь. " +
                       "Назву програми пишіть без .exe, регістр не важливий.",
                Font = Theme.CreateFont(9.5F),
                ForeColor = Theme.Muted,
                AutoSize = false,
                Height = 54,
                Margin = new Padding(0, 0, 0, 8)
            });

            // --- Список ---
            var cardList = new CardPanel
            {
                Height = 226,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 10)
            };

            _listExcluded = new ListBox
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
            _listExcluded.DrawItem += ExcludedList_DrawItem;
            _listExcluded.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    RemoveSelectedApps();
                    e.Handled = true;
                }
            };

            cardList.Controls.Add(_listExcluded);
            cards.Controls.Add(cardList);

            // --- Рядок введення та кнопки ---
            var row = new TableLayoutPanel
            {
                Height = 44,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Theme.Back,
                Margin = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _txtApp = new TextBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Theme.Field,
                ForeColor = Theme.Text,
                Font = Theme.CreateFont(10.5F),
                PlaceholderText = "Назва програми, наприклад: notepad",
                Margin = new Padding(0, 0, 8, 0)
            };
            _txtApp.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AddTypedApp();
                    e.SuppressKeyPress = true;
                }
            };

            var buttonAdd = new Button { Text = "Додати", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 6, 3) };
            Theme.StyleButton(buttonAdd, primary: true);
            buttonAdd.Click += (_, _) => AddTypedApp();

            var buttonPick = new Button { Text = "З запущених…", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 6, 3) };
            Theme.StyleButton(buttonPick);
            buttonPick.Click += (_, _) => PickRunningApps();

            var buttonRemove = new Button { Text = "Видалити", Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
            Theme.StyleButton(buttonRemove);
            buttonRemove.Click += (_, _) => RemoveSelectedApps();

            row.Controls.Add(_txtApp, 0, 0);
            row.Controls.Add(buttonAdd, 1, 0);
            row.Controls.Add(buttonPick, 2, 0);
            row.Controls.Add(buttonRemove, 3, 0);

            cards.Controls.Add(row);

            RefreshExcludedList();
        }

        private void ExcludedList_DrawItem(object? sender, DrawItemEventArgs e)
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
                _listExcluded.Items[e.Index]?.ToString() ?? string.Empty,
                Theme.CreateFont(10.5F),
                textBounds,
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static string NormalizeName(string name)
        {
            name = name.Trim();

            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? name[..^4]
                : name;
        }

        private void RefreshExcludedList(string? select = null)
        {
            _listExcluded.BeginUpdate();
            _listExcluded.Items.Clear();

            foreach (string app in _engine.Config.ExcludedApps
                         .Select(NormalizeName)
                         .Where(a => a.Length > 0)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(a => a, StringComparer.OrdinalIgnoreCase))
            {
                int index = _listExcluded.Items.Add(app);

                if (select != null && string.Equals(app, select, StringComparison.OrdinalIgnoreCase))
                    _listExcluded.SelectedIndex = index;
            }

            _listExcluded.EndUpdate();
        }

        private void AddApps(IEnumerable<string> names)
        {
            bool changed = false;
            string? last = null;

            foreach (string raw in names)
            {
                string name = NormalizeName(raw);

                if (name.Length == 0)
                    continue;

                last = name;

                bool exists = _engine.Config.ExcludedApps.Any(a =>
                    string.Equals(NormalizeName(a), name, StringComparison.OrdinalIgnoreCase));

                if (exists)
                    continue;

                _engine.Config.ExcludedApps.Add(name);
                changed = true;
            }

            if (changed)
                _engine.ApplySettings();

            RefreshExcludedList(last);
        }

        private void AddTypedApp()
        {
            AddApps(new[] { _txtApp.Text });

            _txtApp.Clear();
            _txtApp.Focus();
        }

        private void PickRunningApps()
        {
            using var dialog = new ProcessPickerDialog();

            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Selected.Count > 0)
                AddApps(dialog.Selected);
        }

        private void RemoveSelectedApps()
        {
            var selected = _listExcluded.SelectedItems
                .Cast<object>()
                .Select(o => NormalizeName(o.ToString() ?? string.Empty))
                .ToList();

            if (selected.Count == 0)
                return;

            _engine.Config.ExcludedApps.RemoveAll(a =>
                selected.Any(s => string.Equals(NormalizeName(a), s, StringComparison.OrdinalIgnoreCase)));

            _engine.ApplySettings();
            RefreshExcludedList();
        }

        // ==================== СТОРІНКА «ПРО ПРОГРАМУ» ====================

        private void BuildAboutPage()
        {
            FlowLayoutPanel cards = CreatePage("about", "Про програму");

            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

            // --- Назва та версія ---
            CardPanel cardInfo = CreateCard(104);
            AddCardTexts(cardInfo, "AUREON Keyboard Switcher", null);

            cardInfo.Controls.Add(new Label
            {
                Text = $"Версія {version}\r\nАвтоматичне виправлення розкладки клавіатури (EN / UK / DE / RU)",
                Font = Theme.CreateFont(9.5F),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = false,
                Size = new Size(480, 40),
                Location = new Point(18, 44)
            });

            cards.Controls.Add(cardInfo);

            // --- Конфіденційність ---
            CardPanel cardPrivacy = CreateCard(94);
            AddCardTexts(cardPrivacy, "Конфіденційність", null);

            cardPrivacy.Controls.Add(new Label
            {
                Text = "Набраний текст не записується на диск і нікуди не надсилається. " +
                       "Слова обробляються в пам’яті та одразу забуваються.",
                Font = Theme.CreateFont(9.5F),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = false,
                Size = new Size(480, 40),
                Location = new Point(18, 42)
            });

            cards.Controls.Add(cardPrivacy);

            // --- Папка налаштувань ---
            CardPanel cardFolder = CreateCard(82);
            AddCardTexts(cardFolder, "Файл налаштувань", "Зберігається в папці користувача (AppData)");

            var buttonFolder = new Button { Text = "Відкрити папку", Size = new Size(140, 34) };
            Theme.StyleButton(buttonFolder);
            buttonFolder.Click += (_, _) => OpenSettingsFolder();

            PlaceRight(cardFolder, buttonFolder, 20, 24);
            cards.Controls.Add(cardFolder);

            // --- Журнал подій (виклик з SettingsForm.Log.cs) ---
            AddLogCard(cards);
        }

        private void OpenSettingsFolder()
        {
            try
            {
                // Якщо файлу ще немає, створюємо його, щоб папка точно існувала
                if (!File.Exists(AppSettings.FilePath))
                    _engine.Config.Save();

                string? folder = Path.GetDirectoryName(AppSettings.FilePath);

                if (folder != null)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{folder}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не вдалося відкрити папку: " + ex.Message,
                    "AUREON Keyboard Switcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    // =====================================================================
    //  Діалог «Вибрати із запущених програм»
    // =====================================================================
    internal sealed class ProcessPickerDialog : Form
    {
        private readonly ListBox _list;

        public List<string> Selected { get; } = new();

        public ProcessPickerDialog()
        {
            Text = "Запущені програми";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            ClientSize = new Size(380, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Back;
            ForeColor = Theme.Text;
            Font = Theme.CreateFont(10F);

            var info = new Label
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(14, 12, 14, 0),
                ForeColor = Theme.Muted,
                Text = "Оберіть програми (Ctrl або Shift для кількох):"
            };

            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Field,
                ForeColor = Theme.Text,
                Font = Theme.CreateFont(10.5F),
                SelectionMode = SelectionMode.MultiExtended,
                IntegralHeight = false
            };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = Theme.Sidebar };

            var buttonOk = new Button
            {
                Text = "Додати",
                Size = new Size(120, 34),
                DialogResult = DialogResult.OK
            };
            Theme.StyleButton(buttonOk, primary: true);

            var buttonCancel = new Button
            {
                Text = "Скасувати",
                Size = new Size(110, 34),
                DialogResult = DialogResult.Cancel
            };
            Theme.StyleButton(buttonCancel);

            bottom.Controls.Add(buttonOk);
            bottom.Controls.Add(buttonCancel);
            bottom.Resize += (_, _) =>
            {
                buttonOk.Location = new Point(bottom.Width - 14 - buttonOk.Width, 12);
                buttonCancel.Location = new Point(buttonOk.Left - 8 - buttonCancel.Width, 12);
            };

            AcceptButton = buttonOk;
            CancelButton = buttonCancel;

            // Порядок важливий: спочатку Fill, потім Top і Bottom
            Controls.Add(_list);
            Controls.Add(info);
            Controls.Add(bottom);

            foreach (string name in GetRunningAppNames())
                _list.Items.Add(name);

            FormClosing += (_, _) =>
            {
                if (DialogResult == DialogResult.OK)
                {
                    foreach (object item in _list.SelectedItems)
                        Selected.Add(item.ToString() ?? string.Empty);
                }
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.UseDarkTitleBar(this);
        }

        // Програми, які мають вікно (фонові служби не показуємо)
        private static List<string> GetRunningAppNames()
        {
            string own = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);

            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.MainWindowHandle != nint.Zero &&
                        !string.Equals(process.ProcessName, own, StringComparison.OrdinalIgnoreCase))
                    {
                        names.Add(process.ProcessName);
                    }
                }
                catch
                {
                    // Немає доступу до процесу: пропускаємо
                }
                finally
                {
                    process.Dispose();
                }
            }

            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}