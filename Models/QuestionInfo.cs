using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Models;

public class QuestionInfo
{
    public string Name { get; set; } = string.Empty;
    public int AnswerColumnIndex { get; set; }
    public int ScoreColumnIndex { get; set; } = -1;
    public bool HasScoreColumn => ScoreColumnIndex >= 0;
    public QuestionType Type { get; set; } = QuestionType.Choice;
}

public enum QuestionType
{
    OpenText,
    Choice
}

