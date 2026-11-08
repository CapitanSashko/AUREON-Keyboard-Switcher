using AUREON_Keyboard_Switcher.Core;
using AUREON_Keyboard_Switcher.UI;

namespace AUREON_Keyboard_Switcher
{
    /// <summary>
    /// Програма працює в треї: значок, меню та вікно налаштувань, яке відкривається за запитом.
    /// </summary>
    public sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly SwitcherEngine _engine;
        private readonly NotifyIcon _tray;
        private readonly ToolStripMenuItem _menuAutoCorrect;
        private readonly Icon? _iconOn;
        private readonly Icon? _iconOff;

        private SettingsForm? _settingsForm;
        private bool _cleanedUp;

        public TrayApplicationContext(bool startHidden)
        {
            _iconOn = LoadIcon("AUREON.ico");
            _iconOff = LoadIcon("AUREON_off.ico");

            _engine = new SwitcherEngine(
                SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext());

            // ----- Меню трею -----
            var menuSettings = new ToolStripMenuItem("Налаштування…");
            menuSettings.Font = new Font(menuSettings.Font, FontStyle.Bold);
            menuSettings.Click += (_, _) => ShowSettings();

            _menuAutoCorrect = new ToolStripMenuItem("Автовиправлення розкладки");
            _menuAutoCorrect.Click += (_, _) => _engine.AutoCorrectEnabled = !_engine.AutoCorrectEnabled;

            var menuExit = new ToolStripMenuItem("Вихід");
            menuExit.Click += (_, _) => ExitApplication();

            var menu = new ContextMenuStrip();
            menu.Items.AddRange(new ToolStripItem[]
            {
                menuSettings,
                _menuAutoCorrect,
                new ToolStripSeparator(),
                menuExit
            });

            _tray = new NotifyIcon
            {
                ContextMenuStrip = menu,
                Icon = _iconOn ?? SystemIcons.Application,
                Visible = true
            };
            _tray.DoubleClick += (_, _) => ShowSettings();

            _engine.StateChanged += UpdateTrayState;
            _engine.Notify += message =>
    _tray.ShowBalloonTip(2500, "AUREON Keyboard Switcher", message, ToolTipIcon.Info);
            UpdateTrayState();

            // ----- Запуск двигуна -----
            List<string> problems = _engine.Start();

            // Автозапуск стартує з параметром --tray: відразу в трей, без вікна
            if (!startHidden)
                ShowSettings();

            if (problems.Count > 0)
            {
                MessageBox.Show(
                    string.Join("\n\n", problems),
                    "AUREON Keyboard Switcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static Icon? LoadIcon(string fileName)
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);

                return File.Exists(path)
                    ? new Icon(path, SystemInformation.SmallIconSize)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        // Значок, підказка та галочка в меню відповідають стану автовиправлення
        private void UpdateTrayState()
        {
            bool on = _engine.AutoCorrectEnabled;

            _menuAutoCorrect.Checked = on;

            _tray.Icon = (on ? _iconOn : _iconOff) ?? SystemIcons.Application;

            _tray.Text = on
                ? "AUREON: автовиправлення увімкнено"
                : "AUREON: автовиправлення вимкнено";
        }

        private void ShowSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(_engine, _iconOn);
                _settingsForm.FormClosed += (_, _) =>
                {
                    _settingsForm = null;
                    ShowTrayHintOnce();
                };
            }

            if (!_settingsForm.Visible)
                _settingsForm.Show();

            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;

            _settingsForm.Activate();
        }

        // При першому закритті вікна пояснюємо, що програма продовжує працювати
        private void ShowTrayHintOnce()
        {
            if (!_engine.Config.ShowTrayHint)
                return;

            _tray.ShowBalloonTip(
                2500,
                "AUREON Keyboard Switcher",
                "Програма продовжує працювати в треї. Налаштування: подвійний клік по значку. Вихід: права кнопка → «Вихід».",
                ToolTipIcon.Info);

            _engine.Config.ShowTrayHint = false;
            _engine.Config.Save();
        }

        private void ExitApplication()
        {
            _settingsForm?.Close();

            CleanUp();
            ExitThread();
        }

        private void CleanUp()
        {
            if (_cleanedUp)
                return;

            _cleanedUp = true;

            _engine.StateChanged -= UpdateTrayState;

            // Інакше значок «привидом» залишається в треї до наведення миші
            _tray.Visible = false;
            _tray.Dispose();

            _engine.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                CleanUp();

            base.Dispose(disposing);
        }
    }
}