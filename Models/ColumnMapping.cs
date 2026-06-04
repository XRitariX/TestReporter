using System.Collections.Generic;

namespace TestReporter.Models
{
    public class ColumnMapping
    {
        public string? NameColumn { get; set; }
        public string? GroupColumnName { get; set; }
        public string? DateColumnName { get; set; }
        public List<string>? QuestionColumns { get; set; }

        public int IdColumn { get; set; } = -1;
        public int DateColumn { get; set; } = -1;
        public int GroupColumn { get; set; } = -1;
        public int FullNameColumn { get; set; } = -1;
        public int TotalScoreColumn { get; set; } = -1;
        public int MaxScoreColumn { get; set; } = -1;
        public int ResultColumn { get; set; } = -1;
        public List<QuestionInfo> Questions { get; set; } = new();

        public bool IsValid => (FullNameColumn >= 0 || !string.IsNullOrWhiteSpace(NameColumn)) && ((Questions != null && Questions.Count > 0) || (QuestionColumns != null && QuestionColumns.Count > 0));
    }
}

