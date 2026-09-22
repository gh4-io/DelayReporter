using System;
using System.Collections.Generic;
using System.Globalization;

namespace DelayReporter.Core.Spreadsheet
{
    /// <summary>
    /// Style slots the writer understands. The order is the order of &lt;cellXfs&gt; in
    /// <see cref="XlsxStyles"/>; the two must be changed together.
    /// </summary>
    public enum CellStyle
    {
        Default = 0,
        Title = 1,
        Subtitle = 2,
        Band = 3,
        Label = 4,
        Value = 5,
        Header = 6,
        Body = 7,
        BodyCenter = 8,
        BodyStack = 9,
        BodyWrap = 10,
        Notes = 11,
        BodyFlag = 12,
    }

    public readonly struct Cell
    {
        public Cell(string text, CellStyle style)
        {
            Text = text;
            Style = style;
        }

        public string Text { get; }
        public CellStyle Style { get; }
    }

    public sealed class ColumnSpec
    {
        public ColumnSpec(string header, double width)
        {
            Header = header;
            Width = width;
        }

        public string Header { get; }
        public double Width { get; }
    }

    /// <summary>
    /// A sheet described in full before anything is written: cells by row and column,
    /// merges, row heights and the print settings. Keeping this separate from the writer
    /// means the layout can be asserted in tests without producing a file.
    /// </summary>
    public sealed class SheetSpec
    {
        public string Name { get; set; } = "Sheet1";

        public List<ColumnSpec> Columns { get; } = new List<ColumnSpec>();

        public Dictionary<int, Dictionary<int, Cell>> Rows { get; } =
            new Dictionary<int, Dictionary<int, Cell>>();

        public Dictionary<int, double> RowHeights { get; } = new Dictionary<int, double>();

        public List<string> Merges { get; } = new List<string>();

        /// <summary>1-based row carrying the table header; also the last repeated print row.</summary>
        public int HeaderRow { get; set; }

        public int LastRow { get; set; }

        public string? FooterText { get; set; }

        public void Set(int row, int column, string? text, CellStyle style = CellStyle.Default)
        {
            if (!Rows.TryGetValue(row, out Dictionary<int, Cell>? cells))
            {
                cells = new Dictionary<int, Cell>();
                Rows[row] = cells;
            }
            cells[column] = new Cell(text ?? string.Empty, style);
            if (row > LastRow) LastRow = row;
        }

        public void SetHeight(int row, double points) => RowHeights[row] = points;

        public void Merge(int row1, int column1, int row2, int column2) =>
            Merges.Add(ColumnName(column1) + row1.ToString(CultureInfo.InvariantCulture) + ":" +
                       ColumnName(column2) + row2.ToString(CultureInfo.InvariantCulture));

        public int LastColumn => Columns.Count - 1;

        /// <summary>0-based column index to its spreadsheet letters: 0 -> A, 27 -> AB.</summary>
        public static string ColumnName(int index)
        {
            string name = string.Empty;
            int value = index + 1;
            while (value > 0)
            {
                int remainder = (value - 1) % 26;
                name = (char)('A' + remainder) + name;
                value = (value - 1) / 26;
            }
            return name;
        }
    }
}
