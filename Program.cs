using TestReporter.Parser.Parsers;
using TestReporter.Parser.Reporting;

namespace TestReporter.Parser;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("TestReporter — Парсер + Генератор отчётов");
        Console.WriteLine();

        string? inputPath = args.Length > 0 ? args[0] : null;
        string? outputPath = args.Length > 1 ? args[1] : null;

        if (string.IsNullOrEmpty(inputPath))
        {
            var currentDir = Directory.GetCurrentDirectory();
            var testFiles = Directory.GetFiles(currentDir, "*.xlsx")
                .Concat(Directory.GetFiles(currentDir, "*.csv"))
                .ToArray();

            if (testFiles.Length == 0)
            {
                Console.WriteLine("Использование:");
                Console.WriteLine("  dotnet run -- \"<входной_файл_или_папка>\" [выходной_файл.xlsx]");
                Console.WriteLine();
                Console.WriteLine("Примеры:");
                Console.WriteLine("  dotnet run -- \"/home/user/tests/test.xlsx\"");
                Console.WriteLine("  dotnet run -- \"/home/user/tests/\" \"/home/user/Отчёт.xlsx\"");
                return;
            }

            inputPath = testFiles[0];
            Console.WriteLine($"Автоматически найден файл: {inputPath}");
        }

        if (string.IsNullOrEmpty(outputPath))
        {
            var inputDir = File.Exists(inputPath)
                ? Path.GetDirectoryName(Path.GetFullPath(inputPath))!
                : inputPath;

            outputPath = Path.Combine(inputDir, $"Отчёт_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx");

            try
            {
                var testFile = Path.Combine(inputDir, $".write_test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch
            {
                outputPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    $"Отчёт_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== МОДУЛЬ 1: ПАРСИНГ ===");
        Console.WriteLine();

        var factory = new FileParserFactory();
        List<Models.ParsedFile> parsedFiles;

        try
        {
            if (Directory.Exists(inputPath))
            {
                Console.WriteLine($"Директория: {inputPath}");
                parsedFiles = factory.ParseDirectory(inputPath, recursive: true);
            }
            else if (File.Exists(inputPath))
            {
                Console.WriteLine($"Файл: {inputPath}");
                parsedFiles = new List<Models.ParsedFile> { factory.Parse(inputPath) };
            }
            else
            {
                Console.WriteLine($"ОШИБКА: Путь не найден: {inputPath}");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка парсинга: {ex.Message}");
            Console.ResetColor();
            return;
        }

        if (parsedFiles.Count == 0)
        {
            Console.WriteLine("Нет успешно распарсенных файлов.");
            return;
        }

        DisplayParseSummary(parsedFiles);

        Console.WriteLine();
        Console.WriteLine("=== МОДУЛЬ 2: ФОРМИРОВАНИЕ ОТЧЁТА ===");
        Console.WriteLine();

        try
        {
            var reportGenerator = new ReportGenerator();
            reportGenerator.GenerateReport(parsedFiles, outputPath);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Отчёт сохранён: {outputPath}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Ошибка генерации отчёта: {ex.Message}");
            Console.ResetColor();

            if (ex.Message.Contains("libgdiplus", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Для генерации отчёта на Linux требуется libgdiplus:");
                Console.WriteLine("  Fedora:  sudo dnf install libgdiplus");
                Console.WriteLine("  Ubuntu:  sudo apt-get install libgdiplus");
                Console.ResetColor();
            }
        }
    }

    static void DisplayParseSummary(List<Models.ParsedFile> results)
    {
        Console.WriteLine($"Успешно: {results.Count}");
        Console.WriteLine(new string('-', 50));

        foreach (var file in results)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  {file.FileName}");
            Console.ResetColor();
            Console.WriteLine($"     Тема: {file.Topic}");
            Console.WriteLine($"     Студентов: {file.TotalStudents}");
            Console.WriteLine($"     Групп: {file.Groups.Count(g => g != null)}");
            Console.WriteLine($"     Вопросов: {file.Questions.Count}");

            if (file.EarliestDate.HasValue)
                Console.WriteLine($"     Период: {file.EarliestDate:dd.MM.yyyy} — {file.LatestDate:dd.MM.yyyy}");

            var groups = file.Groups.Where(g => g != null).Cast<string>().ToList();
            if (groups.Count > 0)
                Console.WriteLine($"     Группы: {string.Join(", ", groups)}");

            var choiceQuestions = file.Questions.Where(q => q.Type == Models.QuestionType.Choice).ToList();
            if (choiceQuestions.Count > 0)
            {
                Console.WriteLine($"     Статистика по вопросам:");
                var allAnswers = file.Records.SelectMany(r => r.Answers).ToList();
                foreach (var q in choiceQuestions)
                {
                    var qAnswers = allAnswers.Where(a => a.QuestionName == q.Name).ToList();
                    var total = qAnswers.Count(a => a.Score.HasValue);
                    var correct = qAnswers.Count(a => a.IsCorrect);
                    var pct = total > 0 ? (double)correct / total * 100 : 0;
                    var qShort = q.Name.Length > 45 ? q.Name.Substring(0, 45) + "..." : q.Name;
                    Console.WriteLine($"       {correct}/{total} ({pct:F1}%) — {qShort}");
                }
            }
        }

        if (results.Count > 1)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("  ОБЩАЯ СВОДКА:");
            Console.ResetColor();
            Console.WriteLine($"     Всего студентов: {results.Sum(f => f.TotalStudents)}");
            Console.WriteLine($"     Уникальных групп: {results.SelectMany(f => f.Groups.Where(g => g != null).Cast<string>()).Distinct().Count()}");
            Console.WriteLine($"     Темы: {string.Join(", ", results.Select(f => f.Topic))}");
        }

        Console.WriteLine();
        Console.WriteLine(new string('-', 50));
    }
}
