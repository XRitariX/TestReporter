using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TestReporter.Parser.Utils
{
    public static class ValueParser
    {
        public static bool TryParseScore(string s, out int score)
        {
            score = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out score)) return ValidateScore(score);
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.CurrentCulture, out score)) return ValidateScore(score);

            if (double.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            {
                if (double.IsInfinity(d) || double.IsNaN(d)) return false;
                if (d < int.MinValue || d > int.MaxValue) return false;
                score = (int)Math.Truncate(d);
                return ValidateScore(score);
            }
            if (double.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out d))
            {
                if (double.IsInfinity(d) || double.IsNaN(d)) return false;
                if (d < int.MinValue || d > int.MaxValue) return false;
                score = (int)Math.Truncate(d);
                return ValidateScore(score);
            }

            var m = Regex.Match(s, "[-+]?\\d+");
            if (m.Success)
            {
                if (long.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                {
                    if (l < int.MinValue || l > int.MaxValue) return false;
                    score = (int)l;
                    return ValidateScore(score);
                }
            }

            return false;
        }

        private static bool ValidateScore(int score)
        {
            if (Math.Abs(score) > 1000) return false;
            return true;
        }
    }
}
