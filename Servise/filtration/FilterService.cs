using System;
using System.Collections.Generic;
using System.Linq;
using TestReporter.Service.Filtration;

namespace TestReporter.Service.Filtration
{
    public class QuestionStat
    {
        public string Topic { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public double Percent { get; set; }
    }

    public class FilterService
    {
        // Фильтрация данных согласно критериям
        public IEnumerable<TestingRecord> FilterData(IEnumerable<TestingRecord> rawData, FilterCriteria criteria)
        {
            if (rawData == null) yield break;

            // Подготовка хэш-наборов для быстрой проверки
            HashSet<string>? topics = null, groups = null, students = null;
            if (criteria.SelectedTopics != null && criteria.SelectedTopics.Any())
                topics = new HashSet<string>(criteria.SelectedTopics, StringComparer.OrdinalIgnoreCase);
            if (criteria.ApplyGroupFilter && criteria.SelectedGroups != null && criteria.SelectedGroups.Any())
                groups = new HashSet<string>(criteria.SelectedGroups, StringComparer.OrdinalIgnoreCase);
            if (criteria.SelectedStudents != null && criteria.SelectedStudents.Any())
                students = new HashSet<string>(criteria.SelectedStudents, StringComparer.OrdinalIgnoreCase);

            // Используем IEnumerable + yield для ленивости и минимальных аллокаций
            foreach (var r in rawData)
            {
                // Topic filter
                if (topics != null && !topics.Contains(r.TopicName ?? string.Empty))
                    continue;

                // Group filter
                if (groups != null && criteria.ApplyGroupFilter)
                {
                    if (string.IsNullOrWhiteSpace(r.GroupName) || !groups.Contains(r.GroupName))
                        continue;
                }

                // Student filter
                if (students != null && !students.Contains(r.StudentName))
                    continue;

                // Date filter
                if (criteria.ApplyDateFilter && r.TestDate.HasValue)
                {
                    var d = r.TestDate.Value.Date;
                    if (criteria.DateFrom.HasValue && d < criteria.DateFrom.Value.Date) continue;
                    if (criteria.DateTo.HasValue && d > criteria.DateTo.Value.Date) continue;
                }
                // Если ApplyDateFilter=true, но r.TestDate == null, то по ТЗ: если столбец даты не сопоставлен — фильтр игнорируется.
                // Здесь считаем, что ApplyDateFilter означает, что столбец был сопоставлен; если у конкретной записи дата отсутствует, то её исключаем.
                if (criteria.ApplyDateFilter && !r.TestDate.HasValue)
                {
                    // исключаем записи без даты при активном фильтре (требование: фильтр по дате применяется если дата сопоставлена)
                    continue;
                }

                yield return r;
            }
        }

        // Формирование статистики по вопросам
        public IEnumerable<QuestionStat> BuildQuestionStats(IEnumerable<TestingRecord> filteredData)
        {
            if (filteredData == null) yield break;

            // Сбор пар (topic, question) -> агрегация
            // Используем Dictionary для эффективной агрегации
            var dict = new Dictionary<(string topic, string question), (int correct, int total)>(StringTupleComparer.Instance);

            foreach (var r in filteredData)
            {
                if (r.Questions == null) continue;

                foreach (var kv in r.Questions)
                {
                    var qName = kv.Key ?? string.Empty;
                    // По ТЗ: null трактуем как 0
                    int val = kv.Value ?? 0;

                    var key = (r.TopicName ?? string.Empty, qName);
                    if (!dict.TryGetValue(key, out var agg))
                    {
                        agg = (0, 0);
                    }
                    // Всего ответивших: учитываем как 1, даже если val==0 или null(интерпретирован как 0)
                    agg.total += 1;
                    if (val == 1) agg.correct += 1;
                    dict[key] = agg;
                }
            }

            // Проекция и сортировка: по теме(алфавит), по вопросу(алфавит)
            var result = dict.Select(kv => new QuestionStat
            {
                Topic = kv.Key.topic,
                Question = kv.Key.question,
                CorrectCount = kv.Value.correct,
                TotalCount = kv.Value.total,
                Percent = Math.Round(kv.Value.total == 0 ? 0 : (double)kv.Value.correct / kv.Value.total * 100.0, 1)
            })
            .OrderBy(x => x.Topic, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Question, StringComparer.OrdinalIgnoreCase);

            foreach (var item in result) yield return item;
        }

        // Вспомогательный компаратор для ключей (tuple) с ignore-case
        private class StringTupleComparer : IEqualityComparer<(string topic, string question)>
        {
            public static readonly StringTupleComparer Instance = new StringTupleComparer();
            public bool Equals((string topic, string question) x, (string topic, string question) y)
            {
                return string.Equals(x.topic, y.topic, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.question, y.question, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode((string topic, string question) obj)
            {
                unchecked
                {
                    int h1 = obj.topic == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.topic);
                    int h2 = obj.question == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.question);
                    return (h1 * 397) ^ h2;
                }
            }
        }
    }
}
