using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using AUREON_Keyboard_Switcher.Core;

namespace AUREON_Keyboard_Switcher.Keyboard
{
    /// <summary>
    /// Следит за нажатиями кнопок мыши (по всей системе).
    /// Клик мог переместить курсор ввода, поэтому набираемое слово после него неактуально.
    /// </summary>
    public sealed class MouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;

        private delegate nint HookProc(
            int nCode,
            nint wParam,
            nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint SetWindowsHookEx(
            int idHook,
            HookProc lpfn,
            nint hMod,
            uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll")]
        private static extern nint CallNextHookEx(
            nint hhk,
            int nCode,
            nint wParam,
            nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern nint GetModuleHandle(string? lpModuleName);

        private nint _hookHandle = nint.Zero;

        // Делегат хранится в поле, иначе сборщик мусора удалит его
        private readonly HookProc _hookProcedure;

        private long _lastEventTick;

        /// <summary>Нажата любая кнопка мыши.</summary>
        public event Action? ButtonPressed;

        public bool IsInstalled => _hookHandle != nint.Zero;

        /// <summary>Когда хук последний раз получил любое событие мыши (TickCount64).</summary>
        public long LastEventTick => Interlocked.Read(ref _lastEventTick);

        public MouseHook()
        {
            _hookProcedure = HookCallback;
        }

        public bool Install()
        {
            if (IsInstalled)
                return true;

            _hookHandle = SetWindowsHookEx(
                WH_MOUSE_LL,
                _hookProcedure,
                GetModuleHandle(null),
                0);

            if (_hookHandle == nint.Zero)
            {
                int error = Marshal.GetLastWin32Error();

                Debug.WriteLine($"MouseHook installation failed. Error: {error}");
                AppLog.Warn($"Хук миші не встановлено (код помилки Windows {error})");

                return false;
            }

            Interlocked.Exchange(ref _lastEventTick, Environment.TickCount64);

            return true;
        }

        public void Uninstall()
        {
            if (!IsInstalled)
                return;

            UnhookWindowsHookEx(_hookHandle);

            _hookHandle = nint.Zero;
        }

        /// <summary>Снимает хук и ставит заново (на случай, если Windows сняла его молча).</summary>
        public bool Reinstall()
        {
            Uninstall();
            return Install();
        }

        private nint HookCallback(
            int nCode,
            nint wParam,
            nint lParam)
        {
            if (nCode >= 0)
            {
                // Любое движение или нажатие доказывает, что хук жив
                Interlocked.Exchange(ref _lastEventTick, Environment.TickCount64);

                int message = unchecked((int)wParam);

                if (message == WM_LBUTTONDOWN ||
                    message == WM_RBUTTONDOWN ||
                    message == WM_MBUTTONDOWN ||
                    message == WM_XBUTTONDOWN)
                {
                    try
                    {
                        ButtonPressed?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"MouseHook callback error: {ex.Message}");
                        AppLog.Error("Помилка в обробнику миші", ex);
                    }
                }
            }

            return CallNextHookEx(
                _hookHandle,
                nCode,
                wParam,
                lParam);
        }

        public void Dispose()
        {
            Uninstall();

            GC.SuppressFinalize(this);
        }
    }
}