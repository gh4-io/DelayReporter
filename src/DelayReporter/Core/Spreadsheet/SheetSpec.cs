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

        // The classic layout.
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

        // The grouped layout: black on white, no borders except the rules, and every cell
        // centred vertically so a flight's single values sit level with the middle of its
        // stacked codes.
        GroupedTitle = 13,
        GroupedPeriod = 14,
        GroupedNote = 15,
        GroupedSection = 16,
        GroupedLabel = 17,
        GroupedFigure = 18,
        GroupedText = 19,
        GroupedHeading = 20,
        GroupedHeadingCenter = 21,
        GroupedHeadingRight = 22,
        GroupedCell = 23,
        GroupedCellCenter = 24,
        GroupedCellWrap = 25,
        GroupedCellStack = 26,
        GroupedCellStackRight = 27,
        GroupedNotes = 28,
        GroupedFlag = 29,
    }

    /// <summary>
    /// Differential styles for conditional formatting. The order is the order of &lt;dxfs&gt;
    /// in <see cref="XlsxStyles"/>.
    /// </summary>
    public enum DifferentialStyle
    {
        /// <summary>A thin grey rule under the cell, closing a group of flights.</summary>
        GroupRule = 0,
    }

    /// <summary>
    /// A hint Excel shows beside a cell while it is selected: a data validation that restricts
    /// nothing and carries only an input message. The cell itself stays empty, and the hint
    /// never prints.
    /// </summary>
    public sealed class InputPrompt
    {
        /// <summary>Excel's limits: 32 characters of title, 255 of message.</summary>
        public const int MaxTitle = 32;
        public const int MaxText = 255;

        public InputPrompt(string cell, string title, string text)
        {
            Cell = cell;
            Title = title.Length > MaxTitle ? title.Substring(0, MaxTitle) : title;
            Text = text.Length > MaxText ? text.Substring(0, MaxText - 1) + "…" : text;
        }

        public string Cell { get; }
        public string Title { get; }
        public string Text { get; }
    }

    /// <summary>A formula rule applied over a range, styled when the formula is true.</summary>
    public sealed class ConditionalRule
    {
        public ConditionalRule(string range, string formula, DifferentialStyle style)
        {
            Range = range;
            Formula = formula;
            Style = style;
        }

        /// <summary>An A1 range such as "A18:N60"; relative references in the formula are relative to its top-left cell.</summary>
        public string Range { get; }
        public string Formula { get; }
        public DifferentialStyle Style { get; }
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

        public List<ConditionalRule> ConditionalRules { get; } = new List<ConditionalRule>();

        public List<InputPrompt> InputPrompts { get; } = new List<InputPrompt>();

        /// <summary>1-based row and 0-based column to an A1 reference: (19, 13) -> N19.</summary>
        public static string CellName(int row, int column) =>
            ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);

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
