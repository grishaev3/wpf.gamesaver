using LiteDB;
using System.IO;
using System.Windows;

namespace wpf.gamesaver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string DbName = "SaveBackupData.db";
        private GameSave? _selectedGame;

        public MainWindow()
        {
            InitializeComponent();
            InitDatabase();
            LoadGames();
        }

        private void InitDatabase()
        {
            using var db = new LiteDatabase(DbName);
            var col = db.GetCollection<GameSave>("games");

            if (col.Count() == 0)
            {
                col.Insert(new GameSave
                {
                    NameEn = "Mortal Shell 2",
                    SavePathPattern = @"%LOCALAPPDATA%\MortalShell2\Saved\SaveGames\*.sav"
                });
                col.Insert(new GameSave
                {
                    NameEn = "Witcher 3",
                    SavePathPattern = @"%USERPROFILE%\Documents\The Witcher 3\gamesaves\*.sav"
                });
            }
        }

        private void LoadGames()
        {
            using var db = new LiteDatabase(DbName);
            var col = db.GetCollection<GameSave>("games");
            GamesComboBox.ItemsSource = col.FindAll().ToList();
        }

        private void GamesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedGame = GamesComboBox.SelectedItem as GameSave;
            UpdateUI();
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

        // РЕЗЕРВНОЕ КОПИРОВАНИЕ (В DATA)
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
                foreach (var file in files)
                {
                    string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                    File.Copy(file, destFile, overwrite: true);
                }

                // Обновляем БД
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

        // ВОССТАНОВЛЕНИЕ (ИЗ DATA)
        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;

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

                string[] files = Directory.GetFiles(sourceDir, pattern);
                if (files.Length == 0)
                {
                    MessageBox.Show("В бэкапе нет файлов для восстановления.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // На всякий случай создаем папку игры, если её удалили
                Directory.CreateDirectory(targetDir);

                foreach (var file in files)
                {
                    string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                    File.Copy(file, destFile, overwrite: true);
                }

                // Обновляем БД
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

        private void UpdateGameInDb(GameSave game)
        {
            using var db = new LiteDatabase(DbName);
            var col = db.GetCollection<GameSave>("games");
            col.Update(game);
        }
    }

}