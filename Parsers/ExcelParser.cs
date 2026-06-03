using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using ClosedXML.Excel;
using TestReporter.Models;
using TestReporter.Parser.Utils;

namespace TestReporter.Parser.Parsers;

public class ExcelParser
{
    public ParsedFile Parse(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл не найден", filePath);

        var fileName = Path.GetFileName(filePath);
        var topic = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension != ".xlsx" && extension != ".xls")
            throw new NotSupportedException($"Формат {extension} не поддерживается. Используйте .xlsx или .xls");

        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheet(1);

        var headers = ReadHeaders(worksheet);

        if (ColumnMapper.DebugMode)
        {
            Console.WriteLine("=== ЗАГОЛОВКИ ===");
            for (int i = 0; i < headers.Count; i++)
                Console.WriteLine($"  [{i + 1}] {headers[i]}");
        }

        var mapping = ColumnMapper.BuildMapping(headers);

        if (ColumnMapper.DebugMode)
        {
            Console.WriteLine("=== MAPPING ===");
            Console.WriteLine(mapping.ToString());
        }

        if (!mapping.IsValid)
        {
            throw new InvalidOperationException(
                "Не удалось автоматически распознать структуру файла. " +
                "Убедитесь, что есть колонка с ФИО и хотя бы один вопрос.\n\n" +
                $"Найдено {headers.Count} колонок:\n" +
                string.Join("\n", headers.Select((h, i) => $"  [{i + 1}] {h}")));
        }

        var records = new List<TestRecord>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (int rowIdx = 2; rowIdx <= lastRow; rowIdx++)
        {
            var row = worksheet.Row(rowIdx);
            var record = ParseRow(row, mapping, topic, headers);
            if (record != null)
                records.Add(record);
        }

        return new ParsedFile
        {
            FilePath = filePath,
            FileName = fileName,
            Topic = topic,
            Extension = extension,
            Questions = mapping.Questions,
            Records = records
        };
    }

    private List<string> ReadHeaders(IXLWorksheet worksheet)
    {
        var headers = new List<string>();
        var firstRow = worksheet.Row(1);
        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 1;

        for (int col = 1; col <= lastCol; col++)
        {
            var cellValue = firstRow.Cell(col).GetString()?.Trim() ?? string.Empty;
            headers.Add(cellValue);
        }

        return headers;
    }

    private TestRecord? ParseRow(IXLRow row, ColumnMapping mapping, string topic, List<string> headers)
    {
        var fioCell = row.Cell(mapping.FullNameColumn + 1);
        if (fioCell.IsEmpty())
            return null;

        var fullName = fioCell.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return null;

        var record = new TestRecord
        {
            FullName = fullName,
            Topic = topic
        };

        if (mapping.IdColumn >= 0)
        {
            var idCell = row.Cell(mapping.IdColumn + 1);
            record.Id = idCell.IsEmpty() ? null : idCell.GetString()?.Trim() ?? idCell.GetValue<object>()?.ToString();
        }

        if (mapping.DateColumn >= 0)
        {
            var dateCell = row.Cell(mapping.DateColumn + 1);
            if (!dateCell.IsEmpty())
                record.CreatedAt = ParseDateTime(dateCell);
        }

        if (mapping.GroupColumn >= 0)
        {
            var groupCell = row.Cell(mapping.GroupColumn + 1);
            record.Group = groupCell.IsEmpty() ? null : groupCell.GetString()?.Trim() ?? groupCell.GetValue<object>()?.ToString();
        }

        if (mapping.TotalScoreColumn >= 0)
        {
            var totalCell = row.Cell(mapping.TotalScoreColumn + 1);
            if (!totalCell.IsEmpty() && totalCell.TryGetValue<int>(out var totalScore))
                record.TotalScore = totalScore;
        }

        if (mapping.MaxScoreColumn >= 0)
        {
            var maxCell = row.Cell(mapping.MaxScoreColumn + 1);
            if (!maxCell.IsEmpty() && maxCell.TryGetValue<int>(out var maxScore))
                record.MaxPossibleScore = maxScore;
        }

        if (mapping.ResultColumn >= 0)
        {
            var resultCell = row.Cell(mapping.ResultColumn + 1);
            record.Result = resultCell.IsEmpty() ? null : resultCell.GetString()?.Trim();
        }

        foreach (var question in mapping.Questions)
        {
            var answer = new StudentAnswer
            {
                QuestionName = question.Name
            };

            if (question.AnswerColumnIndex >= 0 && question.AnswerColumnIndex < headers.Count)
            {
                var ansCell = row.Cell(question.AnswerColumnIndex + 1);
                answer.AnswerText = ansCell.IsEmpty() ? null : ansCell.GetString()?.Trim();
            }

            if (question.ScoreColumnIndex >= 0 && question.ScoreColumnIndex < headers.Count)
            {
                var scoreCell = row.Cell(question.ScoreColumnIndex + 1);
                if (!scoreCell.IsEmpty())
                {
                    var s = scoreCell.GetString();
                    var qname = (question.Name ?? string.Empty).ToLowerInvariant();
                    // If this question looks like an ID field, do not treat numeric cell as score
                    if (qname.Contains("id") || qname.Contains("ид"))
                    {
                        answer.AnswerText = scoreCell.IsEmpty() ? null : scoreCell.GetString()?.Trim();
                        answer.Score = null;
                    }
                    else
                    {
                        if (TestReporter.Parser.Utils.ValueParser.TryParseScore(s, out var score))
                            answer.Score = score;
                    }
                }
            }

            record.Answers.Add(answer);
        }

        return record;
    }

    private static DateTime? ParseDateTime(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime || cell.DataType == XLDataType.Number)
        {
            try
            {
                return cell.GetDateTime();
            }
            catch { }
        }

        var str = cell.GetString()?.Trim();
        if (string.IsNullOrEmpty(str))
            return null;

        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "dd.MM.yyyy HH:mm:ss",
            "dd.MM.yyyy HH:mm",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
        };

        if (DateTime.TryParseExact(str, formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt))
            return dt;

        if (DateTime.TryParse(str, out dt))
            return dt;

        return null;
    }
}

