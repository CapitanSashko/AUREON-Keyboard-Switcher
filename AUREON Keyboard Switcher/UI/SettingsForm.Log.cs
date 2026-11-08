using System.Diagnostics;
using AUREON_Keyboard_Switcher.Core;

namespace AUREON_Keyboard_Switcher.UI
{
    // Частина вікна налаштувань: картка «Журнал подій» на сторінці «Про програму»
    public sealed partial class SettingsForm
    {
        private ToggleSwitch _toggleLog = null!;
        private Label _lblHookStatus = null!;

        private void AddLogCard(FlowLayoutPanel cards)
        {
            CardPanel card = CreateCard(124);

            AddCardTexts(card,
                "Журнал подій",
                "Без набраного тексту: лише події та помилки");

            _toggleLog = new ToggleSwitch { Checked = _engine.Config.LoggingEnabled };
            _toggleLog.CheckedChanged += (_, _) =>
            {
                if (_syncing)
                    return;

                _engine.Config.LoggingEnabled = _toggleLog.Checked;
                _engine.ApplySettings();
            };

            PlaceRight(card, _toggleLog, 20, 18);

            // Стан перехоплення
            _lblHookStatus = new Label
            {
                Font = Theme.CreateFont(9.5F),
                ForeColor = Theme.Muted,
                BackColor = Theme.Card,
                AutoSize = false,
                Size = new Size(360, 20),
                Location = new Point(18, 84)
            };
            card.Controls.Add(_lblHookStatus);

            var buttonOpen = new Button { Text = "Відкрити папку", Size = new Size(140, 32) };
            Theme.StyleButton(buttonOpen);
            buttonOpen.Click += (_, _) => OpenLogFolder();

            PlaceRight(card, buttonOpen, 20, 78);

            cards.Controls.Add(card);

            UpdateHookStatus();

            _engine.StateChanged += UpdateHookStatus;
            FormClosed += (_, _) => _engine.StateChanged -= UpdateHookStatus;
        }

        private void UpdateHookStatus()
        {
            if (_lblHookStatus == null || _lblHookStatus.IsDisposed)
                return;

            string state = _engine.KeyboardHookActive ? "працює" : "не активне";

            _lblHookStatus.Text =
                $"Перехоплення клавіатури: {state} · відновлень після збою: {_engine.HookRecoveries}";
        }

        private void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(AppLog.Folder);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{AppLog.Folder}\"",
                    UseShellExecute = true
                });
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
}