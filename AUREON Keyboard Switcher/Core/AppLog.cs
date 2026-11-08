using System.Collections.Concurrent;
using System.Text;

namespace AUREON_Keyboard_Switcher.Core
{
    /// <summary>
    /// Простой файловый журнал событий.
    /// Набранный текст в журнал НЕ пишется: только события (запуск, исправление, ошибки).
    /// Запись идёт в фоновом потоке, чтобы не тормозить обработчики клавиатуры.
    /// </summary>
    public static class AppLog
    {
        private const int KeepDays = 7;
        private const long MaxFileBytes = 1_000_000;

        private static readonly BlockingCollection<string> Queue = new(boundedCapacity: 2000);
        private static readonly object StartLock = new();
        private static Thread? _writer;
        private static volatile bool _enabled = true;

        /// <summary>Включён ли журнал.</summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static string Folder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AUREON Keyboard Switcher",
                "logs");

        public static void Info(string message) => Write("INFO ", message);

        public static void Warn(string message) => Write("WARN ", message);

        public static void Error(string message, Exception? exception = null) =>
            Write("ERROR", exception == null ? message : message + Environment.NewLine + exception);

        private static void Write(string level, string message)
        {
            if (!_enabled || Queue.IsAddingCompleted)
                return;

            try
            {
                EnsureWriter();

                // Если очередь переполнена, строка теряется: это лучше, чем тормозить хук
                Queue.TryAdd($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}");
            }
            catch
            {
                // Журнал никогда не должен ронять программу
            }
        }

        /// <summary>Дописывает оставшиеся строки и останавливает запись (вызывать при выходе).</summary>
        public static void Shutdown()
        {
            try
            {
                Queue.CompleteAdding();
                _writer?.Join(1500);
            }
            catch
            {
                // Не критично
            }
        }

        private static void EnsureWriter()
        {
            if (_writer != null)
                return;

            lock (StartLock)
            {
                if (_writer != null)
                    return;

                _writer = new Thread(WriterLoop)
                {
                    IsBackground = true,
                    Name = "AUREON log writer"
                };

                _writer.Start();
            }
        }

        private static void WriterLoop()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                CleanOldFiles();
            }
            catch
            {
                // Папку создать не удалось: строки будут отбрасываться ниже
            }

            foreach (string line in Queue.GetConsumingEnumerable())
            {
                try
                {
                    string path = Path.Combine(Folder, $"aureon-{DateTime.Now:yyyyMMdd}.log");

                    // Файл за день разросся: дальше не пишем, чтобы журнал не занял весь диск
                    if (File.Exists(path) && new FileInfo(path).Length > MaxFileBytes)
                        continue;

                    using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false));

                    writer.WriteLine(line);
                }
                catch
                {
                    // Например, файл занят антивирусом: пропускаем строку
                }
            }
        }

        private static void CleanOldFiles()
        {
            DateTime limit = DateTime.Now.AddDays(-KeepDays);

            foreach (string file in Directory.GetFiles(Folder, "aureon-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < limit)
                        File.Delete(file);
                }
                catch
                {
                    // Файл занят: удалим в следующий раз
                }
            }
        }
    }
}