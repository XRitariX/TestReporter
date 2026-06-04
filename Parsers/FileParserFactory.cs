using TestReporter.Models;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Parser.Parsers;

public class FileParserFactory
{
    private readonly ExcelParser _excelParser = new();
    private readonly CsvParser _csvParser = new();

    public ParsedFile Parse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".xlsx" => _excelParser.Parse(filePath),
            ".csv" => _csvParser.Parse(filePath),
            _ => throw new NotSupportedException(
                $"Формат файла '{extension}' не поддерживается. " +
                "Поддерживаемые форматы: .xlsx, .csv")
        };
    }

    public List<ParsedFile> ParseMultiple(IEnumerable<string> filePaths)
    {
        var results = new List<ParsedFile>();
        var errors = new List<string>();

        foreach (var filePath in filePaths)
        {
            try
            {
                var result = Parse(filePath);
                results.Add(result);
            }
            catch (Exception ex)
            {
                errors.Add($"Ошибка при парсинге '{filePath}': {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var error in errors)
                Console.WriteLine($"WARNING: {error}");
            Console.ResetColor();
        }

        return results;
    }

    public List<ParsedFile> ParseDirectory(string directoryPath, bool recursive = false)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Директория не найдена: {directoryPath}");

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var xlsxFiles = Directory.GetFiles(directoryPath, "*.xlsx", searchOption);
        var csvFiles = Directory.GetFiles(directoryPath, "*.csv", searchOption);

        var allFiles = xlsxFiles.Concat(csvFiles).ToList();

        Console.WriteLine($"Найдено файлов: {allFiles.Count} (.xlsx: {xlsxFiles.Length}, .csv: {csvFiles.Length})");

        return ParseMultiple(allFiles);
    }
}
