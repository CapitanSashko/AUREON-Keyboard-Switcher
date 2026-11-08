using System.Runtime.InteropServices;
using AUREON_Keyboard_Switcher.Languages;

namespace AUREON_Keyboard_Switcher.Keyboard
{
    public static class LayoutSwitcher
    {
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(
            nint hWnd,
            uint msg,
            nint wParam,
            nint lParam);

        /// <summary>Переключает раскладку в активном окне на указанный язык.</summary>
        public static bool SwitchTo(KeyboardLanguage language)
        {
            Dictionary<KeyboardLanguage, nint> handles = InstalledLayouts.GetHandles();

            if (!handles.TryGetValue(language, out nint layoutHandle))
                return false;

            nint window = GetForegroundWindow();

            if (window == 0)
                return false;

            return PostMessage(window, WM_INPUTLANGCHANGEREQUEST, 0, layoutHandle);
        }
    }
}