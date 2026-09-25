using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DelayReporter.Core.Spreadsheet;

namespace DelayReporter.Core.Report
{
    /// <summary>
    /// The layout used up to 0.3.1: a blue-banded summary, then one boxed row per flight.
    /// Kept unchanged behind <see cref="ReportLayout.Classic"/> so the grouped redesign can be
    /// compared against it and rolled back with one setting; docs/report-layout-classic.md
    /// records it. Delete this file and the enum value together once the redesign has settled.
    ///
    /// A flight is a single row so the sheet behaves like a spreadsheet — sort, filter and
    /// freeze all work per flight. Each flight's delay codes stack as lines inside the
    /// Code, Reason and Duration cells of that one row.
    /// </summary>
    internal static class ClassicReportWriter
    {
        // Column order and widths, in Excel character units.
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

        /// <summary>Hard wrap width for the reason column, a little under its width.</summary>
        public const int ReasonWrapChars = 44;
        public const int NotesWrapChars = 28;

        /// <summary>
        /// Hard wrap width for the summary's detail line, which spans from the fourth column to
        /// the last: comfortably under their combined width at the summary's 10pt.
        /// </summary>
        public const int DetailWrapChars = 130;

        /// <summary>Points per wrapped line at 10pt, plus a little padding per row.</summary>
        public const double LineHeight = 13.5;
        public const double RowPadding = 3.0;
        public const double MinimumRowHeight = 16.0;

        private const int SummaryFirstRow = 5;
        private const int MaxSummaryCodes = 8;

        public static SheetSpec BuildSheet(ReportModel model)
        {
            var sheet = new SheetSpec { Name = "Departure delays" };
            AddColumns(sheet, model.Options);

            int last = sheet.LastColumn;

            sheet.Set(1, 0, "DEPARTURE DELAY REPORT", CellStyle.Title);
            sheet.Merge(1, 0, 1, last);
            sheet.SetHeight(1, 26);

            sheet.Set(2, 0, Subtitle(model), CellStyle.Subtitle);
            sheet.Merge(2, 0, 2, last);
            sheet.SetHeight(2, 16);

            sheet.Set(4, 0, "SUMMARY", CellStyle.Band);
            sheet.Merge(4, 0, 4, ColDelay);
            sheet.Set(4, ColCode, "DELAY CODES BY TIME", CellStyle.Band);
            sheet.Merge(4, ColCode, 4, last);

            int summaryRows = WriteSummary(sheet, model);
            int headerRow = SummaryFirstRow + summaryRows + 1;

            WriteHeader(sheet, headerRow);
            sheet.HeaderRow = headerRow;

            int row = headerRow + 1;
            foreach (ReportFlight flight in model.Flights)
                row = WriteFlight(sheet, row, flight);

            // An empty report still needs a valid filter range.
            sheet.LastRow = Math.Max(sheet.LastRow, headerRow);

            sheet.FooterText =
                "&L&\"Calibri,Regular\"&8Delay Reporter" +
                "&C&\"Calibri,Regular\"&8Page &P of &N" +
                "&R&\"Calibri,Regular\"&8" + model.Station;

            return sheet;
        }

        private static void AddColumns(SheetSpec sheet, ReportOptions options)
        {
            sheet.Columns.Add(new ColumnSpec("Date", 10));
            sheet.Columns.Add(new ColumnSpec("MVT Nr", 10));
            sheet.Columns.Add(new ColumnSpec("Reg", 10));
            sheet.Columns.Add(new ColumnSpec("From", 6));
            sheet.Columns.Add(new ColumnSpec("To", 6));
            sheet.Columns.Add(new ColumnSpec("STD", 7));
            sheet.Columns.Add(new ColumnSpec("ATD", 7));
            sheet.Columns.Add(new ColumnSpec("Delay", 8));
            // A carrier name needs room a three-letter code does not.
            sheet.Columns.Add(new ColumnSpec("OPR", options.UseOperatorLabels ? 18 : 6));
            sheet.Columns.Add(new ColumnSpec("Aircraft", 20));
            sheet.Columns.Add(new ColumnSpec("Code", 7));
            sheet.Columns.Add(new ColumnSpec("Reason", 46));
            sheet.Columns.Add(new ColumnSpec("Dur", 7));
            sheet.Columns.Add(new ColumnSpec("Notes", 30));
        }

        private static string Subtitle(ReportModel model)
        {
            var parts = new List<string> { "Station " + model.Station };
            if (model.PeriodText.Length > 0) parts.Add("Period " + model.PeriodText);
            if (model.SourceName.Length > 0) parts.Add("Source " + model.SourceName);
            parts.Add("Generated " + model.GeneratedUtc.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC");
            parts.Add("Minimum delay " + model.Options.MinimumDelayMinutes.ToString(CultureInfo.InvariantCulture) +
                      " min (" + (model.Options.ThresholdBasis == DelayThresholdBasis.ActualDelay
                          ? "actual" : "included codes") + ")");
            string mx = ReportSummary.MxFilterText(model.Options);
            if (mx.Length > 0) parts.Add(mx);
            return string.Join("   ·   ", parts);
        }

        private static int WriteSummary(SheetSpec sheet, ReportModel model)
        {
            IReadOnlyList<KeyValuePair<string, string>> metrics = ReportSummary.Metrics(model);

            for (int i = 0; i < metrics.Count; i++)
            {
                int row = SummaryFirstRow + i;
                sheet.Set(row, 0, metrics[i].Key, CellStyle.Label);
                sheet.Merge(row, 0, row, 2);
                sheet.Set(row, 3, metrics[i].Value, CellStyle.Value);
                sheet.Merge(row, 3, row, 4);
            }

            List<CodeTally> top = model.CodeTallies.Take(MaxSummaryCodes).ToList();
            for (int i = 0; i < top.Count; i++)
            {
                int row = SummaryFirstRow + i;
                sheet.Set(row, ColCode, top[i].Code, CellStyle.Value);
                sheet.Set(row, ColReason, top[i].Label, CellStyle.Value);
                sheet.Set(row, ColDuration, top[i].Duration, CellStyle.Value);
                sheet.Set(row, ColNotes, top[i].Events + " events", CellStyle.Value);
            }

            int rows = Math.Max(metrics.Count, top.Count);
            if (!model.Options.ShowDebugSummary) return rows;

            // The detail line runs the full width beneath both blocks, one row per wrapped
            // line: the summary styles do not wrap, and the text is ours to wrap anyway.
            List<string> lines = ReportSummary.DetailLines(model, DetailWrapChars);
            int first = SummaryFirstRow + rows;
            sheet.Set(first, 0, "Detail", CellStyle.Label);
            sheet.Merge(first, 0, first, 2);
            for (int i = 0; i < lines.Count; i++)
            {
                sheet.Set(first + i, 3, lines[i], CellStyle.Value);
                sheet.Merge(first + i, 3, first + i, sheet.LastColumn);
            }

            return rows + lines.Count;
        }

        private static void WriteHeader(SheetSpec sheet, int row)
        {
            for (int c = 0; c < sheet.Columns.Count; c++)
                sheet.Set(row, c, sheet.Columns[c].Header, CellStyle.Header);
            sheet.SetHeight(row, 22);
        }

        private static int WriteFlight(SheetSpec sheet, int row, ReportFlight flight)
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

                // Blank code and duration lines keep each code beside its own reason.
                for (int i = 1; i < wrapped.Count; i++)
                {
                    codeLines.Add(string.Empty);
                    durationLines.Add(string.Empty);
                    reasonLines.Add(wrapped[i]);
                }
            }

            string notes = string.Join("; ", flight.SupplementaryPrompts);
            int noteLines = notes.Length > 0 ? TextWrap.Wrap(notes, NotesWrapChars).Count : 1;

            int lines = Math.Max(Math.Max(reasonLines.Count, noteLines), 1);
            sheet.SetHeight(row, Math.Max(MinimumRowHeight, lines * LineHeight + RowPadding));

            sheet.Set(row, ColDate, flight.DateText, CellStyle.BodyCenter);
            sheet.Set(row, ColFlight, flight.FlightNumber, CellStyle.Body);
            sheet.Set(row, ColRegistration, flight.Registration, CellStyle.Body);
            sheet.Set(row, ColFrom, flight.From, CellStyle.BodyCenter);
            sheet.Set(row, ColTo, flight.To, CellStyle.BodyCenter);
            sheet.Set(row, ColScheduled, flight.ScheduledText, CellStyle.BodyCenter);
            sheet.Set(row, ColActual, flight.ActualText, CellStyle.BodyCenter);

            // A flight whose codes do not add up to the clock delay is flagged rather than
            // silently trusted.
            sheet.Set(row, ColDelay, flight.ActualDelayText,
                      flight.Reconciles == false ? CellStyle.BodyFlag : CellStyle.BodyCenter);

            sheet.Set(row, ColOperator, flight.OperatorDisplay, CellStyle.BodyCenter);
            sheet.Set(row, ColAircraft, flight.AircraftLabel, CellStyle.BodyWrap);
            sheet.Set(row, ColCode, string.Join("\n", codeLines), CellStyle.BodyStack);
            sheet.Set(row, ColReason, string.Join("\n", reasonLines), CellStyle.BodyWrap);
            sheet.Set(row, ColDuration, string.Join("\n", durationLines), CellStyle.BodyStack);
            sheet.Set(row, ColNotes, notes, CellStyle.Notes);

            return row + 1;
        }
    }
}
