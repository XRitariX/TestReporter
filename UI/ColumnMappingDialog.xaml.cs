using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.IO;
using System.Text.Json;

namespace TestReporter
{
    public partial class ColumnMappingDialog : Window
    {
        private List<string> _headers = new List<string>();
        private static readonly string TemplatesDir = System.IO.Path.Combine(AppContext.BaseDirectory ?? ".", "MappingTemplates");
        private const string TemplateExtension = ".json";

        public ColumnMappingDialog()
        {
            InitializeComponent();
            LoadTemplateList();
        }

        private void RefreshTemplateCombo()
        {
            try
            {
                var files = Directory.Exists(TemplatesDir) ? Directory.GetFiles(TemplatesDir, "*" + TemplateExtension) : new string[0];
                cbTemplates.ItemsSource = files.Select(f => Path.GetFileName(f)).ToList();
            }
            catch { cbTemplates.ItemsSource = null; }
        }

        // Установить обнаруженные заголовки и заполнить контролы.
        public void PopulateHeaders(IEnumerable<string> headers)
        {
            _headers = headers?.ToList() ?? new List<string>();
            lbDetectedHeaders.ItemsSource = _headers;

            cbNameColumn.ItemsSource = _headers;
            cbGroupColumn.ItemsSource = _headers;
            cbDateColumn.ItemsSource = _headers;

            icQuestionColumns.Items.Clear();
            foreach (var h in _headers)
            {
                var cb = new System.Windows.Controls.CheckBox { Content = h, Margin = new System.Windows.Thickness(2) };
                icQuestionColumns.Items.Add(cb);
            }
        }

        // Возвращает выбранное имя столбца для ФИО
        public string? SelectedNameColumn => cbNameColumn.SelectedItem as string;
        public string? SelectedGroupColumn => cbGroupColumn.SelectedItem as string;
        public string? SelectedDateColumn => cbDateColumn.SelectedItem as string;
        public IEnumerable<string> SelectedQuestionColumns => icQuestionColumns.Items.OfType<System.Windows.Controls.CheckBox>().Where(c => c.IsChecked == true).Select(c => c.Content?.ToString() ?? string.Empty);

        public Models.ColumnMapping GetMapping()
        {
            return new Models.ColumnMapping
            {
                NameColumn = SelectedNameColumn,
                GroupColumnName = SelectedGroupColumn,
                DateColumnName = SelectedDateColumn,
                QuestionColumns = SelectedQuestionColumns.ToList()
            };
        }

        private void LoadTemplateList()
        {
            try
            {
                if (!Directory.Exists(TemplatesDir))
                    Directory.CreateDirectory(TemplatesDir);
                RefreshTemplateCombo();
            }
            catch { }
        }

        private void OnLoadTemplateClick(object sender, RoutedEventArgs e)
        {
            string? file = null;
            if (cbTemplates.SelectedItem != null)
            {
                var sel = cbTemplates.SelectedItem.ToString();
                if (!string.IsNullOrWhiteSpace(sel))
                {
                    var inTemplates = Path.Combine(TemplatesDir, sel);
                    if (File.Exists(inTemplates)) file = inTemplates;
                    else if (File.Exists(sel)) file = sel; // maybe an absolute path was stored
                }
            }

            if (file == null)
            {
                var ofd = new System.Windows.Forms.OpenFileDialog();
                ofd.InitialDirectory = TemplatesDir;
                ofd.Filter = "JSON Template|*.json";
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                file = ofd.FileName;
            }

            try
            {
                var json = File.ReadAllText(file);
                var mapping = System.Text.Json.JsonSerializer.Deserialize<Models.ColumnMapping>(json);
                if (mapping == null) return;

                // Apply template: set combo selections and checkboxes (case-insensitive)
                if (!string.IsNullOrWhiteSpace(mapping.NameColumn) && _headers.Any(h => string.Equals(h, mapping.NameColumn, StringComparison.OrdinalIgnoreCase))) cbNameColumn.SelectedItem = _headers.FirstOrDefault(h => string.Equals(h, mapping.NameColumn, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(mapping.GroupColumnName) && _headers.Any(h => string.Equals(h, mapping.GroupColumnName, StringComparison.OrdinalIgnoreCase))) cbGroupColumn.SelectedItem = _headers.FirstOrDefault(h => string.Equals(h, mapping.GroupColumnName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(mapping.DateColumnName) && _headers.Any(h => string.Equals(h, mapping.DateColumnName, StringComparison.OrdinalIgnoreCase))) cbDateColumn.SelectedItem = _headers.FirstOrDefault(h => string.Equals(h, mapping.DateColumnName, StringComparison.OrdinalIgnoreCase));
                if (mapping.QuestionColumns != null)
                {
                    foreach (var ch in icQuestionColumns.Items.OfType<System.Windows.Controls.CheckBox>())
                    {
                        var txt = ch.Content?.ToString() ?? string.Empty;
                        ch.IsChecked = mapping.QuestionColumns.Any(q => string.Equals(q, txt, StringComparison.OrdinalIgnoreCase));
                    }
                }
                System.Windows.MessageBox.Show("Шаблон применён.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка загрузки шаблона: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnSaveTemplateClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "JSON Template|*.json";
            dialog.InitialDirectory = TemplatesDir;
            dialog.DefaultExt = ".json";

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var mapping = GetMapping();
                    var json = JsonSerializer.Serialize(mapping, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dialog.FileName, json);
                        // Обновим список шаблонов, если сохранили в папку шаблонов
                        try { RefreshTemplateCombo(); } catch { }
                    System.Windows.MessageBox.Show("Шаблон сохранён успешно.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка при сохранении шаблона: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnApplyMappingClick(object sender, RoutedEventArgs e)
        {
            // Проверить, что ФИО выбрано
            if (cbNameColumn.SelectedItem == null)
            {
                System.Windows.MessageBox.Show("Необходимо выбрать столбец для ФИО.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверить, что хотя бы один вопрос выбран
            var selectedQuestions = SelectedQuestionColumns.Count();
            if (selectedQuestions == 0)
            {
                System.Windows.MessageBox.Show("Необходимо выбрать хотя бы один столбец для вопросов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            this.DialogResult = true;
            this.Close();
        }

        private void OnCancelMappingClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
