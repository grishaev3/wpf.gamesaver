using System.IO;

namespace wpf.gamesaver
{
    public class GameSave
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string NameEn { get; set; } = string.Empty;
        public string SavePathPattern { get; set; } = string.Empty; // Пример: %LOCALAPPDATA%\MortalShell2\Saved\SaveGames\*.sav
        public DateTime? LastCopyToBackup { get; set; }
        public DateTime? LastRestoreFromBackup { get; set; }

        // Получение абсолютного пути к исходной папке сейвов
        public string GetAbsoluteSourceFolder()
        {
            string expanded = Environment.ExpandEnvironmentVariables(SavePathPattern);
            return Path.GetDirectoryName(expanded) ?? string.Empty;
        }

        // Получение маски файлов (например, *.sav)
        public string GetFilePattern()
        {
            return Path.GetFileName(SavePathPattern);
        }

        // Получение пути к локальной папке бэкапа внутри папки приложения \Data\
        public string GetLocalBackupFolder()
        {
            // Формируем безопасное имя папки на основе пути, убирая двоеточия и проценты
            string safeFolderName = SavePathPattern
                .Replace("%", "")
                .Replace(":", "")
                .Replace("*", "");

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", safeFolderName);
        }
    }
}
