using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Models
{
    public class TestRecord
    {
        public string? Id { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Group { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public List<StudentAnswer> Answers { get; set; } = new();
        public int? TotalScore { get; set; }
        public int? MaxPossibleScore { get; set; }
        public string? Result { get; set; }
        public int ComputedScore => Answers.Where(a => a.Score.HasValue).Sum(a => a.Score!.Value);
    }
}

