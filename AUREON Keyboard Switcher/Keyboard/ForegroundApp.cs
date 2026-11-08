using System.Runtime.InteropServices;
using System.Text;

namespace AUREON_Keyboard_Switcher.Keyboard
{
    public static class ForegroundApp
    {
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("user32.dll")]
        private static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern nint OpenProcess(uint access, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(nint handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(
            nint process,
            uint flags,
            StringBuilder exeName,
            ref uint size);

        /// <summary>Идентификатор (HWND) активного окна или 0.</summary>
        public static nint GetWindowHandle() => GetForegroundWindow();

        /// <summary>Принадлежит ли активное окно самой программе (настройки, диалоги, меню трея).</summary>
        public static bool IsOwnWindow()
        {
            nint window = GetForegroundWindow();

            if (window == 0)
                return false;

            GetWindowThreadProcessId(window, out uint processId);

            return processId == (uint)Environment.ProcessId;
        }

        /// <summary>
        /// Имя процесса активного окна без расширения (например, "notepad", "WindowsTerminal").
        /// Возвращает null, если определить не удалось (например, окно запущено от администратора).
        /// </summary>
        public static string? GetProcessName()
        {
            nint window = GetForegroundWindow();

            if (window == 0)
                return null;

            GetWindowThreadProcessId(window, out uint processId);

            if (processId == 0)
                return null;

            nint handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);

            if (handle == 0)
                return null;

            try
            {
                var path = new StringBuilder(1024);
                uint size = (uint)path.Capacity;

                if (!QueryFullProcessImageName(handle, 0, path, ref size))
                    return null;

                return Path.GetFileNameWithoutExtension(path.ToString());
            }
            finally
            {
                CloseHandle(handle);
            }
        }
    }
}