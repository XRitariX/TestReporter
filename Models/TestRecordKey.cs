using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

namespace TestReporter.Models
{

    public record TestRecordKey(
        string Student,
        DateTime? Date,
        string Theme,
        string Question
    );


    public class TestRecordKeyComparer : IEqualityComparer<TestRecordKey>
    {
        public bool Equals(TestRecordKey? x, TestRecordKey? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;

            return string.Equals(x.Student, y.Student, StringComparison.OrdinalIgnoreCase) &&
                   x.Date == y.Date &&
                   string.Equals(x.Theme, y.Theme, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.Question, y.Question, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(TestRecordKey obj)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (obj.Student != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Student) : 0);
                hash = hash * 23 + obj.Date.GetHashCode();
                hash = hash * 23 + (obj.Theme != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Theme) : 0);
                hash = hash * 23 + (obj.Question != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Question) : 0);
                return hash;
            }
        }
    }
}
