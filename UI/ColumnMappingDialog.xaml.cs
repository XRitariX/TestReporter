using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace TestReporter
{
    public partial class ColumnMappingDialog : Window
    {
        private List<string> _headers = new List<string>();

        public ColumnMappingDialog()
        {
            InitializeComponent();
        }

        // Установить обнаруженные заголовки и заполнить контролы
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

        private void OnSaveTemplateClick(object sender, RoutedEventArgs e)
        {
            // TODO: Сохранить текущие сопоставления в файл-шаблон (JSON/XML)
            System.Windows.MessageBox.Show("Шаблон сохранён (заглушка)", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnApplyMappingClick(object sender, RoutedEventArgs e)
        {
            // TODO: Проверить, что ФИО выбрано
            if (cbNameColumn.SelectedItem == null)
            {
                System.Windows.MessageBox.Show("Необходимо выбрать столбец для ФИО.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // TODO: собрать выбранные колонки вопросов
            // Сохраняем настройки в модель приложения (заглушка)
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
