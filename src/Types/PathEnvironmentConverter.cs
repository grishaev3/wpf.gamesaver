namespace wpf.gamesaver.Types
{
    public static class PathEnvironmentConverter
    {
        // Словарь сопоставления путей текущего пользователя с переменными окружения.
        // Порядок важен: сначала проверяем глубокие папки (Local/Roaming), затем корень профиля.
        private static readonly (string FullPath, string EnvVariable)[] EnvironmentFolders;

        static PathEnvironmentConverter()
        {
            // Получаем реальные пути для текущего ПК
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); // C:\Users\...\AppData\Local
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);           // C:\Users\...\AppData\Roaming
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);           // C:\Users\...

            EnvironmentFolders =
            [
                (localAppData, "%LOCALAPPDATA%"),
                (appData, "%APPDATA%"),
                (userProfile, "%USERPROFILE%")
            ];
        }

        /// <summary>
        /// Заменяет абсолютный путь текущего пользователя на путь с переменной окружения.
        /// </summary>
        /// <param name="absolutePath">Исходный путь, например: C:\Users\msi-3060\Documents\game\*.sav</param>
        /// <returns>Путь с переменной окружения или исходный путь, если совпадений не найдено.</returns>
        public static string ConvertToEnvironmentPath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            // Приводим к единому виду (убираем лишние пробелы по краям)
            string resultPath = absolutePath.Trim();

            foreach (var folder in EnvironmentFolders)
            {
                // Проверяем, начинается ли переданный путь с пути системной папки (без учета регистра)
                if (resultPath.StartsWith(folder.FullPath, StringComparison.OrdinalIgnoreCase))
                {
                    // Заменяем абсолютную часть на переменную окружения
                    resultPath = folder.EnvVariable + resultPath.Substring(folder.FullPath.Length);
                    break; // Прерываем цикл, так как нашли наиболее точное совпадение
                }
            }

            return resultPath;
        }
    }
}
