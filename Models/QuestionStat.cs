using System;

namespace TestReporter.Models
{
    public class QuestionStat
    {
        public string Topic { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public double Percent => TotalCount > 0 ? (double)CorrectCount / TotalCount * 100 : 0;
    }
}