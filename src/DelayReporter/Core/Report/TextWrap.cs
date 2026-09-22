using System;
using System.Collections.Generic;

namespace DelayReporter.Core.Report
{
    /// <summary>
    /// Wraps text into explicit lines.
    ///
    /// The report wraps its own text rather than letting Excel do it, because the code,
    /// reason and duration columns must stay line-for-line aligned: when a reason takes
    /// two lines, its code and duration cells need a matching blank line.
    /// </summary>
    public static class TextWrap
    {
        public static List<string> Wrap(string? text, int width)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            if (width < 1) width = 1;
            string current = string.Empty;

            foreach (string word in text!.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (candidate.Length <= width)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                string remaining = word;
                while (remaining.Length > width)
                {
                    lines.Add(remaining.Substring(0, width));
                    remaining = remaining.Substring(width);
                }
                current = remaining;
            }

            if (current.Length > 0) lines.Add(current);
            if (lines.Count == 0) lines.Add(string.Empty);
            return lines;
        }
    }
}
