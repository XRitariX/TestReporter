namespace TestReporter.Parser.Models;

public class ParsedFile
{
    public string FilePath { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public List<QuestionInfo> Questions { get; set; } = new();
    public List<TestRecord> Records { get; set; } = new();
    public HashSet<string?> Groups => Records.Select(r => r.Group).ToHashSet();
    public HashSet<string> Students => Records.Select(r => r.FullName).ToHashSet();
    public int TotalStudents => Records.Count;
    public DateTime? EarliestDate => Records.Where(r => r.CreatedAt.HasValue).MinBy(r => r.CreatedAt)?.CreatedAt;
    public DateTime? LatestDate => Records.Where(r => r.CreatedAt.HasValue).MaxBy(r => r.CreatedAt)?.CreatedAt;
}
