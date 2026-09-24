using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DelayReporter.Core.Spreadsheet;

namespace DelayReporter.Core.Report
{
    /// <summary>
    /// The default layout, modelled on the printed movement delay reports stations already
    /// file: black on white, no gridlines or boxes, a centred title and period, then one tall
    /// row per flight with every cell centred vertically, and a thin rule closing each group
    /// of five flights.
    ///
    /// The columns, the summary and the stacking of delay codes are the classic layout's, so
    /// switching layouts never changes which flights are reported. What changes is the room:
    /// rows are tall enough to write on, and the Notes column is wide enough to write in.
    /// </summary>
    internal static class GroupedReportWriter
    {
        public const int ColDate = 0;
        public const int ColFlight = 1;
        public const int ColRegistration = 2;
        public const int ColFrom = 3;
        public const int ColTo = 4;
        public const int ColScheduled = 5;
        public const int ColActual = 6;
        public const int ColDelay = 7;
        public const int ColOperator = 8;
        public const int ColAircraft = 9;
        public const int ColCode = 10;
        public const int ColReason = 11;
        public const int ColDuration = 12;
        public const int ColNotes = 13;

        /// <summary>Hard wrap widths, a little under the column widths below.</summary>
        public const int ReasonWrapChars = 36;
        public const int DetailWrapChars = 130;

        /// <summary>
        /// The table's total width, in Excel character units. It stays the same whatever the
        /// options, so the page prints at the same scale; width the sized columns do not need
        /// goes to Notes.
        /// </summary>
        public const int TableWidth = 199;
        public const int MinimumNotesWidth = 30;

        /// <summary>
        /// OPR and Aircraft are sized to what the report holds: the narrowest width at which
        /// every value fits on this many lines, which a minimum-height row already has room for.
        /// </summary>
        public const int SizedColumnLines = 2;
        public const int MaximumSizedChars = 22;

        /// <summary>Space around the text in a sized column, since Excel wraps by pixels, not characters.</summary>
        private const int SizedColumnPadding = 2;

        public const string PromptTitle = "Supplementary information";

        /// <summary>
        /// Points per stacked line at 10pt, and the space added around the stack. The padding
        /// and the floor are what leave room to write: even a one-code flight gets a row about
        /// half an inch tall.
        /// </summary>
        public const double LineHeight = 13.5;
        public const double RowPadding = 16.0;
        public const double MinimumRowHeight = 36.0;

        /// <summary>Flights per group; a rule is drawn under the last flight of each.</summary>
        public const int GroupSize = 5;

        private const int SummaryHeadingRow = 5;
        private const int SummaryFirstRow = 6;
        private const int MaxSummaryCodes = 8;

        public static SheetSpec BuildSheet(ReportModel model)
        {
            var sheet = new SheetSpec { Name = "Departure delays" };
            int operatorChars = FitChars(model.Flights.Select(f => f.OperatorDisplay), "OPR");
            int aircraftChars = FitChars(model.Flights.Select(f => f.AircraftLabel), "Aircraft");
            AddColumns(sheet, operatorChars + SizedColumnPadding, aircraftChars + SizedColumnPadding);

            int last = sheet.LastColumn;

            sheet.Set(1, 0, "Departure Delay Report", CellStyle.GroupedTitle);
            sheet.Merge(1, 0, 1, last);
            sheet.SetHeight(1, 28);

            sheet.Set(2, 0, PeriodLine(model), CellStyle.GroupedPeriod);
            sheet.Merge(2, 0, 2, last);
            sheet.SetHeight(2, 18);

            sheet.Set(3, 0, ParametersLine(model), CellStyle.GroupedNote);
            sheet.Merge(3, 0, 3, last);
            sheet.SetHeight(3, 16);

            sheet.Set(SummaryHeadingRow, 0, "Summary", CellStyle.GroupedSection);
            for (int c = 1; c <= ColDelay; c++) sheet.Set(SummaryHeadingRow, c, string.Empty, CellStyle.GroupedSection);
            sheet.Set(SummaryHeadingRow, ColCode, "Delay codes by time", CellStyle.GroupedSection);
            for (int c = ColCode + 1; c <= last; c++) sheet.Set(SummaryHeadingRow, c, string.Empty, CellStyle.GroupedSection);
            sheet.SetHeight(SummaryHeadingRow, 20);

            int summaryRows = WriteSummary(sheet, model);

            // Two clear rows between the summary and the table set the flights apart.
            int headerRow = SummaryFirstRow + summaryRows + 2;
            WriteHeader(sheet, headerRow);
            sheet.HeaderRow = headerRow;

            int row = headerRow + 1;
            foreach (ReportFlight flight in model.Flights)
                row = WriteFlight(sheet, row, flight, operatorChars, aircraftChars);

            // An empty report still needs a valid filter range.
            sheet.LastRow = Math.Max(sheet.LastRow, headerRow);

            if (model.Flights.Count > 0)
                sheet.ConditionalRules.Add(GroupRule(headerRow + 1, sheet.LastRow, last));

            sheet.FooterText =
                "&L&\"Calibri,Regular\"&8Delay Reporter" +
                "&C&\"Calibri,Regular\"&8Page &P of &N" +
                "&R&\"Calibri,Regular\"&8" + model.Station;

            return sheet;
        }

        private static void AddColumns(SheetSpec sheet, int operatorWidth, int aircraftWidth)
        {
            sheet.Columns.Add(new ColumnSpec("Date", 11));
            sheet.Columns.Add(new ColumnSpec("MVT Nr", 10));
            sheet.Columns.Add(new ColumnSpec("Reg", 10));
            sheet.Columns.Add(new ColumnSpec("From", 6));
            sheet.Columns.Add(new ColumnSpec("To", 6));
            sheet.Columns.Add(new ColumnSpec("STD", 7));
            sheet.Columns.Add(new ColumnSpec("ATD", 8));
            // Wide enough for a flagged "0:32 ≠ 0:25" on one line.
            sheet.Columns.Add(new ColumnSpec("Delay", 11));
            sheet.Columns.Add(new ColumnSpec("OPR", operatorWidth));
            sheet.Columns.Add(new ColumnSpec("Aircraft", aircraftWidth));
            sheet.Columns.Add(new ColumnSpec("Code", 7));
            // Most reasons fit in 36 characters; the width they give up goes to Notes, which
            // is where the page is written on.
            sheet.Columns.Add(new ColumnSpec("Reason", 38));
            sheet.Columns.Add(new ColumnSpec("Dur", 7));

            double used = sheet.Columns.Sum(c => c.Width);
            sheet.Columns.Add(new ColumnSpec("Notes", Math.Max(MinimumNotesWidth, TableWidth - used)));
        }

        /// <summary>
        /// The narrowest width, in characters, at which every value wraps onto no more than
        /// <see cref="SizedColumnLines"/> lines: never narrower than the heading, and never wider
        /// than <see cref="MaximumSizedChars"/>, beyond which a long value takes a third line
        /// rather than squeezing Notes. "Boeing 767-300 Freighter" gives 14, as two lines.
        /// </summary>
        private static int FitChars(IEnumerable<string> values, string heading)
        {
            List<string> distinct = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
            for (int chars = heading.Length; chars < MaximumSizedChars; chars++)
            {
                if (distinct.All(v => TextWrap.Wrap(v, chars).Count <= SizedColumnLines))
                    return chars;
            }
            return MaximumSizedChars;
        }

        /// <summary>The line under the title, in the manner of "Period: ... LT" on the printed reports.</summary>
        private static string PeriodLine(ReportModel model)
        {
            string line = model.Station + " departures";
            if (model.PeriodText.Length > 0) line += "   ·   Period: " + model.PeriodText;
            return line;
        }

        /// <summary>What produced the report, small and grey beneath the period.</summary>
        private static string ParametersLine(ReportModel model)
        {
            var parts = new List<string>
            {
                "Minimum delay " + model.Options.MinimumDelayMinutes.ToString(CultureInfo.InvariantCulture) +
                " min (" + (model.Options.ThresholdBasis == DelayThresholdBasis.ActualDelay
                    ? "actual" : "included codes") + ")",
            };
            string mx = ReportSummary.MxFilterText(model.Options);
            if (mx.Length > 0) parts.Add(mx);
            if (model.SourceName.Length > 0) parts.Add("Source " + model.SourceName);
            parts.Add("Generated " + model.GeneratedUtc.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC");
            return string.Join("   ·   ", parts);
        }

        private static int WriteSummary(SheetSpec sheet, ReportModel model)
        {
            IReadOnlyList<KeyValuePair<string, string>> metrics = ReportSummary.Metrics(model);

            for (int i = 0; i < metrics.Count; i++)
            {
                int row = SummaryFirstRow + i;
                sheet.Set(row, 0, metrics[i].Key, CellStyle.GroupedLabel);
                sheet.Merge(row, 0, row, 2);
                sheet.Set(row, 3, metrics[i].Value, CellStyle.GroupedFigure);
                sheet.Merge(row, 3, row, 4);
            }

            List<CodeTally> top = model.CodeTallies.Take(MaxSummaryCodes).ToList();
            for (int i = 0; i < top.Count; i++)
            {
                int row = SummaryFirstRow + i;
                sheet.Set(row, ColCode, top[i].Code, CellStyle.GroupedText);
                sheet.Set(row, ColReason, top[i].Label, CellStyle.GroupedText);
                sheet.Set(row, ColDuration, top[i].Duration, CellStyle.GroupedText);
                sheet.Set(row, ColNotes, top[i].Events + (top[i].Events == 1 ? " event" : " events"), CellStyle.GroupedLabel);
            }

            int rows = Math.Max(metrics.Count, top.Count);
            for (int i = 0; i < rows; i++) sheet.SetHeight(SummaryFirstRow + i, 15);
            if (!model.Options.ShowDebugSummary) return rows;

            // One row per wrapped line of the detail, full width beneath both blocks.
            List<string> lines = ReportSummary.DetailLines(model, DetailWrapChars);
            int first = SummaryFirstRow + rows + 1;
            sheet.Set(first, 0, "Detail", CellStyle.GroupedLabel);
            sheet.Merge(first, 0, first, 2);
            for (int i = 0; i < lines.Count; i++)
            {
                sheet.Set(first + i, 3, lines[i], CellStyle.GroupedText);
                sheet.Merge(first + i, 3, first + i, sheet.LastColumn);
            }

            return rows + 1 + lines.Count;
        }

        private static void WriteHeader(SheetSpec sheet, int row)
        {
            for (int c = 0; c < sheet.Columns.Count; c++)
            {
                CellStyle style =
                    c == ColDuration ? CellStyle.GroupedHeadingRight
                    : IsCentred(c) ? CellStyle.GroupedHeadingCenter
                    : CellStyle.GroupedHeading;
                sheet.Set(row, c, sheet.Columns[c].Header, style);
            }
            sheet.SetHeight(row, 24);
        }

        /// <summary>Short fixed-width values read best centred under their heading.</summary>
        private static bool IsCentred(int column) =>
            column == ColDate || column == ColFrom || column == ColTo || column == ColScheduled ||
            column == ColActual || column == ColDelay || column == ColCode;

        private static int WriteFlight(SheetSpec sheet, int row, ReportFlight flight, int operatorChars, int aircraftChars)
        {
            var codeLines = new List<string>();
            var reasonLines = new List<string>();
            var durationLines = new List<string>();

            foreach (ReportDelayEvent e in flight.Events)
            {
                List<string> wrapped = TextWrap.Wrap(e.Label, ReasonWrapChars);
                codeLines.Add(e.Code);
                durationLines.Add(e.Duration);
                reasonLines.Add(wrapped[0]);

                // Blank code and duration lines keep each code beside its own reason. The
                // three cells are centred vertically together, so equal line counts keep them
                // level with each other and with the middle of the row.
                for (int i = 1; i < wrapped.Count; i++)
                {
                    codeLines.Add(string.Empty);
                    durationLines.Add(string.Empty);
                    reasonLines.Add(wrapped[i]);
                }
            }

            // OPR and Aircraft are wrapped by Excel, but at widths chosen from these same wraps,
            // so the row can be sized for them too.
            int lines = new[]
            {
                reasonLines.Count,
                TextWrap.Wrap(flight.OperatorDisplay, operatorChars).Count,
                TextWrap.Wrap(flight.AircraftLabel, aircraftChars).Count,
                1,
            }.Max();
            sheet.SetHeight(row, Math.Max(MinimumRowHeight, lines * LineHeight + RowPadding));

            sheet.Set(row, ColDate, flight.DateText, CellStyle.GroupedCellCenter);
            sheet.Set(row, ColFlight, flight.FlightNumber, CellStyle.GroupedCell);
            sheet.Set(row, ColRegistration, flight.Registration, CellStyle.GroupedCell);
            sheet.Set(row, ColFrom, flight.From, CellStyle.GroupedCellCenter);
            sheet.Set(row, ColTo, flight.To, CellStyle.GroupedCellCenter);
            sheet.Set(row, ColScheduled, flight.ScheduledText, CellStyle.GroupedCellCenter);
            sheet.Set(row, ColActual, flight.ActualText, CellStyle.GroupedCellCenter);

            // A flight whose codes do not add up to the clock delay is flagged rather than
            // silently trusted, with both figures as the preview shows them, so the mismatch
            // survives a black and white printer.
            sheet.Set(row, ColDelay, flight.DelayDisplay,
                      flight.Reconciles == false ? CellStyle.GroupedFlag : CellStyle.GroupedCellCenter);

            sheet.Set(row, ColOperator, flight.OperatorDisplay, CellStyle.GroupedCellWrap);
            sheet.Set(row, ColAircraft, flight.AircraftLabel, CellStyle.GroupedCellWrap);
            sheet.Set(row, ColCode, string.Join("\n", codeLines), CellStyle.GroupedCellStack);
            sheet.Set(row, ColReason, string.Join("\n", reasonLines), CellStyle.GroupedCellWrap);
            sheet.Set(row, ColDuration, string.Join("\n", durationLines), CellStyle.GroupedCellStackRight);
            // Notes is left empty to be written or typed in. What the codes oblige the station
            // to record is a hint Excel shows while the cell is selected; it never prints. The
            // summary still counts the flights that owe it, and the email lists it.
            sheet.Set(row, ColNotes, string.Empty, CellStyle.GroupedNotes);
            if (flight.SupplementaryPrompts.Count > 0)
                sheet.InputPrompts.Add(new InputPrompt(SheetSpec.CellName(row, ColNotes), PromptTitle,
                                                       string.Join("; ", flight.SupplementaryPrompts)));

            return row + 1;
        }

        /// <summary>
        /// A rule under every fifth visible flight. It is conditional formatting rather than a
        /// border on the cells, so it stays in fives when the sheet is sorted or filtered in
        /// Excel: SUBTOTAL(103, ...) counts only the visible, non-empty flight numbers from the
        /// first flight down to this row.
        /// </summary>
        private static ConditionalRule GroupRule(int firstRow, int lastRow, int lastColumn)
        {
            string first = firstRow.ToString(CultureInfo.InvariantCulture);
            string flightColumn = SheetSpec.ColumnName(ColFlight);
            string range = "A" + first + ":" + SheetSpec.ColumnName(lastColumn) + lastRow.ToString(CultureInfo.InvariantCulture);
            string formula = "MOD(SUBTOTAL(103,$" + flightColumn + "$" + first + ":$" + flightColumn + first + ")," +
                             GroupSize.ToString(CultureInfo.InvariantCulture) + ")=0";
            return new ConditionalRule(range, formula, DifferentialStyle.GroupRule);
        }
    }
}
