using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using TestReporter.Models;
using TestReporter.Parser.Utils;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace TestReporter.Parser.Parsers;

public class CsvParser
{
    public class CsvParserOptions
    {
        public string? Delimiter { get; set; }
        public System.Text.Encoding Encoding { get; set; } = System.Text.Encoding.UTF8;
    }

    public ParsedFile Parse(string filePath, CsvParserOptions? options = null)
    {
        options ??= new CsvParserOptions();

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Файл не найден", filePath);

        var fileName = Path.GetFileName(filePath);
        var topic = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension != ".csv")
            throw new NotSupportedException($"Формат {extension} не поддерживается. Используйте .csv");

        var delimiter = options.Delimiter ?? DetectDelimiter(filePath, options.Encoding);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            Encoding = options.Encoding,
            BadDataFound = null,
            MissingFieldFound = null,
        };

        using var reader = new StreamReader(filePath, options.Encoding);
        using var csv = new CsvReader(reader, config);

        if (!csv.Read())
            throw new InvalidOperationException("CSV-файл пуст.");

        csv.ReadHeader();
        var headers = csv.HeaderRecord?.ToList()
            ?? throw new InvalidOperationException("Не удалось прочитать заголовки CSV.");

        var mapping = ColumnMapper.BuildMapping(headers);

        if (!mapping.IsValid)
        {
            throw new InvalidOperationException(
                "Не удалось автоматически распознать структуру CSV. " +
                "Убедитесь, что есть колонка с ФИО и хотя бы один вопрос.\n\n" +
                $"Найдено {headers.Count} колонок:\n" +
                string.Join("\n", headers.Select((h, i) => $"  [{i + 1}] {h}")));
        }

        var records = new List<TestRecord>();

        while (csv.Read())
        {
            var record = ParseCsvRow(csv, mapping, topic, headers);
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

    private TestRecord? ParseCsvRow(CsvReader csv, ColumnMapping mapping, string topic, List<string> headers)
    {
        if (mapping.FullNameColumn < 0 || mapping.FullNameColumn >= headers.Count)
            return null;

        var fullName = csv.GetField(mapping.FullNameColumn)?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            return null;

        var record = new TestRecord
        {
            FullName = fullName,
            Topic = topic
        };

        if (mapping.IdColumn >= 0 && mapping.IdColumn < headers.Count)
            record.Id = csv.GetField(mapping.IdColumn)?.Trim();

        if (mapping.DateColumn >= 0 && mapping.DateColumn < headers.Count)
        {
            var dateStr = csv.GetField(mapping.DateColumn)?.Trim();
            if (!string.IsNullOrEmpty(dateStr))
                record.CreatedAt = ParseDateTimeString(dateStr);
        }

        if (mapping.GroupColumn >= 0 && mapping.GroupColumn < headers.Count)
            record.Group = csv.GetField(mapping.GroupColumn)?.Trim();

        if (mapping.TotalScoreColumn >= 0 && mapping.TotalScoreColumn < headers.Count)
        {
            var totalStr = csv.GetField(mapping.TotalScoreColumn)?.Trim();
            if (int.TryParse(totalStr, out var total))
                record.TotalScore = total;
        }

        if (mapping.MaxScoreColumn >= 0 && mapping.MaxScoreColumn < headers.Count)
        {
            var maxStr = csv.GetField(mapping.MaxScoreColumn)?.Trim();
            if (int.TryParse(maxStr, out var max))
                record.MaxPossibleScore = max;
        }

        if (mapping.ResultColumn >= 0 && mapping.ResultColumn < headers.Count)
            record.Result = csv.GetField(mapping.ResultColumn)?.Trim();

        foreach (var question in mapping.Questions)
        {
            var answer = new StudentAnswer
            {
                QuestionName = question.Name
            };

            if (question.AnswerColumnIndex >= 0 && question.AnswerColumnIndex < headers.Count)
                answer.AnswerText = csv.GetField(question.AnswerColumnIndex)?.Trim();

            if (question.ScoreColumnIndex >= 0 && question.ScoreColumnIndex < headers.Count)
            {
                var scoreStr = csv.GetField(question.ScoreColumnIndex)?.Trim();
                if (int.TryParse(scoreStr, out var score))
                    answer.Score = score;
                else if (double.TryParse(scoreStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var dscore))
                    answer.Score = (int)dscore;
            }

            record.Answers.Add(answer);
        }

        return record;
    }

    private static string DetectDelimiter(string filePath, System.Text.Encoding encoding)
    {
        using var reader = new StreamReader(filePath, encoding);
        var firstLine = reader.ReadLine();
        if (string.IsNullOrEmpty(firstLine))
            return ";";

        var semicolons = firstLine.Count(c => c == ';');
        var commas = firstLine.Count(c => c == ',');
        var tabs = firstLine.Count(c => c == '\t');

        var max = Math.Max(semicolons, Math.Max(commas, tabs));
        if (max == 0)
            return ";";

        if (max == semicolons) return ";";
        if (max == commas) return ",";
        return "\t";
    }

    private static DateTime? ParseDateTimeString(string str)
    {
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
