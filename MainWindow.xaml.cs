using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TestReporter.Models;

namespace TestReporter
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<LoadedFile> _loadedFiles = new ObservableCollection<LoadedFile>();
        private ObservableCollection<StudentAnswer> _previewAnswers = new ObservableCollection<StudentAnswer>();
        private ObservableCollection<QuestionStat> _questionStats = new ObservableCollection<QuestionStat>();
        private bool _mappingApplied = false;

        public MainWindow()
        {
            InitializeComponent();

            dgLoadedFiles.ItemsSource = _loadedFiles;
            dgPreviewDetail.ItemsSource = _previewAnswers;
            dgQuestionStats.ItemsSource = _questionStats;

            UpdateButtonsState();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("TestReporter — простой парсер ответов студентов. Разработано как пример.", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnImportFilesClick(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "Excel и CSV|*.xlsx;*.xls;*.csv|Все файлы|*.*";
            if (ofd.ShowDialog() == true)
            {
                foreach (var f in ofd.FileNames)
                {
                    var lf = new LoadedFile
                    {
                        FilePath = f,
                        FileName = Path.GetFileNameWithoutExtension(f),
                        Format = Path.GetExtension(f).ToLowerInvariant().TrimStart('.'),
                        Status = "Загружен"
                    };
                    // проверка на совпадение имен
                    if (_loadedFiles.Any(x => x.FileName == lf.FileName))
                    {
                        lf.Status = "Конфликт имени";
                    }
                    _loadedFiles.Add(lf);
                }
                txtStatus.Text = $"Загружено файлов: {_loadedFiles.Count}";
                UpdateButtonsState();
            }
        }

        private void OnSelectFolderClick(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "Выберите папку с файлами тестов";
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var files = Directory.GetFiles(dlg.SelectedPath, "*.*")
                        .Where(f => new[] { ".csv", ".xls", ".xlsx" }.Contains(Path.GetExtension(f).ToLower())).ToArray();
                    foreach (var f in files)
                    {
                        var lf = new LoadedFile
                        {
                            FilePath = f,
                            FileName = Path.GetFileNameWithoutExtension(f),
                            Format = Path.GetExtension(f).ToLowerInvariant().TrimStart('.'),
                            Status = "Загружен"
                        };
                        if (_loadedFiles.Any(x => x.FileName == lf.FileName)) lf.Status = "Конфликт имени";
                        _loadedFiles.Add(lf);
                    }
                    txtStatus.Text = $"Загружено файлов: {_loadedFiles.Count}";
                    UpdateButtonsState();
                }
            }
        }

        private void OnOpenColumnMapping(object sender, RoutedEventArgs e)
        {
            var dlg = new ColumnMappingDialog();
            dlg.Owner = this;
            // TODO: заполнить detected headers на основе первого файла
            if (dlg.ShowDialog() == true)
            {
                _mappingApplied = true;
                txtStatus.Text = "Сопоставление столбцов применено.";
            }
            UpdateButtonsState();
        }

        private void OnResolveNameConflictsClick(object sender, RoutedEventArgs e)
        {
            // Заглушка: открываем сообщение. Реализовать диалог для выбора действий (переименовать/пропустить/заменить)
            System.Windows.MessageBox.Show("Разрешение конфликтов не реализовано. (заглушка)", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void OnApplyFiltersClick(object sender, RoutedEventArgs e)
        {
            // Применяем фильтры — для примера просто парсим загруженные файлы в превью
            _previewAnswers.Clear();
            mainProgress.Value = 0;
            txtStatus.Text = "Импорт и парсинг файлов...";

            var files = _loadedFiles.ToArray();
            int total = files.Length;
            int processed = 0;

            await Task.Run(() =>
            {
                foreach (var lf in files)
                {
                    try
                    {
                        var sa = ParseFileSimple(lf.FilePath);
                        Dispatcher.Invoke(() => _previewAnswers.Add(sa));
                    }
                    catch { }
                    processed++;
                    Dispatcher.Invoke(() => mainProgress.Value = (int)((double)processed / Math.Max(1, total) * 100));
                }
            });

            txtStatus.Text = $"Готово. Превью: {_previewAnswers.Count} записей";
        }

        private async void OnGenerateReportClick(object sender, RoutedEventArgs e)
        {
            // Проверка: есть файлы и сопоставлено ФИО
            if (!_loadedFiles.Any() || !_mappingApplied)
            {
                System.Windows.MessageBox.Show("Нужно загрузить файлы и сопоставить столбцы (ФИО).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtStatus.Text = "Генерация отчёта...";
            mainProgress.Value = 0;

            // Заглушка: имитируем длительную работу
            await Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    await Task.Delay(120);
                    Dispatcher.Invoke(() => mainProgress.Value = i);
                }
            });

            txtStatus.Text = "Отчёт сгенерирован (заглушка).";
            System.Windows.MessageBox.Show("Отчёт сформирован (заглушка).", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnCreateNewReportClick(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Создание нового отчёта (заглушка).", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnUpdateReportClick(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Excel|*.xlsx;*.xls";
            if (ofd.ShowDialog() == true)
            {
                System.Windows.MessageBox.Show($"Обновление файла {ofd.FileName} (заглушка).", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnExportPdfClick(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Экспорт в PDF (заглушка).", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnPrintClick(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show("Печать (заглушка).", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private StudentAnswer ParseFileSimple(string filePath)
        {
            // простой парсер для CSV / TXT — читает первые 3 непустых строки: ФИО, балл, дата
            var lines = new string[0];
            try
            {
                lines = File.ReadAllLines(filePath, Encoding.Default).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            }
            catch { }

            var sa = new StudentAnswer();
            sa.TopicName = Path.GetFileNameWithoutExtension(filePath);
            sa.QuestionName = "Вопрос";
            sa.GroupName = string.Empty;
            sa.Score = 0;
            sa.TestDate = null;

            if (lines.Length > 0) sa.StudentName = lines[0].Trim();
            if (lines.Length > 1)
            {
                if (int.TryParse(lines[1].Trim(), out int sc)) sa.Score = sc;
                else
                {
                    var digits = new string(lines[1].Where(char.IsDigit).ToArray());
                    if (int.TryParse(digits, out sc)) sa.Score = sc;
                }
            }
            if (lines.Length > 2 && DateTime.TryParse(lines[2].Trim(), out DateTime dt)) sa.TestDate = dt;
            if (string.IsNullOrWhiteSpace(sa.StudentName)) sa.StudentName = Path.GetFileNameWithoutExtension(filePath);

            return sa;
        }

        private void UpdateButtonsState()
        {
            bool hasFiles = _loadedFiles.Any();
            btnColumnMap.IsEnabled = hasFiles;
            bool readyForReports = hasFiles && _mappingApplied;
            btnGenerate.IsEnabled = readyForReports;
            btnNewReport.IsEnabled = readyForReports;
            btnUpdateReport.IsEnabled = readyForReports;
            btnExportPdf.IsEnabled = readyForReports;
            btnPrint.IsEnabled = readyForReports;
            btnApplyFilters.IsEnabled = hasFiles && _mappingApplied;
        }
    }
}
