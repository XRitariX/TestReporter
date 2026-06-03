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
using System.Collections.Generic;
using ClosedXML.Excel;

namespace TestReporter
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<LoadedFile> _loadedFiles = new ObservableCollection<LoadedFile>();
        private ObservableCollection<StudentAnswer> _previewAnswers = new ObservableCollection<StudentAnswer>();
        private List<StudentAnswer> _allPreviewAnswers = new List<StudentAnswer>();
        private ObservableCollection<QuestionStat> _questionStats = new ObservableCollection<QuestionStat>();
        private bool _mappingApplied = false;
        //private bool _filtersPopulated = false;
        private Models.ColumnMapping? _currentMapping = null;

        public MainWindow()
        {
            InitializeComponent();
            // no-op (initialized)

            dgLoadedFiles.ItemsSource = _loadedFiles;
            dgPreviewDetail.ItemsSource = _previewAnswers;
            dgQuestionStats.ItemsSource = _questionStats;

            UpdateButtonsState();
        }

        // Возвращает все записи из файла как IEnumerable<StudentAnswer>
        private IEnumerable<StudentAnswer> ParseFileRecords(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".xlsx" || ext == ".xls")
            {
                using (var wb = new XLWorkbook(path))
                {
                    var ws = wb.Worksheets.First();
                    var firstRow = ws.FirstRowUsed();
                    if (firstRow == null) yield break;

                    var headerCells = firstRow.CellsUsed().ToList();
                    var headers = headerCells.Select(c => c.GetString()?.Trim() ?? string.Empty).ToList();

                    // Используем текущий маппинг, если применён
                    int idxName = -1, idxGroup = -1, idxDate = -1;
                    var questionIndices = new List<int>();

                    if (_currentMapping != null && _mappingApplied)
                    {
                        var map = _currentMapping;
                        for (int i = 0; i < headers.Count; i++)
                        {
                            var raw = headers[i];
                            if (!string.IsNullOrWhiteSpace(map.NameColumn) && 
                                string.Equals(raw, map.NameColumn, StringComparison.OrdinalIgnoreCase))
                                idxName = i;
                            else if (!string.IsNullOrWhiteSpace(map.GroupColumnName) && 
                                     string.Equals(raw, map.GroupColumnName, StringComparison.OrdinalIgnoreCase))
                                idxGroup = i;
                            else if (!string.IsNullOrWhiteSpace(map.DateColumnName) && 
                                     string.Equals(raw, map.DateColumnName, StringComparison.OrdinalIgnoreCase))
                                idxDate = i;
                            else if (map.QuestionColumns != null && 
                                     map.QuestionColumns.Any(q => string.Equals(q, raw, StringComparison.OrdinalIgnoreCase)))
                            {
                                questionIndices.Add(i);
                            }
                        }
                    }
                    else
                    {
                        // Автоматический маппинг
                        for (int i = 0; i < headers.Count; i++)
                        {
                            var h = headers[i].ToLowerInvariant();
                            if (h.Contains("фам") || h.Contains("имя") || h.Contains("фио") || h.Contains("name"))
                                idxName = i;
                            else if (h.Contains("групп") || h.Contains("group"))
                                idxGroup = i;
                            else if (h.Contains("время") || h.Contains("дата") || h.Contains("time") || h.Contains("date"))
                                idxDate = i;
                            else if (!h.Contains("результ") && !h.Contains("итог") && !h.Contains("всего") && !h.Contains("набрано"))
                            {
                                questionIndices.Add(i);
                            }
                        }
                    }

                        var usedRows = ws.RowsUsed().Skip(1);
                    foreach (var row in usedRows)
                    {
                        string studentName = null;
                        string groupName = null;
                        DateTime? testDate = null;

                        // Получаем имя студента
                        if (idxName >= 0)
                        {
                            studentName = row.Cell(idxName + 1).GetString()?.Trim();
                        }
                        if (string.IsNullOrWhiteSpace(studentName))
                            studentName = Path.GetFileNameWithoutExtension(path);

                        // Получаем группу
                        if (idxGroup >= 0)
                        {
                            groupName = row.Cell(idxGroup + 1).GetString()?.Trim();
                        }

                        // Получаем дату
                        if (idxDate >= 0)
                        {
                            var dateCell = row.Cell(idxDate + 1);
                            try
                            {
                                if (dateCell.TryGetValue<DateTime>(out var dt))
                                    testDate = dt;
                            }
                            catch { }
                        }

                        if (!testDate.HasValue)
                            testDate = DateTime.UtcNow;

                        // Обрабатываем каждый вопрос
                        foreach (var qi in questionIndices)
                        {
                            var qName = qi < headers.Count ? headers[qi] : $"Q{qi}";
                            var scoreCell = row.Cell(qi + 1);
                            int score = 0;

                            try
                            {
                                if (scoreCell.TryGetValue<int>(out var intVal))
                                    score = intVal;
                                else if (scoreCell.TryGetValue<double>(out var dVal))
                                    score = (int)dVal;
                                else
                                {
                                    var strVal = scoreCell.GetString();
                                    if (!string.IsNullOrEmpty(strVal) && int.TryParse(strVal, out var parsed))
                                        score = parsed;
                                }
                            }
                            catch { }

                            yield return new StudentAnswer
                            {
                                StudentName = studentName,
                                TopicName = Path.GetFileNameWithoutExtension(path),
                                QuestionName = qName,
                                Score = score,
                                GroupName = groupName ?? string.Empty,
                                TestDate = testDate
                            };
                        }
                    }
                }
            }
            else if (ext == ".csv" || ext == ".txt")
            {
                // CSV логика остается без изменений
                string[] lines;
                try { lines = File.ReadAllLines(path, Encoding.Default); }
                catch { yield break; }

                if (lines.Length == 0) yield break;

                var header = lines[0];
                var sep = header.Contains(',') ? ',' : (header.Contains(';') ? ';' : '\t');
                var headers = header.Split(sep).Select(s => s.Trim()).ToList();

                int idxName = -1, idxGroup = -1, idxDate = -1;
                var questionIndices = new List<int>();

                for (int i = 0; i < headers.Count; i++)
                {
                    var raw = headers[i];
                    var h = raw.ToLowerInvariant();

                    if (_currentMapping != null && _mappingApplied)
                    {
                        var map = _currentMapping;
                        if (!string.IsNullOrWhiteSpace(map.NameColumn) && 
                            string.Equals(raw, map.NameColumn, StringComparison.OrdinalIgnoreCase))
                            idxName = i;
                        else if (!string.IsNullOrWhiteSpace(map.GroupColumnName) && 
                                 string.Equals(raw, map.GroupColumnName, StringComparison.OrdinalIgnoreCase))
                            idxGroup = i;
                        else if (!string.IsNullOrWhiteSpace(map.DateColumnName) && 
                                 string.Equals(raw, map.DateColumnName, StringComparison.OrdinalIgnoreCase))
                            idxDate = i;
                        else if (map.QuestionColumns != null && 
                                 map.QuestionColumns.Any(q => string.Equals(q, raw, StringComparison.OrdinalIgnoreCase)))
                        {
                            questionIndices.Add(i);
                        }
                    }
                    else
                    {
                        if (h.Contains("фам") || h.Contains("имя") || h.Contains("фио") || h.Contains("name"))
                            idxName = i;
                        else if (h.Contains("групп") || h.Contains("group"))
                            idxGroup = i;
                        else if (h.Contains("время") || h.Contains("дата") || h.Contains("time") || h.Contains("date"))
                            idxDate = i;
                        else if (!h.Contains("результ") && !h.Contains("итог") && !h.Contains("всего") && !h.Contains("набрано"))
                        {
                            questionIndices.Add(i);
                        }
                    }
                }

                for (int r = 1; r < lines.Length; r++)
                {
                    var parts = lines[r].Split(sep).Select(s => s.Trim()).ToList();
                    var studentName = idxName >= 0 && idxName < parts.Count ? parts[idxName] : string.Empty;
                    var groupName = idxGroup >= 0 && idxGroup < parts.Count ? parts[idxGroup] : string.Empty;
                    DateTime? testDate = null;

                    if (idxDate >= 0 && idxDate < parts.Count)
                    {
                        var s = parts[idxDate];
                        if (!string.IsNullOrEmpty(s) && DateTime.TryParse(s, out var dt))
                            testDate = dt;
                    }

                    if (!testDate.HasValue)
                        testDate = DateTime.UtcNow;

                    foreach (var qi in questionIndices)
                    {
                        var qName = qi < headers.Count ? headers[qi] : $"Q{qi}";
                        int score = 0;

                        if (qi < parts.Count)
                        {
                            var s = parts[qi];
                            if (!string.IsNullOrEmpty(s))
                            {
                                if (!int.TryParse(s, out score))
                                {
                                    var digits = new string(s.Where(char.IsDigit).ToArray());
                                    if (!int.TryParse(digits, out score))
                                        score = 0;
                                }
                            }
                        }

                        yield return new StudentAnswer
                        {
                            StudentName = string.IsNullOrWhiteSpace(studentName) 
                                ? Path.GetFileNameWithoutExtension(path) 
                                : studentName,
                            TopicName = Path.GetFileNameWithoutExtension(path),
                            QuestionName = qName,
                            Score = score,
                            GroupName = groupName ?? string.Empty,
                            TestDate = testDate
                        };
                    }
                }
            }
        }

        private StudentAnswer ParseExcelFirstRecord(string path)
        {
            var sa = new StudentAnswer();
            sa.TopicName = Path.GetFileNameWithoutExtension(path);
            sa.QuestionName = "Вопрос";
            sa.GroupName = string.Empty;
            sa.Score = 0;
            sa.TestDate = null;

            try
            {
                using (var wb = new ClosedXML.Excel.XLWorkbook(path))
                {
                    var ws = wb.Worksheets.First();
                    var firstRow = ws.FirstRowUsed();
                    if (firstRow != null)
                    {
                        var cells = firstRow.CellsUsed().ToList();
                        if (cells.Count >= 1) sa.StudentName = cells[0].GetString();
                        if (cells.Count >= 2 && int.TryParse(cells[1].GetString(), out var sc)) sa.Score = sc;
                        if (cells.Count >= 3 && DateTime.TryParse(cells[2].GetString(), out var dt)) sa.TestDate = dt;
                    }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(sa.StudentName)) sa.StudentName = Path.GetFileNameWithoutExtension(path);
            return sa;
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

            // Попытаемся обнаружить заголовки из первого загруженного файла
            var first = _loadedFiles.FirstOrDefault();
            if (first != null)
            {
                IEnumerable<string> headers = DetectHeadersFromFile(first.FilePath);
                dlg.PopulateHeaders(headers);
            }

            if (dlg.ShowDialog() == true)
            {
                _currentMapping = dlg.GetMapping();
                _mappingApplied = true;
                txtStatus.Text = "Сопоставление столбцов применено.";
            }
            UpdateButtonsState();
        }

        private IEnumerable<string> DetectHeadersFromFile(string path)
        {
            try
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".csv" || ext == ".txt")
                {
                    var line = File.ReadLines(path, Encoding.Default).FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(line)) return new string[0];
                    // Попробуем разделитель запятая; если нет — точка с запятой
                    var sep = line.Contains(',') ? ',' : (line.Contains(';') ? ';' : ',');
                    return line.Split(sep).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToArray();
                }
                else if (ext == ".xlsx" || ext == ".xls")
                {
                    // Попробуем прочитать первую строку через ClosedXML если доступно
                    try
                    {
                        using (var wb = new ClosedXML.Excel.XLWorkbook(path))
                        {
                            var ws = wb.Worksheets.First();
                            var firstRow = ws.FirstRowUsed();
                            if (firstRow != null)
                            {
                                var values = new List<string>();
                                foreach (var cell in firstRow.CellsUsed()) values.Add(cell.GetString());
                                return values.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return new string[0];
        }

        private void OnResolveNameConflictsClick(object sender, RoutedEventArgs e)
        {
            // Заглушка: открываем сообщение. Реализовать диалог для выбора действий (переименовать/пропустить/заменить)
            System.Windows.MessageBox.Show("Разрешение конфликтов не реализовано. (заглушка)", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void OnApplyFiltersClick(object sender, RoutedEventArgs e)
        {
            // Парсим файлы в «сырое» превью, затем применим выбранные фильтры к этому набору
            _previewAnswers.Clear();
            _allPreviewAnswers.Clear();
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
                        foreach (var sa in ParseFileRecords(lf.FilePath))
                        {
                            lock (_allPreviewAnswers)
                            {
                                _allPreviewAnswers.Add(sa);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show($"Ошибка при обработке {lf.FileName}: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }

                    processed++;
                    Dispatcher.Invoke(() =>
                    {
                        mainProgress.Value = (processed * 100) / total;
                    });
                }
            });

            Dispatcher.Invoke(() =>
            {
                // Заполняем фильтры
                PopulateFilterControls(false);
                
                // Применяем фильтры (показываем ВСЕ данные по умолчанию)
                ApplyCurrentFilters();

                mainProgress.Value = 100;
                txtStatus.Text = $"Загружено и обработано: {_allPreviewAnswers.Count} ответов";
                UpdateButtonsState();
            });
        }

        // Новый метод: агрегирует StudentAnswer по студент-тема
        private List<StudentAnswer> AggregateAnswers(List<StudentAnswer> allAnswers)
        {
            if (allAnswers.Count == 0)
                return new List<StudentAnswer>();

            var aggregated = new List<StudentAnswer>();

            // Группируем по студент-группа-дата-тема
            var grouped = allAnswers
                .GroupBy(a => new { a.StudentName, a.GroupName, a.TestDate, a.TopicName })
                .OrderBy(g => g.Key.TopicName)
                .ThenBy(g => g.Key.GroupName ?? "")
                .ThenBy(g => g.Key.StudentName)
                .ThenBy(g => g.Key.TestDate)
                .ToList();

            foreach (var group in grouped)
            {
                // Суммируем баллы и считаем процент правильных
                int totalScore = group.Sum(a => a.Score ?? 0);
                int correctCount = group.Count(a => a.Score == 1);
                int totalCount = group.Count();
                double percentCorrect = totalCount > 0 ? (double)correctCount / totalCount * 100 : 0;

                var aggregatedItem = new StudentAnswer
                {
                    StudentName = group.Key.StudentName,
                    GroupName = group.Key.GroupName,
                    TestDate = group.Key.TestDate,
                    TopicName = group.Key.TopicName,
                    QuestionName = $"[Сумма {group.Count()} вопросов]",
                    Score = totalScore,
                    AnswerText = $"{percentCorrect:F1}%"
                };
                aggregated.Add(aggregatedItem);
            }

            return aggregated;
        }

        private void PopulateFilterControls(bool preserveSelections = true)
        {
            // Сохраняем текущие выбранные значения, если требуется
            var selTopics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selStudents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTime? prevFrom = null, prevTo = null;
            if (preserveSelections)
            {
                selTopics.UnionWith(icTopics.Items.OfType<System.Windows.Controls.CheckBox>().Where(c => c.IsChecked == true).Select(c => c.Content?.ToString() ?? string.Empty));
                selGroups.UnionWith(icGroups.Items.OfType<System.Windows.Controls.CheckBox>().Where(c => c.IsChecked == true).Select(c => c.Content?.ToString() ?? string.Empty));
                selStudents.UnionWith(icStudents.Items.OfType<System.Windows.Controls.CheckBox>().Where(c => c.IsChecked == true).Select(c => c.Content?.ToString() ?? string.Empty));
                prevFrom = dpFrom.SelectedDate; prevTo = dpTo.SelectedDate;
            }

            icTopics.Items.Clear();
            icGroups.Items.Clear();
            icStudents.Items.Clear();

            // Используем ВСЕ ответы, а не агрегированные
            var topics = _allPreviewAnswers.Select(a => a.TopicName ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s);
            foreach (var t in topics)
            {
                var cb = new System.Windows.Controls.CheckBox { Content = t, Margin = new Thickness(2) };
                cb.Checked += (s, ev) => ApplyCurrentFilters();
                cb.Unchecked += (s, ev) => ApplyCurrentFilters();
                if (preserveSelections && selTopics.Contains(t)) cb.IsChecked = true;
                icTopics.Items.Add(cb);
            }

            var groups = _allPreviewAnswers.Select(a => a.GroupName ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s);
            foreach (var g in groups)
            {
                var cb = new System.Windows.Controls.CheckBox { Content = g, Margin = new Thickness(2) };
                cb.Checked += (s, ev) => ApplyCurrentFilters();
                cb.Unchecked += (s, ev) => ApplyCurrentFilters();
                if (preserveSelections && selGroups.Contains(g)) cb.IsChecked = true;
                icGroups.Items.Add(cb);
            }

            var students = _allPreviewAnswers.Select(a => a.StudentName ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s);
            foreach (var s in students)
            {
                var cb = new System.Windows.Controls.CheckBox { Content = s, Margin = new Thickness(2) };
                cb.Checked += (ss, ev) => ApplyCurrentFilters();
                cb.Unchecked += (ss, ev) => ApplyCurrentFilters();
                if (preserveSelections && selStudents.Contains(s)) cb.IsChecked = true;
                icStudents.Items.Add(cb);
            }

            // Даты
            var dates = _allPreviewAnswers.Where(a => a.TestDate.HasValue).Select(a => a.TestDate.Value.Date).Distinct().OrderBy(d => d).ToList();
            if (dates.Any())
            {
                dpFrom.IsEnabled = true; dpTo.IsEnabled = true;
                if (preserveSelections && prevFrom.HasValue && dates.Contains(prevFrom.Value)) dpFrom.SelectedDate = prevFrom;
                else dpFrom.SelectedDate = dates.First();
                if (preserveSelections && prevTo.HasValue && dates.Contains(prevTo.Value)) dpTo.SelectedDate = prevTo;
                else dpTo.SelectedDate = dates.Last();
            }
            else
            {
                dpFrom.IsEnabled = false; dpTo.IsEnabled = false;
                dpFrom.SelectedDate = null; dpTo.SelectedDate = null;
            }
        }

        private void ApplyCurrentFilters()
        {
            IEnumerable<StudentAnswer> query = _allPreviewAnswers;

            // темы
            var selTopics = icTopics.Items.OfType<System.Windows.Controls.CheckBox>().Where(c=>c.IsChecked==true).Select(c=>c.Content?.ToString()).Where(s=>!string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (selTopics.Any()) query = query.Where(a => selTopics.Contains(a.TopicName ?? string.Empty));

            // группы
            var selGroups = icGroups.Items.OfType<System.Windows.Controls.CheckBox>().Where(c=>c.IsChecked==true).Select(c=>c.Content?.ToString()).Where(s=>!string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (selGroups.Any()) query = query.Where(a => selGroups.Contains(a.GroupName ?? string.Empty));

            // студенты
            var selStudents = icStudents.Items.OfType<System.Windows.Controls.CheckBox>().Where(c=>c.IsChecked==true).Select(c=>c.Content?.ToString()).Where(s=>!string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (selStudents.Any()) query = query.Where(a => selStudents.Contains(a.StudentName ?? string.Empty));

            // даты
            if (dpFrom.IsEnabled && dpFrom.SelectedDate.HasValue && dpTo.SelectedDate.HasValue)
            {
                var from = dpFrom.SelectedDate.Value.Date;
                var to = dpTo.SelectedDate.Value.Date;
                query = query.Where(a => a.TestDate.HasValue && a.TestDate.Value.Date >= from && a.TestDate.Value.Date <= to);
            }

            var filteredList = query.ToList();

            // Обновляем детальный preview (без агрегации) - ВСЕ ответы
            _previewAnswers.Clear();
            foreach (var a in filteredList)
            {
                _previewAnswers.Add(a);
            }

            // Построим матричный превью (агрегированный по студент-тема)
            BuildMatrixPreview(filteredList);

            // Обновляем статистику по вопросам (детальную, без агрегации)
            _questionStats.Clear();
            var stats = filteredList.GroupBy(x => new { x.TopicName, x.QuestionName })
                .Select(g => new QuestionStat {
                    Topic = g.Key.TopicName,
                    Question = g.Key.QuestionName,
                    CorrectCount = g.Count(x=>x.Score==1),
                    TotalCount = g.Count()
                })
                .OrderBy(s=>s.Topic)
                .ThenBy(s=>s.Question);

            foreach (var s in stats) _questionStats.Add(s);

            txtStatus.Text = $"Готово. Превью: {_previewAnswers.Count} записей";
        }

        // Строит матричное представление: строки = (Тема, Студент), колонки = вопросы
        private void BuildMatrixPreview(IEnumerable<StudentAnswer> records)
        {
            try
            {
                var recs = records.ToList();
                if (!recs.Any())
                {
                    dgPreviewMatrix.ItemsSource = null;
                    return;
                }

                var questions = recs.Select(r => r.QuestionName ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();

                var table = new System.Data.DataTable();
                table.Columns.Add("Тема", typeof(string));
                table.Columns.Add("Студент", typeof(string));
                table.Columns.Add("Группа", typeof(string));
                table.Columns.Add("Дата", typeof(string));

                foreach (var q in questions) table.Columns.Add(q, typeof(int));

                table.Columns.Add("Сумма", typeof(int));

                // Агрегируем по студент-группа-дата-тема
                var groups = recs.GroupBy(r => new { r.TopicName, r.StudentName, r.GroupName, r.TestDate });
                foreach (var g in groups.OrderBy(x => x.Key.TopicName).ThenBy(x => x.Key.StudentName).ThenBy(x => x.Key.TestDate))
                {
                    var row = table.NewRow();
                    row["Тема"] = g.Key.TopicName ?? string.Empty;
                    row["Студент"] = g.Key.StudentName ?? string.Empty;
                    row["Группа"] = g.Key.GroupName ?? string.Empty;
                    row["Дата"] = g.Key.TestDate.HasValue ? g.Key.TestDate.Value.ToString("dd.MM.yyyy") : string.Empty;
                    
                    int rowSum = 0;
                    bool hasAnyData = false;
                    foreach (var q in questions)
                    {
                        var ans = g.FirstOrDefault(x => string.Equals(x.QuestionName ?? string.Empty, q, StringComparison.OrdinalIgnoreCase));
                        if (ans != null)
                        {
                            row[q] = ans.Score ?? 0;
                            rowSum += ans.Score ?? 0;
                            hasAnyData = true;
                        }
                        else
                            row[q] = DBNull.Value;
                    }
                    
                    // Добавляем строку только если есть хотя бы один ответ
                    if (hasAnyData)
                    {
                        row["Сумма"] = rowSum;
                        table.Rows.Add(row);
                    }
                }

                dgPreviewMatrix.ItemsSource = table.DefaultView;
            }
            catch { dgPreviewMatrix.ItemsSource = null; }
        }

        private void OnGenerateReportClick(object sender, RoutedEventArgs e)
        {
            if (_allPreviewAnswers.Count == 0)
            {
                System.Windows.MessageBox.Show("Нет данных для отчёта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Собираем ParsedFiles из StudentAnswer
            var topics = _allPreviewAnswers
                .Select(a => a.TopicName)
                .Distinct()
                .ToList();

            var parsedFiles = new List<TestReporter.Models.ParsedFile>();

            foreach (var topic in topics)
            {
                var topicAnswers = _allPreviewAnswers
                    .Where(a => a.TopicName == topic)
                    .ToList();

                var recordsDict = new Dictionary<(string, string, DateTime?), List<StudentAnswer>>();

                // Группируем по студенту-группе-дате
                foreach (var answer in topicAnswers)
                {
                    var key = (answer.StudentName, answer.GroupName ?? "", answer.TestDate);
                    if (!recordsDict.ContainsKey(key))
                        recordsDict[key] = new List<StudentAnswer>();
                    recordsDict[key].Add(answer);
                }

                var records = new List<TestReporter.Models.TestRecord>();
                var allQuestions = new HashSet<string>();

                foreach (var kvp in recordsDict)
                {
                    var (studentName, groupName, testDate) = kvp.Key;
                    var answers = kvp.Value;

                    var testRecord = new TestReporter.Models.TestRecord
                    {
                        FullName = studentName,
                        Group = string.IsNullOrWhiteSpace(groupName) ? null : groupName,
                        CreatedAt = testDate,
                        Topic = topic,
                        Answers = new List<StudentAnswer>(answers)
                    };
                    records.Add(testRecord);

                    foreach (var ans in answers)
                        allQuestions.Add(ans.QuestionName);
                }

                var questionInfos = allQuestions.Select(q => new TestReporter.Models.QuestionInfo
                {
                    Name = q,
                    Type = TestReporter.Models.QuestionType.Choice
                }).ToList();

                var parsedFile = new TestReporter.Models.ParsedFile
                {
                    Topic = topic,
                    Records = records,
                    Questions = questionInfos
                };
                parsedFiles.Add(parsedFile);
            }

            // Диалог сохранения
            var sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Filter = "Excel Workbook|*.xlsx";
            sfd.DefaultExt = ".xlsx";
            sfd.FileName = $"report_{DateTime.Now:yyyyMMdd_HHmmss}";

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var generator = new TestReporter.Parser.Reporting.ReportGenerator();
                    generator.GenerateReport(parsedFiles, sfd.FileName);
                    System.Windows.MessageBox.Show("Отчёт успешно создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка при создании отчёта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnCreateNewReportClick(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Filter = "Excel Workbook|*.xlsx";
            sfd.FileName = "NewReport.xlsx";
            if (sfd.ShowDialog() != true) return;

            try
            {
                using (var wb = new ClosedXML.Excel.XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Matrix");
                    ws.Cell(1, 1).Value = "Тема";
                    ws.Cell(1, 2).Value = "Студент";
                    ws.Cell(1, 3).Value = "Группа";

                    var ws2 = wb.Worksheets.Add("QuestionStats");
                    ws2.Cell(1, 1).Value = "Тема";
                    ws2.Cell(1, 2).Value = "Вопрос";
                    ws2.Cell(1, 3).Value = "Правильных";
                    ws2.Cell(1, 4).Value = "Всего";
                    ws2.Cell(1, 5).Value = "% правильных";

                    wb.SaveAs(sfd.FileName);
                }
                txtStatus.Text = $"Создан шаблон отчёта: {sfd.FileName}";
                System.Windows.MessageBox.Show($"Шаблон отчёта создан: {sfd.FileName}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при создании файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnUpdateReportClick(object sender, RoutedEventArgs e)
        {
            if (_allPreviewAnswers.Count == 0)
            {
                System.Windows.MessageBox.Show("Нет данных для обновления отчёта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Excel|*.xlsx;*.xls";
            if (ofd.ShowDialog() != true) return;

            var filePath = ofd.FileName;
            try
            {
                // backup
                var bak = filePath + ".bak";
                try { File.Copy(filePath, bak, true); } catch { }

                // Собираем ParsedFiles из текущих данных
                var topics = _allPreviewAnswers
                    .Select(a => a.TopicName)
                    .Distinct()
                    .ToList();

                var parsedFiles = new List<TestReporter.Models.ParsedFile>();

                foreach (var topic in topics)
                {
                    var topicAnswers = _allPreviewAnswers
                        .Where(a => a.TopicName == topic)
                        .ToList();

                    var recordsDict = new Dictionary<(string, string, DateTime?), List<StudentAnswer>>();

                    foreach (var answer in topicAnswers)
                    {
                        var key = (answer.StudentName, answer.GroupName ?? "", answer.TestDate);
                        if (!recordsDict.ContainsKey(key))
                            recordsDict[key] = new List<StudentAnswer>();
                        recordsDict[key].Add(answer);
                    }

                    var records = new List<TestReporter.Models.TestRecord>();
                    var allQuestions = new HashSet<string>();

                    foreach (var kvp in recordsDict)
                    {
                        var (studentName, groupName, testDate) = kvp.Key;
                        var answers = kvp.Value;

                        var testRecord = new TestReporter.Models.TestRecord
                        {
                            FullName = studentName,
                            Group = string.IsNullOrWhiteSpace(groupName) ? null : groupName,
                            CreatedAt = testDate,
                            Topic = topic,
                            Answers = new List<StudentAnswer>(answers)
                        };
                        records.Add(testRecord);

                        foreach (var ans in answers)
                            allQuestions.Add(ans.QuestionName);
                    }

                    var questionInfos = allQuestions.Select(q => new TestReporter.Models.QuestionInfo
                    {
                        Name = q,
                        Type = TestReporter.Models.QuestionType.Choice
                    }).ToList();

                    var parsedFile = new TestReporter.Models.ParsedFile
                    {
                        Topic = topic,
                        Records = records,
                        Questions = questionInfos
                    };
                    parsedFiles.Add(parsedFile);
                }

                // Перезаписываем отчёт
                var generator = new TestReporter.Parser.Reporting.ReportGenerator();
                generator.GenerateReport(parsedFiles, filePath);
                
                txtStatus.Text = $"Отчёт обновлён: {filePath}";
                System.Windows.MessageBox.Show($"Отчёт обновлён: {filePath}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка обновления: {ex.Message}\n\n{ex.StackTrace}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnExportPdfClick(object sender, RoutedEventArgs e)
        {
            if (_allPreviewAnswers.Count == 0)
            {
                System.Windows.MessageBox.Show("Нет данных для экспорта.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Filter = "PDF file|*.pdf";
            sfd.FileName = $"report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            if (sfd.ShowDialog() != true) return;

            try
            {
                // Собираем ParsedFiles из текущих данных
                var topics = _allPreviewAnswers
                    .Select(a => a.TopicName)
                    .Distinct()
                    .ToList();

                var parsedFiles = new List<TestReporter.Models.ParsedFile>();

                foreach (var topic in topics)
                {
                    var topicAnswers = _allPreviewAnswers
                        .Where(a => a.TopicName == topic)
                        .ToList();

                    var recordsDict = new Dictionary<(string, string, DateTime?), List<StudentAnswer>>();

                    foreach (var answer in topicAnswers)
                    {
                        var key = (answer.StudentName, answer.GroupName ?? "", answer.TestDate);
                        if (!recordsDict.ContainsKey(key))
                            recordsDict[key] = new List<StudentAnswer>();
                        recordsDict[key].Add(answer);
                    }

                    var records = new List<TestReporter.Models.TestRecord>();
                    var allQuestions = new HashSet<string>();

                    foreach (var kvp in recordsDict)
                    {
                        var (studentName, groupName, testDate) = kvp.Key;
                        var answers = kvp.Value;

                        var testRecord = new TestReporter.Models.TestRecord
                        {
                            FullName = studentName,
                            Group = string.IsNullOrWhiteSpace(groupName) ? null : groupName,
                            CreatedAt = testDate,
                            Topic = topic,
                            Answers = new List<StudentAnswer>(answers)
                        };
                        records.Add(testRecord);

                        foreach (var ans in answers)
                            allQuestions.Add(ans.QuestionName);
                    }

                    var questionInfos = allQuestions.Select(q => new TestReporter.Models.QuestionInfo
                    {
                        Name = q,
                        Type = TestReporter.Models.QuestionType.Choice
                    }).ToList();

                    var parsedFile = new TestReporter.Models.ParsedFile
                    {
                        Topic = topic,
                        Records = records,
                        Questions = questionInfos
                    };
                    parsedFiles.Add(parsedFile);
                }

                // Генерируем Excel временный файл для конвертации в PDF
                var tempExcelPath = Path.Combine(Path.GetTempPath(), $"temp_report_{Guid.NewGuid()}.xlsx");
                try
                {
                    var generator = new TestReporter.Parser.Reporting.ReportGenerator();
                    generator.GenerateReport(parsedFiles, tempExcelPath);

                    // Конвертируем Excel в PDF используя SelectPdf или другую библиотеку
                    // Пока используем простой текстовый PDF с данными из листов
                    ExportExcelToPdf(tempExcelPath, sfd.FileName);

                    txtStatus.Text = $"PDF экспорт сохранён: {sfd.FileName}";
                    System.Windows.MessageBox.Show($"PDF экспорт сохранён: {sfd.FileName}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                finally
                {
                    if (File.Exists(tempExcelPath))
                        File.Delete(tempExcelPath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при создании PDF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportExcelToPdf(string excelPath, string pdfPath)
        {
            using (var workbook = new XLWorkbook(excelPath))
            {
                using (var doc = new PdfSharpCore.Pdf.PdfDocument())
                {
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        ExportWorksheetToPdf(worksheet, doc);
                    }

                    doc.Save(pdfPath);
                }
            }
        }

        private void ExportWorksheetToPdf(IXLWorksheet worksheet, PdfSharpCore.Pdf.PdfDocument doc)
        {
            var page = doc.AddPage();
            page.Size = PdfSharpCore.PageSize.A4;
            var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);

            try
            {
                var headerFont = new PdfSharpCore.Drawing.XFont("Arial", 12, PdfSharpCore.Drawing.XFontStyle.Bold);
                var regularFont = new PdfSharpCore.Drawing.XFont("Arial", 9);
                double y = 40;
                double lineHeight = 14;
                double marginLeft = 20;
                double marginRight = 20;
                double usableWidth = page.Width - marginLeft - marginRight;
                double maxY = page.Height - 40;

                // Заголовок листа
                gfx.DrawString($"Лист: {worksheet.Name}", headerFont, PdfSharpCore.Drawing.XBrushes.Black,
                    new PdfSharpCore.Drawing.XRect(marginLeft, y, usableWidth, lineHeight), PdfSharpCore.Drawing.XStringFormats.TopLeft);
                y += lineHeight * 1.5;

                // Таблица
                var usedRows = worksheet.RowsUsed();
                foreach (var row in usedRows)
                {
                    var cells = row.CellsUsed();
                    var rowText = string.Join(" | ", cells.Select(c => {
                        var val = c.GetString();
                        return !string.IsNullOrEmpty(val) ? val.Replace('\n', ' ') : string.Empty;
                    }));                    // Форматируем текст для размера страницы
                    if (rowText.Length > 150)
                        rowText = rowText.Substring(0, 147) + "...";

                    var font = row.RowNumber() == 1 ? headerFont : regularFont;
                    gfx.DrawString(rowText, font, PdfSharpCore.Drawing.XBrushes.Black,
                        new PdfSharpCore.Drawing.XRect(marginLeft, y, usableWidth, lineHeight), PdfSharpCore.Drawing.XStringFormats.TopLeft);

                    y += lineHeight;

                    // Переход на новую страницу
                    if (y > maxY)
                    {
                        gfx.Dispose();
                        page = doc.AddPage();
                        page.Size = PdfSharpCore.PageSize.A4;
                        gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
                        y = 40;
                    }
                }
            }
            finally
            {
                gfx.Dispose();
            }
        }

        private void PrintExcelWorkbook(string excelPath, System.Windows.Controls.PrintDialog printDialog)
        {
            // Создаём FlowDocument для печати
            var doc = new System.Windows.Documents.FlowDocument();
            doc.PageHeight = printDialog.PrintableAreaHeight;
            doc.PageWidth = printDialog.PrintableAreaWidth;
            doc.PagePadding = new Thickness(20);

            using (var workbook = new XLWorkbook(excelPath))
            {
                foreach (var worksheet in workbook.Worksheets)
                {
                    // Заголовок листа
                    var heading = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Лист: {worksheet.Name}"))
                    {
                        FontSize = 14,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    doc.Blocks.Add(heading);

                    // Таблица
                    var table = new System.Windows.Documents.Table();
                    table.BorderThickness = new Thickness(0.5);
                    table.BorderBrush = System.Windows.Media.Brushes.Gray;

                    var usedRows = worksheet.RowsUsed().ToList();
                    if (usedRows.Any())
                    {
                        var firstRow = usedRows.First();
                        int colCount = firstRow.CellsUsed().Count();

                        for (int i = 0; i < colCount; i++)
                        {
                            table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(1, GridUnitType.Star) });
                        }

                        int rowNum = 0;
                        foreach (var row in usedRows)
                        {
                            var trg = new System.Windows.Documents.TableRowGroup();
                            var tr = new System.Windows.Documents.TableRow();

                            var cells = row.CellsUsed();
                            foreach (var cell in cells)
                            {
                                var para = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(cell.GetString() ?? ""))
                                {
                                    FontSize = rowNum == 0 ? 10 : 9,
                                    FontWeight = rowNum == 0 ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal
                                };
                                var tc = new System.Windows.Documents.TableCell(para)
                                {
                                    BorderThickness = new Thickness(0.5),
                                    BorderBrush = System.Windows.Media.Brushes.Gray,
                                    Padding = new Thickness(2)
                                };
                                tr.Cells.Add(tc);
                            }
                            trg.Rows.Add(tr);
                            table.RowGroups.Add(trg);
                            rowNum++;
                        }
                    }

                    doc.Blocks.Add(table);
                    doc.Blocks.Add(new System.Windows.Documents.Paragraph()); // Пустая строка между листами
                }
            }

            printDialog.PrintDocument(((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator, "Отчёт");
        }

        private void OnPrintClick(object sender, RoutedEventArgs e)
        {
            if (_allPreviewAnswers.Count == 0)
            {
                System.Windows.MessageBox.Show("Нет данных для печати.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Собираем ParsedFiles из текущих данных
                var topics = _allPreviewAnswers
                    .Select(a => a.TopicName)
                    .Distinct()
                    .ToList();

                var parsedFiles = new List<TestReporter.Models.ParsedFile>();

                foreach (var topic in topics)
                {
                    var topicAnswers = _allPreviewAnswers
                        .Where(a => a.TopicName == topic)
                        .ToList();

                    var recordsDict = new Dictionary<(string, string, DateTime?), List<StudentAnswer>>();

                    foreach (var answer in topicAnswers)
                    {
                        var key = (answer.StudentName, answer.GroupName ?? "", answer.TestDate);
                        if (!recordsDict.ContainsKey(key))
                            recordsDict[key] = new List<StudentAnswer>();
                        recordsDict[key].Add(answer);
                    }

                    var records = new List<TestReporter.Models.TestRecord>();
                    var allQuestions = new HashSet<string>();

                    foreach (var kvp in recordsDict)
                    {
                        var (studentName, groupName, testDate) = kvp.Key;
                        var answers = kvp.Value;

                        var testRecord = new TestReporter.Models.TestRecord
                        {
                            FullName = studentName,
                            Group = string.IsNullOrWhiteSpace(groupName) ? null : groupName,
                            CreatedAt = testDate,
                            Topic = topic,
                            Answers = new List<StudentAnswer>(answers)
                        };
                        records.Add(testRecord);

                        foreach (var ans in answers)
                            allQuestions.Add(ans.QuestionName);
                    }

                    var questionInfos = allQuestions.Select(q => new TestReporter.Models.QuestionInfo
                    {
                        Name = q,
                        Type = TestReporter.Models.QuestionType.Choice
                    }).ToList();

                    var parsedFile = new TestReporter.Models.ParsedFile
                    {
                        Topic = topic,
                        Records = records,
                        Questions = questionInfos
                    };
                    parsedFiles.Add(parsedFile);
                }

                // Генерируем временный Excel файл
                var tempExcelPath = Path.Combine(Path.GetTempPath(), $"temp_report_{Guid.NewGuid()}.xlsx");
                try
                {
                    var generator = new TestReporter.Parser.Reporting.ReportGenerator();
                    generator.GenerateReport(parsedFiles, tempExcelPath);

                    // Открываем диалог печати
                    var pd = new System.Windows.Controls.PrintDialog();
                    if (pd.ShowDialog() == true)
                    {
                        PrintExcelWorkbook(tempExcelPath, pd);
                        txtStatus.Text = "Документ отправлен на печать.";
                    }
                }
                finally
                {
                    if (File.Exists(tempExcelPath))
                        File.Delete(tempExcelPath);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при печати: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateButtonsState()
        {
            bool hasFiles = _loadedFiles.Any();
            btnColumnMap.IsEnabled = hasFiles;
            bool readyForReports = hasFiles;
            
            // toolbar buttons
            btnGenerate.IsEnabled = readyForReports;
            btnNewReport.IsEnabled = readyForReports;
            btnUpdateReport.IsEnabled = readyForReports;
            btnExportPdf.IsEnabled = readyForReports;
            btnPrint.IsEnabled = readyForReports;
            
            // right-panel buttons (mirror toolbar)
            try
            {
                if (btnGeneratePanel != null) btnGeneratePanel.IsEnabled = readyForReports;
                if (btnNewReportPanel != null) btnNewReportPanel.IsEnabled = readyForReports;
                if (btnUpdateReportPanel != null) btnUpdateReportPanel.IsEnabled = readyForReports;
                if (btnExportPdfPanel != null) btnExportPdfPanel.IsEnabled = readyForReports;
                if (btnPrintPanel != null) btnPrintPanel.IsEnabled = readyForReports;
            }
            catch { }
            
            btnApplyFilters.IsEnabled = hasFiles;
        }
    }
}
