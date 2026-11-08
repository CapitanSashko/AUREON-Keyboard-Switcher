using System.Runtime.InteropServices;
using Microsoft.Win32;
using AUREON_Keyboard_Switcher.Keyboard;

namespace AUREON_Keyboard_Switcher.Core
{
    public enum HookRestoreReason
    {
        /// <summary>Ввод есть, а хуки молчат: Windows их сняла.</summary>
        InputWithoutEvents,

        /// <summary>Компьютер вышел из сна.</summary>
        ResumeFromSleep,

        /// <summary>Разблокировка, вход в сеанс или подключение по RDP.</summary>
        SessionUnlock
    }

    /// <summary>
    /// Следит, чтобы Windows не сняла хуки молча, и ставит их заново, если это произошло.
    /// Создавать и запускать из интерфейсного потока.
    /// </summary>
    public sealed class HookWatchdog : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private const int CheckIntervalMs = 5000;      // как часто проверяем
        private const int InputRecentMs = 3000;        // ввод считается «недавним»
        private const int MissedEventsMs = 1500;       // хуки опоздали с ответом больше, чем на это время
        private const int MinRestartGapMs = 30000;     // не чаще одного восстановления в 30 секунд
        private const int ConfirmChecks = 2;           // сколько подозрительных проверок подтверждают поломку
        private const int SettleDelayMs = 2000;        // пауза после сна/разблокировки

        private readonly Func<KeyboardHook?> _getKeyboard;
        private readonly Func<MouseHook?> _getMouse;
        private readonly Action _resetBuffer;

        private System.Windows.Forms.Timer? _timer;
        private SynchronizationContext? _ui;
        private int _suspiciousChecks;
        private long _lastRestartTick;
        private volatile bool _disposed;

        /// <summary>Сколько раз хуки пришлось восстанавливать после молчаливого снятия.</summary>
        public int RestartCount { get; private set; }

        /// <summary>Хуки переустановлены. Параметры: причина и результат (true = всё установилось).</summary>
        public event Action<HookRestoreReason, bool>? HooksRestored;

        public HookWatchdog(
            Func<KeyboardHook?> getKeyboard,
            Func<MouseHook?> getMouse,
            Action resetBuffer)
        {
            _getKeyboard = getKeyboard;
            _getMouse = getMouse;
            _resetBuffer = resetBuffer;
        }

        public void Start(SynchronizationContext ui)
        {
            _ui = ui;

            _timer = new System.Windows.Forms.Timer { Interval = CheckIntervalMs };
            _timer.Tick += (_, _) => Check();
            _timer.Start();

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        // ---------- Проверка «ввод есть, а хуки молчат» ----------

        private void Check()
        {
            if (_disposed)
                return;

            KeyboardHook? keyboard = _getKeyboard();
            MouseHook? mouse = _getMouse();

            if (keyboard == null && mouse == null)
                return;

            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };

            if (!GetLastInputInfo(ref info))
                return;

            // Время последнего ввода по данным Windows: 32-битный счётчик миллисекунд
            uint now32 = unchecked((uint)Environment.TickCount);
            long sinceInput = unchecked(now32 - info.dwTime);

            // Пользователь сейчас ничего не делает: судить не по чему, счётчик не трогаем
            if (sinceInput > InputRecentMs)
                return;

            long lastHeard = Math.Max(
                keyboard?.LastEventTick ?? 0,
                mouse?.LastEventTick ?? 0);

            long sinceHeard = Environment.TickCount64 - lastHeard;

            // Если хуки живы, они услышали этот ввод примерно тогда же, когда он случился
            bool heard = sinceHeard <= sinceInput + MissedEventsMs;

            if (heard)
            {
                _suspiciousChecks = 0;
                return;
            }

            _suspiciousChecks++;

            bool gapPassed = Environment.TickCount64 - _lastRestartTick >= MinRestartGapMs;

            if (_suspiciousChecks >= ConfirmChecks && gapPassed)
                Restore(HookRestoreReason.InputWithoutEvents);
        }

        // ---------- Сон и блокировка (события приходят из другого потока) ----------

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume)
                ScheduleRestore(HookRestoreReason.ResumeFromSleep);
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                    ScheduleRestore(HookRestoreReason.SessionUnlock);
                    break;
            }
        }

        private void ScheduleRestore(HookRestoreReason reason)
        {
            SynchronizationContext? ui = _ui;

            if (ui == null || _disposed)
                return;

            // Возвращаемся в интерфейсный поток и даём системе пару секунд прийти в себя
            ui.Post(async _ =>
            {
                try
                {
                    await Task.Delay(SettleDelayMs);

                    if (!_disposed)
                        Restore(reason);
                }
                catch (Exception ex)
                {
                    AppLog.Error("Помилка сторожа хуків", ex);
                }
            }, null);
        }

        // ---------- Восстановление ----------

        private void Restore(HookRestoreReason reason)
        {
            _suspiciousChecks = 0;
            _lastRestartTick = Environment.TickCount64;

            // Часть клавиш могла «потеряться» при переустановке: начинаем слово с чистого листа
            _resetBuffer();

            bool ok = true;

            KeyboardHook? keyboard = _getKeyboard();

            if (keyboard != null)
                ok &= keyboard.Reinstall();

            MouseHook? mouse = _getMouse();

            if (mouse != null)
                ok &= mouse.Reinstall();

            if (reason == HookRestoreReason.InputWithoutEvents)
                RestartCount++;

            HooksRestored?.Invoke(reason, ok);
        }

        public void Dispose()
        {
            _disposed = true;

            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            // События SystemEvents статические: без отписки объект остался бы в памяти навсегда
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }
    }
}