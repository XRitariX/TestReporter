using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestReporter.Models;

namespace TestReporter.Servise
{
    public class DataService
    {
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
