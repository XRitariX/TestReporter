using TestReporter.Models;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using ClosedXML.Excel;

namespace TestReporter.Parser.Reporting;

public enum AggregationMode
{
    Sum,
    Average
}

public class ReportGenerator
{
    private AggregationMode _aggregationMode = AggregationMode.Sum;

    public ReportGenerator(AggregationMode mode = AggregationMode.Sum)
    {
        _aggregationMode = mode;
    }

    public void GenerateReport(List<ParsedFile> parsedFiles, string outputPath)
    {
        if (parsedFiles == null || parsedFiles.Count == 0)
            throw new ArgumentException("Нет данных для формирования отчёта.", nameof(parsedFiles));

        var allRecords = parsedFiles.SelectMany(f => f.Records).ToList();

        using var workbook = new XLWorkbook();

        GenerateAllGroupsSheet(workbook, allRecords);
        GenerateGroupSheets(workbook, allRecords);
        GenerateTopicSummarySheet(workbook, allRecords);
        GenerateQuestionStatisticsSheet(workbook, parsedFiles);

        workbook.SaveAs(outputPath);
    }

    private double CalculateScore(TestRecord record)
    {
        if (_aggregationMode == AggregationMode.Average)
        {
            int correct = record.Answers.Count(a => a.Score == 1);
            int total = record.Answers.Count();
            return total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;
        }
        else
        {
            return record.ComputedScore;
        }
    }

    private void GenerateAllGroupsSheet(XLWorkbook workbook, List<TestRecord> allRecords)
    {
        var sheet = workbook.Worksheets.Add("Все группы");

        var headers = new[] { "Группа", "Студент", "Дата", "Тема", _aggregationMode == AggregationMode.Average ? "% правильных" : "Суммарный балл" };
        WriteHeaders(sheet, headers);

        var aggregated = allRecords
            .GroupBy(r => new { r.Group, r.FullName, r.CreatedAt, r.Topic })
            .Select(g => new
            {
                Group = g.Key.Group,
                Student = g.Key.FullName,
                Date = g.Key.CreatedAt,
                Topic = g.Key.Topic,
                Score = _aggregationMode == AggregationMode.Average
                    ? Math.Round(g.SelectMany(r => r.Answers).Average(a => a.Score == 1 ? 100 : 0), 1)
                    : g.Sum(r => r.ComputedScore)
            })
            .OrderBy(x => x.Group ?? string.Empty)
            .ThenBy(x => x.Student)
            .ThenBy(x => x.Date ?? DateTime.MinValue)
            .ThenBy(x => x.Topic)
            .ToList();

        int row = 2;
        foreach (var item in aggregated)
        {
            sheet.Cell(row, 1).Value = item.Group ?? "";
            sheet.Cell(row, 2).Value = item.Student;
            sheet.Cell(row, 3).Value = item.Date?.ToString("dd.MM.yyyy HH:mm") ?? "";
            sheet.Cell(row, 4).Value = item.Topic;
            sheet.Cell(row, 5).Value = item.Score;
            row++;
        }

        FormatSheet(sheet, headers.Length, row - 1);
    }

    private void GenerateGroupSheets(XLWorkbook workbook, List<TestRecord> allRecords)
    {
        var groups = allRecords
            .Select(r => r.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g)
            .ToList();

        foreach (var group in groups)
        {
            var sheetName = $"Группа {group}".Length > 31
                ? $"Группа {group}".Substring(0, 31)
                : $"Группа {group}";

            if (workbook.Worksheets.Contains(sheetName))
                sheetName = sheetName.Substring(0, Math.Min(28, sheetName.Length)) + $"_{workbook.Worksheets.Count}";

            var sheet = workbook.Worksheets.Add(sheetName);

            var headers = new[] { "Студент", "Дата", "Тема", _aggregationMode == AggregationMode.Average ? "% правильных" : "Суммарный балл" };
            WriteHeaders(sheet, headers);

            var aggregated = allRecords
                .Where(r => r.Group == group)
                .GroupBy(r => new { r.FullName, r.CreatedAt, r.Topic })
                .Select(g => new
                {
                    Student = g.Key.FullName,
                    Date = g.Key.CreatedAt,
                    Topic = g.Key.Topic,
                    Score = _aggregationMode == AggregationMode.Average
                        ? Math.Round(g.SelectMany(r => r.Answers).Average(a => a.Score == 1 ? 100 : 0), 1)
                        : g.Sum(r => r.ComputedScore)
                })
                .OrderBy(x => x.Student)
                .ThenBy(x => x.Date ?? DateTime.MinValue)
                .ThenBy(x => x.Topic)
                .ToList();

            int row = 2;
            foreach (var item in aggregated)
            {
                sheet.Cell(row, 1).Value = item.Student;
                sheet.Cell(row, 2).Value = item.Date?.ToString("dd.MM.yyyy HH:mm") ?? "";
                sheet.Cell(row, 3).Value = item.Topic;
                sheet.Cell(row, 4).Value = item.Score;
                row++;
            }

            FormatSheet(sheet, headers.Length, row - 1);
        }
    }

    private void GenerateTopicSummarySheet(XLWorkbook workbook, List<TestRecord> allRecords)
    {
        var sheet = workbook.Worksheets.Add("Сводка по темам");

        var topics = allRecords
            .Select(r => r.Topic)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var headers = new List<string> { "№", "ФИО" };
        headers.AddRange(topics);
        headers.Add(_aggregationMode == AggregationMode.Average ? "Средний %" : "Сумма баллов");
        WriteHeaders(sheet, headers.ToArray());

        // Получаем уникальных студентов (без дубликатов)
        var uniqueStudents = allRecords
            .Select(r => r.FullName)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        // Для каждого студента и темы считаем баллы (максимум за все попытки)
        var studentTopicScores = new Dictionary<string, Dictionary<string, double>>();

        foreach (var student in uniqueStudents)
        {
            var studentScores = new Dictionary<string, double>();

            foreach (var topic in topics)
            {
                var recordsForTopicStudent = allRecords
                    .Where(r => r.FullName == student && r.Topic == topic)
                    .ToList();

                if (recordsForTopicStudent.Count > 0)
                {
                    // Берем МАКСИМУМ из всех попыток для этого студента по этой теме
                    double score = 0;

                    if (_aggregationMode == AggregationMode.Average)
                    {
                        // Для среднего - берем максимум процента правильных ответов
                        score = recordsForTopicStudent
                            .Select(r =>
                            {
                                int correctCount = r.Answers.Count(a => a.Score == 1);
                                int totalCount = r.Answers.Count();
                                return totalCount > 0 ? Math.Round((double)correctCount / totalCount * 100, 1) : 0;
                            })
                            .Max();
                    }
                    else // Sum
                    {
                        // Для суммы - берем максимальный результат за одну попытку
                        score = recordsForTopicStudent
                            .Select(r => (double)r.ComputedScore)
                            .Max();
                    }

                    studentScores[topic] = score;
                }
            }

            studentTopicScores[student] = studentScores;
        }

        int row = 2;
        int idx = 1;
        foreach (var student in uniqueStudents)
        {
            sheet.Cell(row, 1).Value = idx;
            sheet.Cell(row, 2).Value = student;

            int col = 3;

            foreach (var topic in topics)
            {
                var score = studentTopicScores[student].GetValueOrDefault(topic, 0);
                // Устанавливаем ЧИСЛОВОЕ значение, а не строку!
                if (score > 0)
                {
                    sheet.Cell(row, col).Value = score;
                    // Форматируем как число с 1 знаком после запятой
                    sheet.Cell(row, col).Style.NumberFormat.Format = "0.0";
                }
                else
                {
                    sheet.Cell(row, col).Value = string.Empty;
                }
                col++;
            }

            // Последний столбец — формула среднего или суммы
            var firstDataCol = GetColumnLetter(3);
            var lastDataCol = GetColumnLetter(col - 1);
            if (_aggregationMode == AggregationMode.Average)
                sheet.Cell(row, col).FormulaA1 = $"=AVERAGE({firstDataCol}{row}:{lastDataCol}{row})";
            else
                sheet.Cell(row, col).FormulaA1 = $"=SUM({firstDataCol}{row}:{lastDataCol}{row})";

            row++;
            idx++;
        }

        FormatSheet(sheet, headers.Count, row - 1);
        sheet.SheetView.FreezeRows(1);
    }

    private void GenerateQuestionStatisticsSheet(XLWorkbook workbook, List<ParsedFile> parsedFiles)
    {
        var sheet = workbook.Worksheets.Add("Статистика по вопросам");

        var headers = new[] { "Тема", "Вопрос", "Правильных ответов", "Всего ответивших", "% правильных" };
        WriteHeaders(sheet, headers);

        var data = new List<(string Topic, string Question, int Correct, int Total, double Percentage)>();

        foreach (var file in parsedFiles)
        {
            foreach (var question in file.Questions.OrderBy(q => q.Name))
            {
                int correctCount = 0;
                int totalAnswered = 0;

                foreach (var record in file.Records)
                {
                    var answer = record.Answers.FirstOrDefault(a => a.QuestionName == question.Name);
                    if (answer != null && answer.Score.HasValue)
                    {
                        totalAnswered++;
                        if (answer.Score.Value == 1)
                            correctCount++;
                    }
                }

                var percentage = totalAnswered > 0 ? Math.Round((double)correctCount / totalAnswered * 100, 1) : 0.0;
                data.Add((file.Topic, question.Name, correctCount, totalAnswered, percentage));
            }
        }

        // Сортируем: тема (алфавитно) → вопрос (алфавитно)
        data = data.OrderBy(d => d.Topic).ThenBy(d => d.Question).ToList();

        int row = 2;
        foreach (var item in data)
        {
            sheet.Cell(row, 1).Value = item.Topic;

            var qName = item.Question.Replace("\n", " ").Replace("  ", " ");
            if (qName.Length > 100)
                qName = qName.Substring(0, 97) + "...";
            sheet.Cell(row, 2).Value = qName;

            sheet.Cell(row, 3).Value = item.Correct;
            sheet.Cell(row, 4).Value = item.Total;
            sheet.Cell(row, 5).Value = item.Percentage;

            row++;
        }

        FormatSheet(sheet, headers.Length, row - 1);

        var percentageColumn = sheet.Column(5);
        percentageColumn.Style.NumberFormat.Format = "0.0";
    }

    private static void WriteHeaders(IXLWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
    }

    private static void FormatSheet(IXLWorksheet sheet, int colCount, int lastDataRow)
    {
        for (int i = 1; i <= colCount; i++)
        {
            sheet.Column(i).AdjustToContents();
            if (sheet.Column(i).Width > 80)
                sheet.Column(i).Width = 80;
        }

        if (lastDataRow >= 2)
        {
            var range = sheet.Range(1, 1, lastDataRow, colCount);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        sheet.SheetView.FreezeRows(1);
    }

    private static string GetColumnLetter(int colNumber)
    {
        var result = string.Empty;
        while (colNumber > 0)
        {
            colNumber--;
            result = (char)('A' + (colNumber % 26)) + result;
            colNumber /= 26;
        }
        return result;
    }
}
