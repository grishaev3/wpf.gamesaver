using LiteDB;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using wpf.gamesaver.Types;

namespace wpf.gamesaver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string FileExtensionPattern = @"\*\.[a-zA-Z0-9]+$";
        private const string DbName = "SaveBackupData.db";

        // ОДИН экземпляр подключения для всего окна (предотвращает конфликты доступа)
        private LiteDatabase _db;
        private ILiteCollection<GameSave> _gamesCollection;
        private GameSave? _selectedGame;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Открываем базу данных ОДИН раз при старте приложения
            _db = new LiteDatabase(DbName);
            _gamesCollection = _db.GetCollection<GameSave>("games");

            // 2. Настраиваем закрытие БД при выходе из приложения
            this.Closed += MainWindow_Closed;

            InitDatabase();
            LoadGames();
            SwitchToCreateMode(); // По умолчанию форма стоит в режиме создания новой записи
        }

        private void InitDatabase()
        {
            if (_gamesCollection.Count() == 0)
            {
                _gamesCollection.Insert(new GameSave
                {
                    NameEn = "Mortal Shell 2",
                    SavePathPattern = @"%LOCALAPPDATA%\MortalShell2\Saved\SaveGames\*.sav"
                });
                _gamesCollection.Insert(new GameSave
                {
                    NameEn = "The Witcher 3",
                    SavePathPattern = @"%USERPROFILE%\Documents\The Witcher 3\gamesaves\*.sav"
                });
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _db?.Dispose();
        }

        private void LoadGames()
        {
            // Больше никаких `using var db` внутри методов! Используем общее поле.
            GamesComboBox.ItemsSource = _gamesCollection.FindAll().ToList();
        }

        private void GamesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedGame = GamesComboBox.SelectedItem as GameSave;
            UpdateUI();

            if (_selectedGame != null)
            {
                // Если выбрали игру, переводим форму в режим редактирования этой игры
                SwitchToEditMode(_selectedGame);
            }
        }

        private void UpdateUI()
        {
            if (_selectedGame == null)
            {
                DetailsPanel.Visibility = Visibility.Collapsed;

                return;
            }

            DetailsPanel.Visibility = Visibility.Visible;
            TxtGameName.Text = _selectedGame.NameEn;
            TxtPatternPath.Text = _selectedGame.SavePathPattern;
            TxtResolvedPath.Text = _selectedGame.GetAbsoluteSourceFolder();
            TxtLocalPath.Text = _selectedGame.GetLocalBackupFolder();

            TxtLastBackup.Text = _selectedGame.LastCopyToBackup?.ToString("dd.MM.yyyy HH:mm:ss") ?? "Ни разу";
            TxtLastRestore.Text = _selectedGame.LastRestoreFromBackup?.ToString("dd.MM.yyyy HH:mm:ss") ?? "Ни разу";
        }

        // ================= ЛОГИКА РАБОТЫ С ФОРМОЙ =================

        // Режим добавления новой записи
        private void SwitchToCreateMode()
        {
            FormHeader.Text = "ДОБАВИТЬ НОВУЮ ИГРУ";
            BtnSaveGame.Content = "Добавить в базу данных";
            FormInputName.Text = string.Empty;
            FormInputPath.Text = @"%LOCALAPPDATA%\GameName\Saved\SaveGames\*.sav";
            BtnCancelEdit.Visibility = Visibility.Collapsed;
        }

        private void SwitchToEditMode(GameSave game)
        {
            FormHeader.Text = "РЕДАКТИРОВАТЬ ПУТЬ ИГРЫ";
            BtnSaveGame.Content = "Сохранить изменения";
            FormInputName.Text = game.NameEn;
            FormInputPath.Text = game.SavePathPattern;
            BtnCancelEdit.Visibility = Visibility.Visible;
        }

        private void UpdateGameInDb(GameSave game)
        {
            _gamesCollection.Update(game);
        }

        private void BtnAddNewGame_Click(object sender, RoutedEventArgs e)
        {
            GamesComboBox.SelectedIndex = -1; // Сбрасываем выбор в списке
            _selectedGame = null;
            DetailsPanel.Visibility = Visibility.Collapsed;

            SwitchToCreateMode();
        }

        private void BtnCancelEdit_Click(object sender, RoutedEventArgs e)
        {
            BtnAddNewGame_Click(sender, e);
        }

        private void BtnSaveGame_Click(object sender, RoutedEventArgs e)
        {
            string name = FormInputName.Text.Trim();
            string pathPattern = FormInputPath.Text.Trim();

            // Базовая валидация ввода
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите название игры.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(pathPattern) || !Regex.IsMatch(pathPattern, FileExtensionPattern))
            {
                MessageBox.Show("Путь должен быть заполнен и заканчиваться на расширение (например, *.sav).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            pathPattern = PathEnvironmentConverter.ConvertToEnvironmentPath(pathPattern);

            if (_selectedGame == null)
            {
                // Создаем новую запись
                var newGame = new GameSave
                {
                    NameEn = name,
                    SavePathPattern = pathPattern
                };
                _gamesCollection.Insert(newGame);
                MessageBox.Show($"Игра '{name}' успешно добавлена в базу данных!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // Обновляем текущую выбранную запись
                _selectedGame.NameEn = name;
                _selectedGame.SavePathPattern = pathPattern;
                _gamesCollection.Update(_selectedGame);
                MessageBox.Show("Изменения путей успешно сохранены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // Перезагружаем выпадающий список
            LoadGames();

            // Сбрасываем форму в режим добавления новой
            BtnAddNewGame_Click(sender, e);
        }

        // ================= СТАНДАРТНЫЕ МЕТОДЫ БЭКАПА И ВОССТАНОВЛЕНИЯ =================

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;

            try
            {
                string sourceDir = _selectedGame.GetAbsoluteSourceFolder();
                string targetDir = _selectedGame.GetLocalBackupFolder();
                string pattern = _selectedGame.GetFilePattern();

                if (!Directory.Exists(sourceDir))
                {
                    MessageBox.Show($"Исходная папка сейвов не найдена:\n{sourceDir}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string[] files = Directory.GetFiles(sourceDir, pattern);
                if (files.Length == 0)
                {
                    MessageBox.Show($"Файлы по маске {pattern} не найдены.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Directory.CreateDirectory(targetDir);
                foreach (string file in files)
                {
                    string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                    File.Copy(file, destFile, overwrite: true);
                }

                _selectedGame.LastCopyToBackup = DateTime.Now;
                UpdateGameInDb(_selectedGame);
                UpdateUI();

                MessageBox.Show("Резервная копия успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при копировании: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null)
            {
                return;
            }

            try
            {
                string sourceDir = _selectedGame.GetLocalBackupFolder();
                string targetDir = _selectedGame.GetAbsoluteSourceFolder();
                string pattern = _selectedGame.GetFilePattern();

                if (!Directory.Exists(sourceDir))
                {
                    MessageBox.Show("Локальная папка бэкапа пуста или не существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string[] backupFiles = Directory.GetFiles(sourceDir, pattern);
                if (backupFiles.Length == 0)
                {
                    MessageBox.Show("В бэкапе нет файлов для восстановления.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверяем, есть ли уже файлы в папке игры, чтобы предупредить о перезаписи
                if (Directory.Exists(targetDir))
                {
                    string[] existingFiles = Directory.GetFiles(targetDir, pattern);
                    if (existingFiles.Length > 0)
                    {
                        // Находим самые свежие файлы в обеих папках для наглядности в MessageBox
                        DateTime lastBackupTime = backupFiles.Max(f => File.GetLastWriteTime(f));
                        DateTime lastGameTime = existingFiles.Max(f => File.GetLastWriteTime(f));

                        string message = $"В папке игры уже есть существующие сохранения!\n\n" +
                                         $"📅 Сейвы в игре изменены: {lastGameTime:dd.MM.yyyy HH:mm:ss}\n" +
                                         $"📦 Сейвы в бэкапе изменены: {lastBackupTime:dd.MM.yyyy HH:mm:ss}\n\n" +
                                         $"Вы уверены, что хотите ВОССТАНОВИТЬ бэкап и затереть текущие файлы игры?";

                        // Запрос подтверждения у пользователя
                        MessageBoxResult result = MessageBox.Show(
                            message,
                            "Внимание! Перезапись файлов",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning,
                            MessageBoxResult.No); // По умолчанию фокус на кнопке "Нет" для безопасности

                        if (result != MessageBoxResult.Yes)
                        {
                            return; // Отмена операции
                        }
                    }
                }
                else
                {
                    // Если папки игры не существовало (например, игру переустановили), создаем её
                    Directory.CreateDirectory(targetDir);
                }
                foreach (string file in backupFiles)
                {
                    string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                    File.Copy(file, destFile, overwrite: true);
                }

                _selectedGame.LastRestoreFromBackup = DateTime.Now;
                UpdateGameInDb(_selectedGame);
                UpdateUI();

                MessageBox.Show("Сейвы успешно восстановлены в папку игры!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при восстановлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFontPlus_Click(object sender, RoutedEventArgs e)
        {
            if (this.FontSize < 24) this.FontSize += 1;
        }

        private void BtnFontMinus_Click(object sender, RoutedEventArgs e)
        {
            if (this.FontSize > 10) this.FontSize -= 1;
        }
    }
}