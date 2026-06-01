using System.Windows;

namespace TestReporter
{
    public partial class ColumnMappingDialog : Window
    {
        public ColumnMappingDialog()
        {
            InitializeComponent();
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