using System;
using System.Collections.Generic;
using System.Linq;

namespace DelayReporter.Core.Spreadsheet
{
    /// <summary>
    /// A sheet reduced to text. Every reader produces one of these, so the movement
    /// parser never learns whether the file was XLSX or CSV.
    /// </summary>
    public sealed class CellGrid
    {
        private readonly List<string[]> _rows;

        public CellGrid(IEnumerable<string[]> rows, string sourceName)
        {
            _rows = rows.ToList();
            SourceName = sourceName;
        }

        public string SourceName { get; }

        public int RowCount => _rows.Count;

        /// <summary>Cells of a row, 0-based. Never null; short rows read as empty.</summary>
        public string[] Row(int index) =>
            index >= 0 && index < _rows.Count ? _rows[index] : Array.Empty<string>();

        public string Cell(int row, int column)
        {
            var cells = Row(row);
            return column >= 0 && column < cells.Length ? cells[column] ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Locates the header row by matching column names rather than a fixed position,
        /// so an export with extra title rows, or with columns reordered, still reads.
        /// Returns -1 when no row contains all the required names.
        /// </summary>
        public int FindHeaderRow(IEnumerable<string> requiredNames, int searchLimit = 50)
        {
            var required = requiredNames.Select(Normalize).ToList();
            int limit = Math.Min(RowCount, searchLimit);
            for (int r = 0; r < limit; r++)
            {
                var present = new HashSet<string>(Row(r).Select(Normalize));
                present.Remove(string.Empty);
                if (required.All(present.Contains))
                    return r;
            }
            return -1;
        }

        /// <summary>Maps normalized header name to column index for the given header row.</summary>
        public Dictionary<string, int> HeaderMap(int headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            var cells = Row(headerRow);
            for (int c = 0; c < cells.Length; c++)
            {
                string key = Normalize(cells[c]);
                if (key.Length > 0 && !map.ContainsKey(key))
                    map[key] = c;
            }
            return map;
        }

        /// <summary>Header names are compared case-insensitively with whitespace collapsed.</summary>
        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var chars = new List<char>(value!.Length);
            bool lastSpace = false;
            foreach (char ch in value.Trim())
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastSpace) chars.Add(' ');
                    lastSpace = true;
                }
                else
                {
                    chars.Add(char.ToUpperInvariant(ch));
                    lastSpace = false;
                }
            }
            return new string(chars.ToArray());
        }
    }
}
