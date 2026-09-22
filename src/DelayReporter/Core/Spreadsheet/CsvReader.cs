using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DelayReporter.Core.Spreadsheet
{
    /// <summary>RFC 4180 reader with delimiter sniffing, producing a <see cref="CellGrid"/>.</summary>
    public static class CsvReader
    {
        private static readonly char[] Candidates = { ',', ';', '\t', '|' };

        public static CellGrid ReadFile(string path)
        {
            using (var stream = File.OpenRead(path))
                return Read(stream, Path.GetFileName(path));
        }

        public static CellGrid Read(Stream stream, string sourceName)
        {
            string text;
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                text = reader.ReadToEnd();
            return ReadText(text, sourceName);
        }

        public static CellGrid ReadText(string text, string sourceName)
        {
            char delimiter = SniffDelimiter(text);
            return new CellGrid(Parse(text, delimiter), sourceName);
        }

        /// <summary>
        /// Picks the delimiter that yields the most consistent column count over the first
        /// lines, counting only separators outside quotes.
        /// </summary>
        public static char SniffDelimiter(string text)
        {
            char best = ',';
            int bestScore = -1;
            foreach (char candidate in Candidates)
            {
                var counts = Parse(text, candidate).Take(20).Select(r => r.Length).ToList();
                if (counts.Count == 0) continue;
                int max = counts.Max();
                if (max <= 1) continue;
                int agreeing = counts.Count(c => c == max);
                int score = max * 100 + agreeing;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private static IEnumerable<string[]> Parse(string text, char delimiter)
        {
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else quoted = false;
                    }
                    else field.Append(ch);
                    continue;
                }

                if (ch == '"' && field.Length == 0) { quoted = true; }
                else if (ch == delimiter) { row.Add(field.ToString()); field.Clear(); }
                else if (ch == '\r') { /* handled by \n */ }
                else if (ch == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    yield return row.ToArray();
                    row.Clear();
                }
                else field.Append(ch);
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                yield return row.ToArray();
            }
        }
    }
}
