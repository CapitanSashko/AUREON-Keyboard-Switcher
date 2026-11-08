using System.Text.Json;

namespace AUREON_Keyboard_Switcher.Settings
{
    public sealed class AppSettings
    {
        /// <summary>Включено ли автоисправление раскладки.</summary>
        public bool AutoCorrectEnabled { get; set; } = false;

        /// <summary>Показывать ли подсказку «программа работает в трее» при первом закрытии окна.</summary>
        public bool ShowTrayHint { get; set; } = true;

        /// <summary>
        /// Какой язык предпочесть, если слово подходит и русскому, и украинскому
        /// ("Ukrainian" или "Russian").
        /// </summary>
        public string PreferredLanguage { get; set; } = "Ukrainian";

        /// <summary>
        /// Слова, которые никогда не исправляются (в том виде, как они выглядят на экране).
        /// Пополняется вручную и самообучением.
        /// </summary>
        public List<string> UserWords { get; set; } = new();

        /// <summary>
        /// Добавлять слово в словарь, если автоисправление отменено клавишей Pause.
        /// </summary>
        public bool SelfLearningEnabled { get; set; } = true;
        /// <summary>Вести ли журнал событий (набранный текст в журнал не пишется).</summary>
        public bool LoggingEnabled { get; set; } = true;

        /// <summary>Сочетание клавиш «исправить последнее слово».</summary>
        public string HotkeyLastWord { get; set; } = "Pause";

        /// <summary>Сочетание клавиш «преобразовать выделенный текст».</summary>
        public string HotkeySelection { get; set; } = "Shift+Pause";

        /// <summary>Сполучення клавіш «транслітерація виділеного тексту».</summary>
        public string HotkeyTransliterate { get; set; } = "Ctrl+Alt+T";

        /// <summary>Виправляти випадково ввімкнений CapsLock («пРИВІТ» → «Привіт»).</summary>
        public bool FixCapsLock { get; set; } = true;

        /// <summary>Виправляти подвійну велику літеру на початку кириличного слова («ПРивіт»).</summary>
        public bool FixDoubleCapital { get; set; } = true;

        /// <summary>Исправлять двухбуквенные слова (Як, на, до…).</summary>
        public bool FixTwoLetterWords { get; set; } = true;

        /// <summary>Исправлять вместе с текущим словом и предыдущие слова фразы.</summary>
        public bool FixPreviousWords { get; set; } = true;

        /// <summary>
        /// Программы (имя процесса без .exe), в которых автоисправление не работает.
        /// Регистр не важен. Запись "notepad" и "notepad.exe" равнозначны.
        /// </summary>
        public List<string> ExcludedApps { get; set; } = new()
        {
            // терминалы
            "cmd", "powershell", "pwsh", "WindowsTerminal", "conhost", "putty",
            // редакторы кода и IDE (Visual Studio, VS Code, JetBrains)
            "devenv", "Code", "rider64", "idea64", "pycharm64",
            // менеджеры паролей
            "KeePass", "KeePassXC", "Bitwarden", "1Password"
        };

        public bool IsExcluded(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            foreach (string entry in ExcludedApps)
            {
                string name = entry.Trim();

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name[..^4];

                if (string.Equals(name, processName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string FolderPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AUREON Keyboard Switcher");

        public static string FilePath => Path.Combine(FolderPath, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // Файл повреждён (например, ошибка в JSON после ручной правки).
                // Сохраняем копию, чтобы правки не пропали, и работаем с настройками по умолчанию.
                try { File.Copy(FilePath, FilePath + ".bad", overwrite: true); } catch { }
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);

                string json = JsonSerializer.Serialize(
                    this,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // Не удалось сохранить: работаем дальше с текущими значениями
            }
        }
    }
}