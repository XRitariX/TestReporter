using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Models
{
    internal class FilterOptions
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<string> SelectedThemes { get; set; } = new();
        public List<string> SelectedGroups { get; set; } = new();
        public List<string> SelectedStudents { get; set; } = new();

        public bool IsDateExist { get; set; } = false;

        public void Reset()
        {
            StartDate = null;
            EndDate = null;
            SelectedThemes.Clear();
            SelectedGroups.Clear();
            SelectedStudents.Clear();
        }
        public bool FilterExist => StartDate.HasValue || EndDate.HasValue || SelectedThemes.Any() || SelectedGroups.Any() || SelectedStudents.Any();

    }
}

