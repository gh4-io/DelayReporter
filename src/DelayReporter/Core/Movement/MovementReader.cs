using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DelayReporter.Core.Spreadsheet;

namespace DelayReporter.Core.Movement
{
    /// <summary>
    /// Turns a <see cref="CellGrid"/> into a <see cref="MovementSheet"/>.
    ///
    /// The header row is located by column name rather than position, so title rows above
    /// it, or a reordered export, do not break the read. Rows below the table that are not
    /// movements (the "Total Weight In:" footer) are recognised and skipped.
    /// </summary>
    public static class MovementReader
    {
        public const string ColType = "TYPE";
        public const string ColFlight = "MVT NR";
        public const string ColRegistration = "REG";
        public const string ColFrom = "FROM";
        public const string ColScheduled = "STD";
        public const string ColActual = "ATD";
        public const string ColTo = "TO";
        public const string ColOperator = "OPR";
        public const string ColEquipment = "EQP";
        public const string ColDate = "DATE";
        public const string ColDepartureDelay = "DEP DELAY";

        /// <summary>Without these the sheet cannot produce a departure delay report.</summary>
        public static readonly string[] RequiredColumns =
        {
            ColFlight, ColFrom, ColTo, ColScheduled, ColActual, ColDate, ColDepartureDelay
        };

        private static readonly string[] DateFormats =
        {
            "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy",
            "yyyy-MM-dd", "dd-MM-yyyy", "MM/dd/yyyy"
        };

        public static MovementSheet ReadFile(string path)
        {
            string extension = Path.GetExtension(path);
            CellGrid grid = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
                         || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                         || extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase)
                ? CsvReader.ReadFile(path)
                : XlsxReader.ReadFile(path);
            return Read(grid);
        }

        public static MovementSheet Read(CellGrid grid)
        {
            var sheet = new MovementSheet { SourceName = grid.SourceName };

            int headerRow = grid.FindHeaderRow(RequiredColumns);
            if (headerRow < 0)
            {
                throw new SpreadsheetFormatException(
                    "No header row was found. A movement sheet needs columns named " +
                    string.Join(", ", RequiredColumns) + ".");
            }

            sheet.HeaderRowNumber = headerRow + 1;
            ReadTitleRows(grid, headerRow, sheet);

            var map = grid.HeaderMap(headerRow);
            for (int r = headerRow + 1; r < grid.RowCount; r++)
            {
                if (IsFooterRow(grid, r, map)) continue;

                MovementRow? row = ReadRow(grid, r, map);
                if (row != null) sheet.Rows.Add(row);
            }

            DetectStation(sheet);
            return sheet;
        }

        private static void ReadTitleRows(CellGrid grid, int headerRow, MovementSheet sheet)
        {
            for (int r = 0; r < headerRow; r++)
            {
                string text = grid.Cell(r, 0).Trim();
                if (text.Length == 0) continue;

                if (text.StartsWith("Period:", StringComparison.OrdinalIgnoreCase))
                {
                    sheet.PeriodText = text.Substring("Period:".Length).Trim();
                    ParsePeriod(sheet);
                }
                else if (text.StartsWith("Weight unit:", StringComparison.OrdinalIgnoreCase))
                {
                    sheet.WeightUnit = text.Substring("Weight unit:".Length).Trim();
                }
                else if (sheet.Title.Length == 0)
                {
                    sheet.Title = text;
                }
            }
        }

        /// <summary>Reads "14.09.2026 00:00 - 20.09.2026 23:59 UTC" into start and end.</summary>
        private static void ParsePeriod(MovementSheet sheet)
        {
            string text = sheet.PeriodText;

            // Split on a spaced dash so a date written as 2026-09-14 is not torn apart.
            int dash = text.IndexOf(" - ", StringComparison.Ordinal);
            if (dash < 0) return;

            sheet.PeriodStart = ParseDateTime(text.Substring(0, dash));
            sheet.PeriodEnd = ParseDateTime(text.Substring(dash + 3));
        }

        private static DateTime? ParseDateTime(string text)
        {
            string[] parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            DateTime? date = ParseDate(parts[0]);
            if (date == null) return null;

            if (parts.Length > 1 && ClockTime.TryParse(parts[1], out ClockTime time))
                return date.Value.AddMinutes(time.MinutesFromMidnight);

            return date;
        }

        public static DateTime? ParseDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (DateTime.TryParseExact(text!.Trim(), DateFormats, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out DateTime parsed))
                return parsed;
            return null;
        }

        /// <summary>
        /// The sheet ends with a totals line rather than a movement. It is recognised by a
        /// leading "Total" label instead of a movement type, so it is skipped without
        /// assuming it is the last row.
        /// </summary>
        private static bool IsFooterRow(CellGrid grid, int row, IReadOnlyDictionary<string, int> map)
        {
            string first = grid.Cell(row, 0).Trim();
            if (first.StartsWith("Total", StringComparison.OrdinalIgnoreCase)) return true;

            // A row with no flight number and no date carries no movement.
            string flight = Value(grid, row, map, ColFlight);
            string date = Value(grid, row, map, ColDate);
            return flight.Length == 0 && date.Length == 0;
        }

        private static MovementRow? ReadRow(CellGrid grid, int row, IReadOnlyDictionary<string, int> map)
        {
            var movement = new MovementRow
            {
                SourceRowNumber = row + 1,
                Type = Value(grid, row, map, ColType),
                FlightNumber = Value(grid, row, map, ColFlight),
                Registration = Value(grid, row, map, ColRegistration),
                From = Value(grid, row, map, ColFrom).ToUpperInvariant(),
                To = Value(grid, row, map, ColTo).ToUpperInvariant(),
                Operator = Value(grid, row, map, ColOperator),
                EquipmentCode = Value(grid, row, map, ColEquipment),
                DateText = Value(grid, row, map, ColDate),
                ScheduledText = Value(grid, row, map, ColScheduled),
                ActualText = Value(grid, row, map, ColActual),
            };

            movement.Date = ParseDate(movement.DateText);

            if (ClockTime.TryParse(movement.ScheduledText, out ClockTime scheduled))
                movement.Scheduled = scheduled;
            if (ClockTime.TryParse(movement.ActualText, out ClockTime actual))
                movement.Actual = actual;

            movement.Delay = DelayCodeParser.Parse(Value(grid, row, map, ColDepartureDelay));

            return movement;
        }

        private static string Value(CellGrid grid, int row, IReadOnlyDictionary<string, int> map, string column) =>
            map.TryGetValue(column, out int index) ? grid.Cell(row, index).Trim() : string.Empty;

        private static void DetectStation(MovementSheet sheet)
        {
            var counts = sheet.Rows
                .Select(r => r.From)
                .Where(f => f.Length > 0)
                .GroupBy(f => f, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (counts == null) return;
            sheet.DetectedStation = counts.Key;
            sheet.DetectedStationRowCount = counts.Count();
        }
    }
}
