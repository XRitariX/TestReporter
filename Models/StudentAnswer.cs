using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Models
{
    public class StudentAnswer
    {
        public string QuestionName { get; set; } = string.Empty;
        public string? AnswerText { get; set; }
        public int? Score { get; set; }
        public bool IsCorrect => Score == 1;
    }
}

