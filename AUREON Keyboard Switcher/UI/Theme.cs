using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AUREON_Keyboard_Switcher.UI
{
    public static class Theme
    {
        public static readonly Color Back = Color.FromArgb(24, 24, 26);
        public static readonly Color Sidebar = Color.FromArgb(30, 30, 33);
        public static readonly Color Card = Color.FromArgb(38, 38, 42);
        public static readonly Color Field = Color.FromArgb(30, 30, 33);
        public static readonly Color Border = Color.FromArgb(62, 62, 68);

        public static readonly Color Text = Color.FromArgb(236, 236, 238);
        public static readonly Color Muted = Color.FromArgb(145, 145, 152);

        public static readonly Color Accent = Color.FromArgb(76, 141, 255);
        public static readonly Color AccentHover = Color.FromArgb(104, 160, 255);
        public static readonly Color AccentPressed = Color.FromArgb(50, 110, 230);

        private static readonly string FontName =
            FontFamily.Families.Any(f => f.Name == "Bahnschrift") ? "Bahnschrift" : "Segoe UI";

        public static Font CreateFont(float size = 10F, FontStyle style = FontStyle.Regular) =>
            new Font(FontName, size, style);

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(1, radius * 2);

            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            return path;
        }

        public static void StyleButton(Button button, bool primary = false)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Cursor = Cursors.Hand;
            button.Font = CreateFont(10F);

            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Border;

            button.BackColor = primary ? Accent : Color.FromArgb(52, 52, 58);
            button.ForeColor = primary ? Color.White : Text;

            button.FlatAppearance.MouseOverBackColor =
                primary ? AccentHover : Color.FromArgb(64, 64, 70);

            button.FlatAppearance.MouseDownBackColor =
                primary ? AccentPressed : Color.FromArgb(46, 46, 52);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);

        /// <summary>Тёмная строка заголовка окна (Windows 10 1809+ и Windows 11).</summary>
        public static void UseDarkTitleBar(Form form)
        {
            try
            {
                int on = 1;

                // 20: новые сборки Windows, 19: старые (1809 и 1903)
                if (DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int));
            }
            catch
            {
                // Не критично: заголовок останется светлым
            }
        }
    }
}