using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AUREON_Keyboard_Switcher.Core;

namespace AUREON_Keyboard_Switcher.Keyboard
{
    /// <summary>
    /// Данные о нажатой клавише: сама клавиша и состояние модификаторов.
    /// </summary>
    public readonly record struct KeyEvent(
        Keys Key,
        uint ScanCode,
        bool Shift,
        bool CapsLock,
        bool Ctrl,
        bool Alt);

    public sealed class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        // Флаг «событие создано программно» (SendInput и т.п.).
        // Нужен, чтобы не реагировать на собственный ввод.
        private const uint LLKHF_INJECTED = 0x10;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;     // Alt
        private const int VK_CAPITAL = 0x14;  // CapsLock

        // Windows снимает хук, если обработчик отвечает дольше ~300 мс.
        // Всё, что дольше этого порога, пишем в журнал как предупреждение.
        private const double SlowCallbackMs = 150;
        private const long SlowLogGapMs = 5000;

        private delegate nint HookProc(
            int nCode,
            nint wParam,
            nint lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public nint dwExtraInfo;
        }

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

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern nint GetModuleHandle(string? lpModuleName);

        private nint _hookHandle = nint.Zero;

        // Делегат хранится в поле, иначе сборщик мусора удалит его,
        // и приложение упадёт при следующем нажатии клавиши.
        private readonly HookProc _hookProcedure;

        private long _lastEventTick;
        private long _lastSlowLogTick;

        /// <summary>Вызывается при каждом нажатии клавиши (не программном).</summary>
        public event Action<KeyEvent>? KeyPressed;

        public bool IsInstalled => _hookHandle != nint.Zero;

        /// <summary>
        /// Когда хук последний раз получил любое событие клавиатуры (TickCount64).
        /// Если ввод есть, а это значение не меняется, значит, Windows сняла хук.
        /// </summary>
        public long LastEventTick => Interlocked.Read(ref _lastEventTick);

        public KeyboardHook()
        {
            _hookProcedure = HookCallback;
        }

        public bool Install()
        {
            if (IsInstalled)
                return true;

            nint moduleHandle = GetModuleHandle(null);

            _hookHandle = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                _hookProcedure,
                moduleHandle,
                0);

            if (_hookHandle == nint.Zero)
            {
                int error = Marshal.GetLastWin32Error();

                Debug.WriteLine(
                    $"KeyboardHook installation failed. Error: {error}");

                AppLog.Error($"Хук клавіатури не встановлено (код помилки Windows {error})");

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
                // Любое событие (даже отпускание клавиши) доказывает, что хук жив
                Interlocked.Exchange(ref _lastEventTick, Environment.TickCount64);

                int message = unchecked((int)wParam);

                if (message == WM_KEYDOWN ||
                    message == WM_SYSKEYDOWN)
                {
                    KBDLLHOOKSTRUCT keyInfo =
                        Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                    // Свои собственные (программные) нажатия игнорируем
                    bool injected = (keyInfo.flags & LLKHF_INJECTED) != 0;

                    if (!injected)
                    {
                        // Внутри low-level хука для зажатых клавиш надёжнее GetAsyncKeyState,
                        // а для CapsLock (переключатель) подходит GetKeyState.
                        bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                        bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                        bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                        bool caps = (GetKeyState(VK_CAPITAL) & 0x0001) != 0;

                        KeyEvent keyEvent = new(
                            (Keys)keyInfo.vkCode,
                            keyInfo.scanCode,
                            shift,
                            caps,
                            ctrl,
                            alt);

                        long started = Stopwatch.GetTimestamp();

                        try
                        {
                            KeyPressed?.Invoke(keyEvent);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(
                                $"KeyboardHook callback error: {ex.Message}");

                            AppLog.Error("Помилка в обробнику клавіатури", ex);
                        }

                        ReportIfSlow(started);
                    }
                }
            }

            return CallNextHookEx(
                _hookHandle,
                nCode,
                wParam,
                lParam);
        }

        private void ReportIfSlow(long startedTimestamp)
        {
            double elapsedMs = Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;

            if (elapsedMs < SlowCallbackMs)
                return;

            long now = Environment.TickCount64;

            // Не засмічуємо журнал повторами
            if (now - _lastSlowLogTick < SlowLogGapMs)
                return;

            _lastSlowLogTick = now;

            AppLog.Warn(
                $"Обробник клавіатури працював {elapsedMs:F0} мс " +
                "(Windows знімає хук, якщо це триває близько 300 мс)");
        }

        public void Dispose()
        {
            Uninstall();

            GC.SuppressFinalize(this);
        }
    }
}