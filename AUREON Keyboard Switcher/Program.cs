using System.Reflection;
using AUREON_Keyboard_Switcher.Core;
using AUREON_Keyboard_Switcher.Settings;

namespace AUREON_Keyboard_Switcher
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            // Лише один екземпляр програми на користувача
            using var mutex = new Mutex(
                initiallyOwned: true,
                name: @"Local\AUREON_Keyboard_Switcher_SingleInstance",
                createdNew: out bool createdNew);

            bool startHidden = args.Contains("--tray", StringComparer.OrdinalIgnoreCase);

            if (!createdNew)
            {
                // При автозапуску мовчимо, при ручному запуску пояснюємо, де програма
                if (!startHidden)
                {
                    MessageBox.Show(
                        "AUREON Keyboard Switcher вже запущено. Значок знаходиться в області сповіщень (трей): " +
                        "подвійний клік відкриє налаштування.",
                        "AUREON Keyboard Switcher",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            // Журнал: враховуємо налаштування «Вести журнал», хоча двигун ще не створено
            AppLog.Enabled = AppSettings.Load().LoggingEnabled;
            SetupErrorHandling();

            // Явно задаємо контекст інтерфейсного потоку: через нього двигун відкладає дії
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
            AppLog.Info($"=== AUREON Keyboard Switcher {version} запущено ===");

            try
            {
                Application.Run(new TrayApplicationContext(startHidden));
            }
            finally
            {
                AppLog.Info("=== Роботу завершено ===");
                AppLog.Shutdown();
            }
        }

        // Помилки пишуться в журнал зі стеком; помилка в інтерфейсному потоці не валить програму
        private static void SetupErrorHandling()
        {
            // Має викликатися до створення першого вікна
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            Application.ThreadException += (_, e) =>
                AppLog.Error("Необроблена помилка в потоці інтерфейсу", e.Exception);

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                AppLog.Error(
                    $"Критична помилка (програма завершується: {e.IsTerminating})",
                    e.ExceptionObject as Exception);

                // Дописуємо журнал до кінця, поки процес ще живий
                AppLog.Shutdown();
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                AppLog.Error("Необроблена помилка у фоновому завданні", e.Exception);
                e.SetObserved();
            };
        }
    }
}