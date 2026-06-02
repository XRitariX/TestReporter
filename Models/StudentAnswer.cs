using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Models
{
    public class StudentAnswer
    {
        public string StudentName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public string TopicName { get; set; } = string.Empty;
        public DateTime? TestDate { get; set; }
        public string QuestionName { get; set; } = string.Empty;
        public string? AnswerText { get; set; }
        public int? Score { get; set; }
        public bool IsCorrect => Score == 1;
    }
}

