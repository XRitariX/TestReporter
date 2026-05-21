using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TestReporter.Models;
using TestReporter.Servise;

namespace TestReporter
{
    public partial class MainWindow : Window
    {
        private DataService _dataService = new DataService();

        public MainWindow()
        {
            InitializeComponent();
        }

        public IEnumerable<DetailRecord> GetUniqueNewRecords(
     IEnumerable<DetailRecord> existingRecords,
     IEnumerable<DetailRecord> newRecords)
        {
            var existingKeys = new HashSet<(string Student, DateTime? Date, string Theme, string Question)>(
                existingRecords.Select(r => (r.Student, r.Date, r.Theme, r.Question))
            );

            return newRecords.Where(r =>
                !existingKeys.Contains((r.Student, r.Date, r.Theme, r.Question))
            );
        }
    }
}