using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace AUREON_Keyboard_Switcher.UI
{
    // =====================================================================
    //  Карточка: панель со скруглёнными углами и тонкой рамкой
    // =====================================================================
    public sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.Card;
            Padding = new Padding(18, 14, 18, 14);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.Clear(Parent?.BackColor ?? Theme.Back);

            using GraphicsPath path = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 10);
            using var fill = new SolidBrush(Theme.Card);
            using var pen = new Pen(Theme.Border);

            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
    }

    // =====================================================================
    //  Переключатель (вкл/выкл)
    // =====================================================================
    public sealed class ToggleSwitch : Control
    {
        private bool _checked;

        public event EventHandler? CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable, true);

            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = Theme.Card;
        }

        // Атрибут скрывает свойство от дизайнера, убирая ошибку WFO1000
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value)
                    return;

                _checked = value;
                Invalidate();

                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            if (Enabled)
                Checked = !Checked;
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData == Keys.Space || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Space && Enabled)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Color track = !Enabled
                ? Color.FromArgb(60, 60, 64)
                : _checked ? Theme.Accent : Color.FromArgb(78, 78, 86);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = (Height - 1) / 2;

            using (GraphicsPath trackPath = Theme.RoundedRect(rect, radius))
            using (var trackBrush = new SolidBrush(track))
            {
                g.FillPath(trackBrush, trackPath);
            }

            // Ползунок
            int d = Height - 8;
            int x = _checked ? Width - d - 4 : 4;

            using (var knob = new SolidBrush(Enabled ? Color.White : Color.FromArgb(150, 150, 154)))
            {
                g.FillEllipse(knob, x, 4, d, d);
            }

            // Рамка фокуса при навигации с клавиатуры
            if (Focused && Enabled)
            {
                using GraphicsPath focusPath = Theme.RoundedRect(rect, radius);
                using var pen = new Pen(Color.FromArgb(160, Theme.AccentHover), 1.5f);

                g.DrawPath(pen, focusPath);
            }
        }
    }

    // =====================================================================
    //  Сегментная кнопка: выбор одного из нескольких вариантов
    // =====================================================================
    public sealed class SegmentedControl : Control
    {
        private string[] _items = Array.Empty<string>();
        private int _selected;
        private int _hover = -1;

        public event EventHandler? SelectedIndexChanged;

        public SegmentedControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            Cursor = Cursors.Hand;
            Font = Theme.CreateFont(9.5F);
            BackColor = Theme.Card;
            Size = new Size(220, 32);
        }

        // Скрываем от дизайнера, чтобы избежать ошибок WFO1000
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string[] Items
        {
            get => _items;
            set { _items = value ?? Array.Empty<string>(); Invalidate(); }
        }

        // Скрываем от дизайнера, чтобы избежать ошибок WFO1000
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _selected;
            set
            {
                if (value == _selected)
                    return;

                _selected = value;
                Invalidate();

                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private int HitTest(Point p)
        {
            if (_items.Length == 0)
                return -1;

            int segmentWidth = Math.Max(1, (Width - 4) / _items.Length);
            int index = (p.X - 2) / segmentWidth;

            return index < 0 || index >= _items.Length ? -1 : index;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int index = HitTest(e.Location);

            if (index != _hover)
            {
                _hover = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            _hover = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
                return;

            int index = HitTest(e.Location);

            if (index >= 0)
                SelectedIndex = index;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            using (GraphicsPath outer = Theme.RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 8))
            using (var fill = new SolidBrush(Theme.Field))
            using (var pen = new Pen(Theme.Border))
            {
                g.FillPath(fill, outer);
                g.DrawPath(pen, outer);
            }

            if (_items.Length == 0)
                return;

            int segmentWidth = (Width - 4) / _items.Length;

            for (int i = 0; i < _items.Length; i++)
            {
                var segment = new Rectangle(2 + i * segmentWidth, 2, segmentWidth, Height - 5);
                bool selected = i == _selected;

                if (selected || i == _hover)
                {
                    using GraphicsPath path = Theme.RoundedRect(segment, 6);
                    using var brush = new SolidBrush(selected ? Theme.Accent : Color.FromArgb(50, 50, 56));

                    g.FillPath(brush, path);
                }

                TextRenderer.DrawText(
                    g,
                    _items[i],
                    Font,
                    segment,
                    selected ? Color.White : Theme.Muted,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }
        }
    }
}