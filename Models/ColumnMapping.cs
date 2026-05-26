namespace TestReporter.Parser.Models;

public class ColumnMapping
{
    public int IdColumn { get; set; } = -1;
    public int DateColumn { get; set; } = -1;
    public int GroupColumn { get; set; } = -1;
    public int FullNameColumn { get; set; } = -1;
    public int TotalScoreColumn { get; set; } = -1;
    public int MaxScoreColumn { get; set; } = -1;
    public int ResultColumn { get; set; } = -1;
    public List<QuestionInfo> Questions { get; set; } = new();

    public bool IsValid => FullNameColumn >= 0 && Questions.Count > 0;

    public override string ToString()
    {
        var parts = new List<string>
        {
            $"ID[col{IdColumn + 1}]",
            $"Date[col{DateColumn + 1}]",
            $"Group[col{GroupColumn + 1}]",
            $"FIO[col{FullNameColumn + 1}]",
            $"Total[col{TotalScoreColumn + 1}]",
            $"Max[col{MaxScoreColumn + 1}]",
            $"Result[col{ResultColumn + 1}]"
        };
        parts.Add($"Questions: {Questions.Count}");
        foreach (var q in Questions)
        {
            parts.Add($"  - '{q.Name.Substring(0, Math.Min(40, q.Name.Length))}' " +
                      $"answer@col{q.AnswerColumnIndex + 1} score@col{q.ScoreColumnIndex + 1} [{q.Type}]");
        }
        return string.Join("\n", parts);
    }
}
