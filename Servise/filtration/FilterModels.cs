using System;
using System.Collections.Generic;

namespace TestReporter.Servise.Filtration
{
    // Сырая запись тестирования
    public class TestingRecord
    {
        public string StudentName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty; // may be empty if not mapped
        public DateTime? TestDate { get; set; }
        public string TopicName { get; set; } = string.Empty;

        // Questions: key = question name, value = score (0/1), null if absent
        // Интерпретация: null будет трактоваться как 0 (по ТЗ)
        public IReadOnlyDictionary<string, int?> Questions { get; init; } = new Dictionary<string, int?>();
    }

    // Критерии фильтрации
    public class FilterCriteria
    {
        // Date range (inclusive). If ApplyDateFilter=false, date filtering is ignored.
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool ApplyDateFilter { get; set; } = false; // указывает, была ли сопоставлена колонка Дата

        // Multi-select filters. If null or empty -> не применяется
        public List<string>? SelectedTopics { get; set; }
        public List<string>? SelectedGroups { get; set; }
        public List<string>? SelectedStudents { get; set; }

        // Indicates whether Group column exists / was mapped. If false, group filter ignored.
        public bool ApplyGroupFilter { get; set; } = false;
    }
}