using System;
using System.Collections.Generic;
using System.Linq;
using DelayReporter.Core.Spreadsheet;

namespace DelayReporter.Core.Mapping
{
    /// <summary>One row of a mapping CSV.</summary>
    public sealed class MappingEntry
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// When set, matching delay events are left out of the report. A flight keeps its
        /// remaining codes; a flight whose codes are all excluded leaves the report.
        /// </summary>
        public bool Exclude { get; set; }

        /// <summary>The code obliges the station to record supplementary information.</summary>
        public bool SupplementaryRequired { get; set; }

        /// <summary>What that information is, e.g. "record ULD ID".</summary>
        public string SupplementaryRemark { get; set; } = string.Empty;
    }

    /// <summary>
    /// A code-to-label table loaded from a user-editable CSV.
    ///
    /// Lookup is normalized, because the movement sheet zero-pads codes the published code
    /// list does not: the sheet's "09" and the list's "9" are the same code.
    /// </summary>
    public sealed class MappingTable
    {
        private readonly Dictionary<string, MappingEntry> _entries =
            new Dictionary<string, MappingEntry>(StringComparer.Ordinal);

        public MappingTable(string name) { Name = name; }

        public string Name { get; }

        public IReadOnlyCollection<MappingEntry> Entries => _entries.Values;

        public int Count => _entries.Count;

        public List<string> LoadWarnings { get; } = new List<string>();

        public MappingEntry? Find(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            return _entries.TryGetValue(Normalize(code!), out MappingEntry entry) ? entry : null;
        }

        public bool IsExcluded(string? code) => Find(code)?.Exclude == true;

        /// <summary>The mapped label, or the code itself when it is not in the table.</summary>
        public string Label(string? code)
        {
            MappingEntry? entry = Find(code);
            return entry != null && entry.Label.Length > 0 ? entry.Label : (code ?? string.Empty);
        }

        public bool IsMapped(string? code) => Find(code) != null;

        public void Add(MappingEntry entry)
        {
            string key = Normalize(entry.Code);
            if (key.Length == 0) return;
            _entries[key] = entry;
        }

        /// <summary>
        /// Upper-cases and strips leading zeros from the numeric part, so "09", "9" and
        /// "9 " are one key, while "93A" keeps its suffix.
        /// </summary>
        public static string Normalize(string code)
        {
            string text = code.Trim().ToUpperInvariant();
            if (text.Length == 0) return text;

            int digits = 0;
            while (digits < text.Length && char.IsDigit(text[digits])) digits++;
            if (digits == 0) return text;

            string numeric = text.Substring(0, digits).TrimStart('0');
            if (numeric.Length == 0) numeric = "0";
            return numeric + text.Substring(digits);
        }

        /// <summary>
        /// Reads a mapping CSV. Columns are found by name, so their order may change and
        /// extra columns are ignored: only "code" is mandatory.
        /// </summary>
        public static MappingTable FromCsv(string name, string csvText)
        {
            var table = new MappingTable(name);
            CellGrid grid = CsvReader.ReadText(csvText, name);

            int header = grid.FindHeaderRow(new[] { "CODE" });
            if (header < 0)
            {
                table.LoadWarnings.Add($"{name}: no header row with a 'code' column was found; the file was ignored.");
                return table;
            }

            var map = grid.HeaderMap(header);
            for (int r = header + 1; r < grid.RowCount; r++)
            {
                string code = Cell(grid, r, map, "CODE");
                if (code.Length == 0) continue;
                if (code.StartsWith("#", StringComparison.Ordinal)) continue;   // comment row

                table.Add(new MappingEntry
                {
                    Code = code,
                    Label = Cell(grid, r, map, "LABEL"),
                    Exclude = IsTrue(Cell(grid, r, map, "EXCLUDE")),
                    SupplementaryRequired = IsTrue(Cell(grid, r, map, "SI_REQUIRED")),
                    SupplementaryRemark = Cell(grid, r, map, "SI_REMARK"),
                });
            }

            if (table.Count == 0)
                table.LoadWarnings.Add($"{name}: the file contained no usable rows.");

            return table;
        }

        private static string Cell(CellGrid grid, int row, IReadOnlyDictionary<string, int> map, string column) =>
            map.TryGetValue(column, out int index) ? grid.Cell(row, index).Trim() : string.Empty;

        /// <summary>Accepts the spellings a person is likely to type into a spreadsheet.</summary>
        public static bool IsTrue(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            switch (value!.Trim().ToUpperInvariant())
            {
                case "Y":
                case "YES":
                case "TRUE":
                case "1":
                case "X":
                    return true;
                default:
                    return false;
            }
        }
    }
}
