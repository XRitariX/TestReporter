using ClosedXML.Excel;
using TestReporter.Models;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Parser.Reporting;

public class ReportGenerator
{
    public void GenerateReport(List<ParsedFile> parsedFiles, string outputPath)
    {
        if (parsedFiles == null || parsedFiles.Count == 0)
            throw new ArgumentException("Нет данных для формирования отчёта.", nameof(parsedFiles));

        var allRecords = parsedFiles.SelectMany(f => f.Records).ToList();
        var allQuestions = parsedFiles.SelectMany(f => f.Questions).ToList();

        using var workbook = new XLWorkbook();

        GenerateAllGroupsSheet(workbook, allRecords);
        GenerateGroupSheets(workbook, allRecords);
        GenerateTopicSummarySheet(workbook, allRecords);
        GenerateQuestionStatisticsSheet(workbook, parsedFiles);

        workbook.SaveAs(outputPath);
    }

    private void GenerateAllGroupsSheet(XLWorkbook workbook, List<TestRecord> allRecords)
    {
        var sheet = workbook.Worksheets.Add("Все группы");

        var headers = new[] { "Группа", "Студент", "Дата", "Тема", "Суммарный балл" };
        WriteHeaders(sheet, headers);

        var sorted = allRecords
            .OrderBy(r => r.Group ?? string.Empty)
            .ThenBy(r => r.FullName)
            .ThenBy(r => r.CreatedAt ?? DateTime.MinValue)
            .ThenBy(r => r.Topic)
            .ToList();

        int row = 2;
        foreach (var record in sorted)
        {
            sheet.Cell(row, 1).Value = record.Group ?? "(нет группы)";
            sheet.Cell(row, 2).Value = record.FullName;
            sheet.Cell(row, 3).Value = record.CreatedAt?.ToString("dd.MM.yyyy HH:mm") ?? "";
            sheet.Cell(row, 4).Value = record.Topic;
            sheet.Cell(row, 5).Value = record.ComputedScore;
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

            var headers = new[] { "Студент", "Дата", "Тема", "Суммарный балл" };
            WriteHeaders(sheet, headers);

            var groupRecords = allRecords
                .Where(r => r.Group == group)
                .OrderBy(r => r.FullName)
                .ThenBy(r => r.CreatedAt ?? DateTime.MinValue)
                .ThenBy(r => r.Topic)
                .ToList();

            int row = 2;
            foreach (var record in groupRecords)
            {
                sheet.Cell(row, 1).Value = record.FullName;
                sheet.Cell(row, 2).Value = record.CreatedAt?.ToString("dd.MM.yyyy HH:mm") ?? "";
                sheet.Cell(row, 3).Value = record.Topic;
                sheet.Cell(row, 4).Value = record.ComputedScore;
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
        headers.Add("Сумма баллов");
        WriteHeaders(sheet, headers.ToArray());

        var studentTopicScores = allRecords
            .GroupBy(r => r.FullName)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.Topic)
                      .ToDictionary(
                          tg => tg.Key,
                          tg => tg.Sum(r => r.ComputedScore)
                      )
            );

        var students = studentTopicScores.Keys.OrderBy(n => n).ToList();

        int row = 2;
        int idx = 1;
        foreach (var student in students)
        {
            sheet.Cell(row, 1).Value = idx++;
            sheet.Cell(row, 2).Value = student;

            int totalSum = 0;
            int col = 3;

            foreach (var topic in topics)
            {
                var score = studentTopicScores[student].GetValueOrDefault(topic, 0);
                sheet.Cell(row, col).Value = score;
                totalSum += score;
                col++;
            }

            sheet.Cell(row, col).Value = totalSum;

            var firstDataCol = GetColumnLetter(3);
            var lastDataCol = GetColumnLetter(col - 1);
            sheet.Cell(row, col).FormulaA1 = $"=SUM({firstDataCol}{row}:{lastDataCol}{row})";

            row++;
        }

        FormatSheet(sheet, headers.Count, row - 1);
        sheet.SheetView.FreezeRows(1);
    }

    private void GenerateQuestionStatisticsSheet(XLWorkbook workbook, List<ParsedFile> parsedFiles)
    {
        var sheet = workbook.Worksheets.Add("Статистика по вопросам");

        var headers = new[] { "Тема", "Вопрос", "Правильных ответов", "Всего ответивших", "% правильных" };
        WriteHeaders(sheet, headers);

        int row = 2;

        foreach (var file in parsedFiles.OrderBy(f => f.Topic))
        {
            foreach (var question in file.Questions.Where(q => q.Type == QuestionType.Choice).OrderBy(q => q.Name))
            {
                sheet.Cell(row, 1).Value = file.Topic;

                var qName = question.Name.Replace("\n", " ").Replace("  ", " ");
                if (qName.Length > 100)
                    qName = qName.Substring(0, 97) + "...";
                sheet.Cell(row, 2).Value = qName;

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

                sheet.Cell(row, 3).Value = correctCount;
                sheet.Cell(row, 4).Value = totalAnswered;

                if (totalAnswered > 0)
                {
                    var percentage = (double)correctCount / totalAnswered * 100;
                    sheet.Cell(row, 5).Value = Math.Round(percentage, 1);
                }
                else
                {
                    sheet.Cell(row, 5).Value = 0;
                }

                row++;
            }
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
