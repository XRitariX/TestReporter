using TestReporter.Models;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Parser.Utils;

public static class ColumnMapper
{
    private static readonly string[] IdKeywords = new string[] { "id", "ид", "идентификатор", "в№" };
    private static readonly string[] DateKeywords = new string[] { "время создания", "время", "дата", "time", "date", "created" };
    private static readonly string[] GroupKeywords = new string[] { "номер группы", "группа", "group" };
    private static readonly string[] FullNameKeywords = new string[] { "ваши фамилия имя отчество", "фамилия имя отчество", "фио", "фамилия", "имя", "отчество", "студент", "fio", "full name" };
    private static readonly string[] TotalScoreKeywords = new string[] { "набрано баллов", "набрано", "total", "sum", "сумма" };
    private static readonly string[] MaxScoreKeywords = new string[] { "всего баллов", "max", "maximum", "possible" };
    private static readonly string[] ResultKeywords = new string[] { "результат теста", "результат", "result", "итог" };

    private const string ScoreSuffix = "/ Баллы";

    public static bool DebugMode { get; set; } = false;

    public static ColumnMapping BuildMapping(List<string> headers)
    {
        var mapping = new ColumnMapping();
        var normalizedHeaders = headers
            .Select((h, i) => new { Index = i, Original = h, Normalized = Normalize(h) })
            .ToList();

        var scoreColumns = new HashSet<int>();
        var scoreColumnToAnswerColumn = new Dictionary<int, int>();

        for (int i = 0; i < headers.Count; i++)
        {
            if (headers[i]?.EndsWith(ScoreSuffix, StringComparison.OrdinalIgnoreCase) == true)
            {
                scoreColumns.Add(i);
                if (i > 0)
                {
                    scoreColumnToAnswerColumn[i] = i - 1;
                }
            }
        }

        foreach (var h in normalizedHeaders)
        {
            if (scoreColumns.Contains(h.Index))
                continue;

            if (MatchesAny(h.Normalized, GroupKeywords))
            {
                if (DebugMode) Console.WriteLine($"  [DEBUG] Col {h.Index + 1} '{h.Original?.Substring(0, Math.Min(30, h.Original?.Length ?? 0))}' → Group");
                mapping.GroupColumn = h.Index;
            }
            else if (MatchesAny(h.Normalized, IdKeywords))
            {
                if (DebugMode) Console.WriteLine($"  [DEBUG] Col {h.Index + 1} '{h.Original?.Substring(0, Math.Min(30, h.Original?.Length ?? 0))}' → ID");
                mapping.IdColumn = h.Index;
            }
            else if (MatchesAny(h.Normalized, DateKeywords))
            {
                if (DebugMode) Console.WriteLine($"  [DEBUG] Col {h.Index + 1} '{h.Original?.Substring(0, Math.Min(30, h.Original?.Length ?? 0))}' → Date");
                mapping.DateColumn = h.Index;
            }
            else if (MatchesAny(h.Normalized, FullNameKeywords))
            {
                if (DebugMode) Console.WriteLine($"  [DEBUG] Col {h.Index + 1} '{h.Original?.Substring(0, Math.Min(30, h.Original?.Length ?? 0))}' → FullName");
                mapping.FullNameColumn = h.Index;
            }
            else if (MatchesAny(h.Normalized, TotalScoreKeywords))
            {
                if (DebugMode) Console.WriteLine($"  [DEBUG] Col {h.Index + 1} '{h.Original?.Substring(0, Math.Min(30, h.Original?.Length ?? 0))}' → TotalScore");
                mapping.TotalScoreColumn = h.Index;
            }
            else if (MatchesAny(h.Normalized, MaxScoreKeywords))
            {
                if (DebugMode) Console.WriteLine($"  [DEBUG] Col {h.Index + 1} '{h.Original?.Substring(0, Math.Min(30, h.Original?.Length ?? 0))}' → MaxScore");
                mapping.MaxScoreColumn = h.Index;
            }
            else if (MatchesAny(h.Normalized, ResultKeywords))
            {
                if (DebugMode) Console.WriteLine($"  [DEBUG] Col {h.Index + 1} '{h.Original?.Substring(0, Math.Min(30, h.Original?.Length ?? 0))}' → Result");
                mapping.ResultColumn = h.Index;
            }
        }

        var serviceColumns = new HashSet<int>
        {
            mapping.IdColumn, mapping.DateColumn, mapping.GroupColumn,
            mapping.FullNameColumn, mapping.TotalScoreColumn,
            mapping.MaxScoreColumn, mapping.ResultColumn
        };
        foreach (var sc in scoreColumns)
            serviceColumns.Add(sc);

        for (int i = 0; i < headers.Count; i++)
        {
            if (serviceColumns.Contains(i))
                continue;

            var header = headers[i];
            if (string.IsNullOrWhiteSpace(header))
                continue;

            int? scoreCol = null;
            if (i + 1 < headers.Count && scoreColumns.Contains(i + 1))
            {
                scoreCol = i + 1;
            }

            if (scoreCol.HasValue)
            {
                mapping.Questions.Add(new QuestionInfo
                {
                    Name = header.Trim(),
                    AnswerColumnIndex = i,
                    ScoreColumnIndex = scoreCol.Value,
                    Type = QuestionType.Choice
                });
            }
            else
            {
                mapping.Questions.Add(new QuestionInfo
                {
                    Name = header.Trim(),
                    AnswerColumnIndex = i,
                    ScoreColumnIndex = -1,
                    Type = QuestionType.OpenText
                });
            }
        }

        return mapping;
    }

    private static string Normalize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return string.Empty;
        return header.Trim().ToLowerInvariant()
            .Replace("\n", " ")
            .Replace("  ", " ");
    }

    private static bool MatchesAny(string normalizedHeader, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (normalizedHeader.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

