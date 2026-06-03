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
        private bool _filtersPopulated = false;
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

                    // detect meta columns
                    int idxName = -1, idxGroup = -1, idxDate = -1;
                    var questionIndices = new List<int>();

                    for (int i = 0; i < headers.Count; i++)
                    {
                        var h = headers[i].ToLowerInvariant();
                        // If mapping provided, use mapping names to determine indices
                        if (_currentMapping != null)
                        {
                            var map = _currentMapping;
                        if (!string.IsNullOrWhiteSpace(map.NameColumn) && string.Equals(headers[i], map.NameColumn, StringComparison.OrdinalIgnoreCase)) idxName = i;
                            else if (!string.IsNullOrWhiteSpace(map.GroupColumnName) && string.Equals(headers[i], map.GroupColumnName, StringComparison.OrdinalIgnoreCase)) idxGroup = i;
                            else if (!string.IsNullOrWhiteSpace(map.DateColumnName) && string.Equals(headers[i], map.DateColumnName, StringComparison.OrdinalIgnoreCase)) idxDate = i;
                            else if (map.QuestionColumns != null && map.QuestionColumns.Any(q => string.Equals(q, headers[i], StringComparison.OrdinalIgnoreCase)))
                            {
                                questionIndices.Add(i);
                            }
                            else
                            {
                                // fallback: heuristic
                                if (h.Contains("фам") || h.Contains("имя") || h.Contains("фио") || h.Contains("your") || h.Contains("name")) idxName = i;
                                else if (h.Contains("групп") || h.Contains("group")) idxGroup = i;
                                else if (h.Contains("время") || h.Contains("дата") || h.Contains("time") || h.Contains("date")) idxDate = i;
                                else if (h.Contains("результ") || h.Contains("итог") || h.Contains("всего") || h.Contains("набрано")) { }
                                else questionIndices.Add(i);
                            }
                        }
                        else
                        {
                            if (h.Contains("фам") || h.Contains("имя") || h.Contains("фио") || h.Contains("your") || h.Contains("name")) idxName = i;
                            else if (h.Contains("групп") || h.Contains("group")) idxGroup = i;
                            else if (h.Contains("время") || h.Contains("дата") || h.Contains("time") || h.Contains("date")) idxDate = i;
                            else if (h.Contains("результ") || h.Contains("итог") || h.Contains("всего" ) || h.Contains("набрано"))
                            {
                                // total/result columns - ignore as question columns
                            }
                            else
                            {
                                // treat as question column candidate
                                questionIndices.Add(i);
                            }
                        }
                    }

                    // iterate data rows
                    foreach (var row in ws.RowsUsed().Skip(1))
                    {
                        var cells = row.Cells(1, headers.Count).ToList();
                        var studentName = idxName >= 0 && idxName < cells.Count ? cells[idxName].GetString() : string.Empty;
                        var groupName = idxGroup >= 0 && idxGroup < cells.Count ? cells[idxGroup].GetString() : string.Empty;
                        DateTime? testDate = null;
                        if (idxDate >= 0 && idxDate < cells.Count)
                        {
                            var s = cells[idxDate].GetString();
                            if (DateTime.TryParse(s, out var dt)) testDate = dt;
                        }

                        foreach (var qi in questionIndices)
                        {
                            string qName = headers[qi];
                            int score = 0;
                            if (qi < cells.Count)
                            {
                                var str = cells[qi].GetString();
                                if (!int.TryParse(str, out score))
                                {
                                    // check for digits in string
                                    var digits = new string(str.Where(char.IsDigit).ToArray());
                                    if (!int.TryParse(digits, out score)) score = 0;
                                }
                            }

                            var sa = new StudentAnswer
                            {
                                StudentName = string.IsNullOrWhiteSpace(studentName) ? Path.GetFileNameWithoutExtension(path) : studentName,
                                TopicName = Path.GetFileNameWithoutExtension(path),
                                QuestionName = qName,
                                Score = score,
                                GroupName = groupName ?? string.Empty,
                                TestDate = testDate
                            };
                            yield return sa;
                        }
                    }
                }
            }
            else
            {
                // csv/txt
                string[] lines;
                try { lines = File.ReadAllLines(path, Encoding.Default); } catch { yield break; }
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
                    if (_currentMapping != null)
                    {
                        var map = _currentMapping;
                        if (!string.IsNullOrWhiteSpace(map.NameColumn) && string.Equals(raw, map.NameColumn, StringComparison.OrdinalIgnoreCase)) idxName = i;
                        else if (!string.IsNullOrWhiteSpace(map.GroupColumnName) && string.Equals(raw, map.GroupColumnName, StringComparison.OrdinalIgnoreCase)) idxGroup = i;
                        else if (!string.IsNullOrWhiteSpace(map.DateColumnName) && string.Equals(raw, map.DateColumnName, StringComparison.OrdinalIgnoreCase)) idxDate = i;
                        else if (map.QuestionColumns != null && map.QuestionColumns.Any(q => string.Equals(q, raw, StringComparison.OrdinalIgnoreCase))) questionIndices.Add(i);
                        else
                        {
                            if (h.Contains("фам") || h.Contains("имя") || h.Contains("фио") || h.Contains("name")) idxName = i;
                            else if (h.Contains("групп") || h.Contains("group")) idxGroup = i;
                            else if (h.Contains("время") || h.Contains("дата") || h.Contains("time") || h.Contains("date")) idxDate = i;
                            else if (h.Contains("результ") || h.Contains("итог") || h.Contains("всего") || h.Contains("набрано")) { }
                            else questionIndices.Add(i);
                        }
                    }
                    else
                    {
                        if (h.Contains("фам") || h.Contains("имя") || h.Contains("фио") || h.Contains("name")) idxName = i;
                        else if (h.Contains("групп") || h.Contains("group")) idxGroup = i;
                        else if (h.Contains("время") || h.Contains("дата") || h.Contains("time") || h.Contains("date")) idxDate = i;
                        else if (h.Contains("результ") || h.Contains("итог") || h.Contains("всего") || h.Contains("набрано")) { }
                        else questionIndices.Add(i);
                    }
                }

                for (int r = 1; r < lines.Length; r++)
                {
                    var parts = lines[r].Split(sep).Select(s => s.Trim()).ToList();
                    var studentName = idxName >= 0 && idxName < parts.Count ? parts[idxName] : string.Empty;
                    var groupName = idxGroup >= 0 && idxGroup < parts.Count ? parts[idxGroup] : string.Empty;
                    DateTime? testDate = null;
                    if (idxDate >= 0 && idxDate < parts.Count && DateTime.TryParse(parts[idxDate], out var dt)) testDate = dt;

                    foreach (var qi in questionIndices)
                    {
                        var qName = qi < headers.Count ? headers[qi] : $"Q{qi}";
                        int score = 0;
                        if (qi < parts.Count)
                        {
                            var s = parts[qi];
                            if (!int.TryParse(s, out score))
                            {
                                var digits = new string(s.Where(char.IsDigit).ToArray());
                                if (!int.TryParse(digits, out score)) score = 0;
                            }
                        }

                        yield return new StudentAnswer
                        {
                            StudentName = string.IsNullOrWhiteSpace(studentName) ? Path.GetFileNameWithoutExtension(path) : studentName,
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
            // Attempt to detect headers from first loaded file
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
                    catch { }
                    processed++;
                    Dispatcher.Invoke(() => mainProgress.Value = (int)((double)processed / Math.Max(1, total) * 100));
                }
            });

            // После парсинга обновляем контролы фильтрации и показываем отфильтрованные данные.
            // Первый раз заполняем без попытки сохранить старые выборы, потом сохраняем при повторных парсингах.
            Dispatcher.Invoke(() =>
            {
                PopulateFilterControls(preserveSelections: _filtersPopulated);
                ApplyCurrentFilters();
                _filtersPopulated = true;
            });
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

            // Обновляем preview collection
            _previewAnswers.Clear();
            foreach (var a in query)
            {
                _previewAnswers.Add(a);
            }

            // Построим матричный превью
            BuildMatrixPreview(query);

            // Обновляем статистику по вопросам
            _questionStats.Clear();
            var stats = _previewAnswers.GroupBy(x => new { x.TopicName, x.QuestionName })
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

                foreach (var q in questions) table.Columns.Add(q, typeof(string));

                var groups = recs.GroupBy(r => new { r.TopicName, r.StudentName, r.GroupName });
                foreach (var g in groups.OrderBy(x => x.Key.TopicName).ThenBy(x => x.Key.StudentName))
                {
                    var row = table.NewRow();
                    row["Тема"] = g.Key.TopicName ?? string.Empty;
                    row["Студент"] = g.Key.StudentName ?? string.Empty;
                    row["Группа"] = g.Key.GroupName ?? string.Empty;
                    foreach (var q in questions)
                    {
                        var ans = g.FirstOrDefault(x => string.Equals(x.QuestionName ?? string.Empty, q, StringComparison.OrdinalIgnoreCase));
                        if (ans != null) row[q] = ans.Score.ToString();
                        else row[q] = string.Empty;
                    }
                    table.Rows.Add(row);
                }

                dgPreviewMatrix.ItemsSource = table.DefaultView;
            }
            catch { dgPreviewMatrix.ItemsSource = null; }
        }

        private async void OnGenerateReportClick(object sender, RoutedEventArgs e)
        {
            if (!_loadedFiles.Any())
            {
                System.Windows.MessageBox.Show("Нужно загрузить файлы прежде чем формировать отчёт.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Filter = "Excel Workbook|*.xlsx";
            sfd.FileName = "Report.xlsx";
            if (sfd.ShowDialog() != true) return;

            txtStatus.Text = "Генерация отчёта...";
            mainProgress.Value = 0;
            IsEnabled = false;

            try
            {
                await Task.Run(() =>
                {
                    // write workbook
                    GenerateReportFile(sfd.FileName);
                    Dispatcher.Invoke(() => mainProgress.Value = 100);
                });

                txtStatus.Text = $"Отчёт сохранён: {sfd.FileName}";
                System.Windows.MessageBox.Show($"Отчёт сохранён: {sfd.FileName}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при генерации отчёта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Ошибка генерации отчёта";
            }
            finally
            {
                IsEnabled = true;
                mainProgress.Value = 0;
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
            var ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "Excel|*.xlsx;*.xls";
            if (ofd.ShowDialog() != true) return;

            var file = ofd.FileName;
            try
            {
                // backup
                var bak = file + ".bak";
                try { File.Copy(file, bak, true); } catch { }
                GenerateReportFile(file);
                txtStatus.Text = $"Отчёт обновлён: {file}";
                System.Windows.MessageBox.Show($"Отчёт обновлён: {file}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnExportPdfClick(object sender, RoutedEventArgs e)
        {
            // Экспорт в PDF через PdfSharpCore: формируем простой документ с таблицей
            var sfd = new Microsoft.Win32.SaveFileDialog();
            sfd.Filter = "PDF file|*.pdf";
            sfd.FileName = "Report.pdf";
            if (sfd.ShowDialog() != true) return;

            try
            {
                var matrix = new List<string[]>();
                if (dgPreviewMatrix.ItemsSource is System.Data.DataView dv)
                {
                    var cols = dv.Table.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToArray();
                    matrix.Add(cols);
                    foreach (System.Data.DataRowView drv in dv)
                    {
                        matrix.Add(cols.Select(c => drv.Row[c]?.ToString() ?? string.Empty).ToArray());
                    }
                }
                else
                {
                    matrix.Add(new[] { "Тема", "Студент", "Вопрос", "Балл", "Группа", "Дата" });
                    foreach (var r in _previewAnswers)
                        matrix.Add(new[] { r.TopicName ?? string.Empty, r.StudentName ?? string.Empty, r.QuestionName ?? string.Empty, r.Score.ToString(), r.GroupName ?? string.Empty, r.TestDate?.ToString("yyyy-MM-dd") ?? string.Empty });
                }

                // Создадим PDF документ
                using (var doc = new PdfSharpCore.Pdf.PdfDocument())
                {
                    var page = doc.AddPage();
                    page.Size = PdfSharpCore.PageSize.A4;
                    var gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
                    try
                    {
                        var font = new PdfSharpCore.Drawing.XFont("Arial", 10);
                        double y = 40;
                        double lineHeight = 16;
                        double marginLeft = 40;
                        double marginRight = 40;
                        double usableWidth = page.Width - marginLeft - marginRight;

                        foreach (var row in matrix)
                        {
                            string line = string.Join("  ", row.Select(c => c?.Replace('\n', ' ') ?? string.Empty));
                            // Draw using TopLeft alignment into a rectangle to avoid baseline errors
                            gfx.DrawString(line, font, PdfSharpCore.Drawing.XBrushes.Black, new PdfSharpCore.Drawing.XRect(marginLeft, y, usableWidth, lineHeight), PdfSharpCore.Drawing.XStringFormats.TopLeft);
                            y += lineHeight;
                            if (y > page.Height - 40)
                            {
                                // start a new page
                                gfx.Dispose();
                                page = doc.AddPage();
                                page.Size = PdfSharpCore.PageSize.A4;
                                gfx = PdfSharpCore.Drawing.XGraphics.FromPdfPage(page);
                                y = 40;
                            }
                        }
                    }
                    finally { gfx.Dispose(); }

                    using (var ms = new MemoryStream())
                    {
                        doc.Save(ms);
                        File.WriteAllBytes(sfd.FileName, ms.ToArray());
                    }
                }

                txtStatus.Text = $"PDF экспорт сохранён: {sfd.FileName}";
                System.Windows.MessageBox.Show($"PDF экспорт сохранён: {sfd.FileName}", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при создании PDF: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnPrintClick(object sender, RoutedEventArgs e)
        {
            var pd = new System.Windows.Controls.PrintDialog();
            if (pd.ShowDialog() == true)
            {
                // печатаем текущую вкладку (матрицу, если активна)
                var item = tabPreview.SelectedItem as TabItem;
                if (item != null && item.Header != null && item.Header.ToString().Contains("Матрич"))
                {
                    pd.PrintVisual(dgPreviewMatrix, "Печать матрицы");
                }
                else
                {
                    pd.PrintVisual(dgPreviewDetail, "Печать превью");
                }
                txtStatus.Text = "Документ отправлен на печать.";
            }
        }

        // Вспомогательный метод: формирует excel-файл с матрицей и статистикой
        private void GenerateReportFile(string filePath)
        {
            // собираем текущие данные из _previewAnswers и _questionStats
            var recs = _previewAnswers.ToList();
            var questions = recs.Select(r => r.QuestionName ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();

            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Matrix");
                int col = 1;
                ws.Cell(1, col++).Value = "Тема";
                ws.Cell(1, col++).Value = "Студент";
                ws.Cell(1, col++).Value = "Группа";
                foreach (var q in questions)
                {
                    ws.Cell(1, col++).Value = q;
                }

                var groups = recs.GroupBy(r => new { r.TopicName, r.StudentName, r.GroupName }).OrderBy(g => g.Key.TopicName).ThenBy(g => g.Key.StudentName).ToList();
                int row = 2;
                foreach (var g in groups)
                {
                    col = 1;
                    ws.Cell(row, col++).Value = g.Key.TopicName ?? string.Empty;
                    ws.Cell(row, col++).Value = g.Key.StudentName ?? string.Empty;
                    ws.Cell(row, col++).Value = g.Key.GroupName ?? string.Empty;
                    foreach (var q in questions)
                    {
                        var ans = g.FirstOrDefault(x => string.Equals(x.QuestionName ?? string.Empty, q, StringComparison.OrdinalIgnoreCase));
                        ws.Cell(row, col++).Value = ans != null ? ans.Score : (int?)null;
                    }
                    row++;
                }

                var ws2 = wb.Worksheets.Add("QuestionStats");
                ws2.Cell(1, 1).Value = "Тема";
                ws2.Cell(1, 2).Value = "Вопрос";
                ws2.Cell(1, 3).Value = "Правильных";
                ws2.Cell(1, 4).Value = "Всего";
                ws2.Cell(1, 5).Value = "% правильных";
                int r2 = 2;
                foreach (var s in _questionStats)
                {
                    ws2.Cell(r2, 1).Value = s.Topic;
                    ws2.Cell(r2, 2).Value = s.Question;
                    ws2.Cell(r2, 3).Value = s.CorrectCount;
                    ws2.Cell(r2, 4).Value = s.TotalCount;
                    ws2.Cell(r2, 5).Value = Math.Round(s.Percent, 2);
                    r2++;
                }

                wb.SaveAs(filePath);
            }
        }

        // Простая реализация PDF: создаёт текстовый PDF через поток (очень базово).
        // Для полноценного PDF лучше подключить библиотеку (PdfSharp/MigraDoc или iText7).
        private void GeneratePdfFile(string path, List<string> matrixLines, List<string> statsLines)
        {
            // Попытка создать PDF без сторонних пакетов — упакуем текст в формат PDF минимально.
            // Это не полноценный PDF — но создаёт файл, совместимый с простейшим viewer.
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs, Encoding.UTF8))
            {
                // Заголовок PDF (очень минимальный, не соответствует стандарту полностью).
                var content = new StringBuilder();
                content.AppendLine("Matrix:");
                foreach (var l in matrixLines) content.AppendLine(l);
                content.AppendLine();
                content.AppendLine("Stats:");
                foreach (var l in statsLines) content.AppendLine(l);

                var bytes = Encoding.UTF8.GetBytes(content.ToString());
                bw.Write(bytes);
            }
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
            bool readyForReports = hasFiles; // allow report actions when files are present
            // toolbar buttons
            btnGenerate.IsEnabled = readyForReports;
            btnNewReport.IsEnabled = readyForReports;
            btnUpdateReport.IsEnabled = readyForReports;
            btnExportPdf.IsEnabled = readyForReports;
            btnPrint.IsEnabled = readyForReports;
            // right-panel buttons (mirror toolbar) - update if present
            try
            {
                if (btnGeneratePanel != null) btnGeneratePanel.IsEnabled = readyForReports;
                if (btnNewReportPanel != null) btnNewReportPanel.IsEnabled = readyForReports;
                if (btnUpdateReportPanel != null) btnUpdateReportPanel.IsEnabled = readyForReports;
                if (btnExportPdfPanel != null) btnExportPdfPanel.IsEnabled = readyForReports;
                if (btnPrintPanel != null) btnPrintPanel.IsEnabled = readyForReports;
            }
            catch { }
            // Разрешаем предпросмотр/применение фильтров даже до сопоставления столбцов,
            // чтобы пользователь мог увидеть предварительный результат парсинга.
            btnApplyFilters.IsEnabled = hasFiles;
        }
    }
}
