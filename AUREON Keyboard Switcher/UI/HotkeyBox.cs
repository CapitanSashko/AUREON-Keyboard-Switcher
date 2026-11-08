using System.ComponentModel;
using System.Drawing.Drawing2D;
using AUREON_Keyboard_Switcher.Core;

namespace AUREON_Keyboard_Switcher.UI
{
    /// <summary>Поле для записи сочетания клавиш: щелчок, потом нажатие нужного сочетания.</summary>
    public sealed class HotkeyBox : Control
    {
        private Hotkey _value;
        private bool _recording;

        /// <summary>Пользователь записал новое допустимое сочетание.</summary>
        public event EventHandler? ValueChanged;

        /// <summary>Пользователь нажал недопустимое сочетание (параметр: причина).</summary>
        public event Action<string>? Rejected;

        public HotkeyBox()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);

            Size = new Size(170, 32);
            Cursor = Cursors.Hand;
            TabStop = true;
            Font = Theme.CreateFont(9.5F);
            BackColor = Theme.Card;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Hotkey Value
        {
            get => _value;
            set
            {
                _value = value;
                Invalidate();
            }
        }

        private void StopRecording()
        {
            if (!_recording)
                return;

            _recording = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
                return;

            Focus();
            _recording = !_recording;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Запись с клавиатуры: Enter или Пробел, когда поле в фокусе
            if (!_recording && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))
            {
                _recording = true;
                Invalidate();
                e.Handled = true;
            }
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            StopRecording();
        }

        protected override bool IsInputKey(Keys keyData) =>
            _recording || base.IsInputKey(keyData);

        // Во время записи забираем все клавиши (включая Alt-сочетания и Pause)
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_recording)
                return base.ProcessCmdKey(ref msg, keyData);

            Keys key = keyData & Keys.KeyCode;
            Keys modifiers = keyData & Keys.Modifiers;

            // Esc без модификаторов: отмена записи
            if (key == Keys.Escape && modifiers == Keys.None)
            {
                StopRecording();
                return true;
            }

            // Нажат только модификатор: ждём основную клавишу
            if (Hotkey.IsModifierKey(key))
                return true;

            var hotkey = new Hotkey(
                key,
                Ctrl: (modifiers & Keys.Control) != 0,
                Alt: (modifiers & Keys.Alt) != 0,
                Shift: (modifiers & Keys.Shift) != 0);

            if (!Hotkey.IsAllowed(hotkey, out string reason))
            {
                Rejected?.Invoke(reason);
                return true;
            }

            _value = hotkey;
            StopRecording();

            ValueChanged?.Invoke(this, EventArgs.Empty);

            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            using (GraphicsPath path = Theme.RoundedRect(rect, 8))
            using (var fill = new SolidBrush(Theme.Field))
            using (var pen = new Pen(_recording ? Theme.Accent : Theme.Border, _recording ? 1.5f : 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            string text = _recording
                ? "Натисніть сполучення…"
                : _value.IsEmpty ? "Не задано" : _value.ToString();

            TextRenderer.DrawText(
                g,
                text,
                Font,
                rect,
                _recording ? Theme.Accent : Theme.Text,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }
    }
}