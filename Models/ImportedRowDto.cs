using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Models
{
    /// <summary>
    /// Модель данных, представляющая одну строку импортированных или существующих данных.
    /// </summary>
    public class ImportedRowDto
    {
        public string Student { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public string Theme { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}
