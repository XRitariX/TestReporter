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
        [System.ComponentModel.Browsable(false)]
        public int? Score { get; set; }

        public string ScoreDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(QuestionName) && (QuestionName.ToLowerInvariant().Contains("id") || QuestionName.ToLowerInvariant().Contains("ид")))
                    return "-";
                if (!Score.HasValue)
                    return string.Empty;
                return Score.Value.ToString();
            }
        }

        public bool IsCorrect => Score == 1;
    }
}

