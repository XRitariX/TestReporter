namespace TestReporter.Models
{
    public class QuestionStat
    {
        public string Topic { get; set; }
        public string Question { get; set; }
        public int CorrectCount { get; set; }
        public int TotalCount { get; set; }
        public double Percent => TotalCount == 0 ? 0 : (double)CorrectCount / TotalCount * 100.0;
    }
}