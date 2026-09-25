using System;
using System.Collections.Generic;
using System.Linq;

namespace DelayReporter.Core.Movement
{
    /// <summary>One movement as read from the sheet, before any report filtering.</summary>
    public sealed class MovementRow
    {
        public int SourceRowNumber { get; set; }
        public string Type { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public string Registration { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string EquipmentCode { get; set; } = string.Empty;

        public string DateText { get; set; } = string.Empty;
        public DateTime? Date { get; set; }

        public string ScheduledText { get; set; } = string.Empty;
        public string ActualText { get; set; } = string.Empty;
        public ClockTime? Scheduled { get; set; }
        public ClockTime? Actual { get; set; }

        public DelayParseResult Delay { get; set; } = DelayParseResult.Empty;

        /// <summary>Minutes from STD to ATD, or null when either time is unreadable.</summary>
        public int? ActualDelayMinutes =>
            Scheduled.HasValue && Actual.HasValue
                ? ClockTime.DelayMinutesBetween(Scheduled.Value, Actual.Value)
                : (int?)null;

        public bool HasCodedDelay => Delay.Events.Count > 0;

        public override string ToString() =>
            $"{DateText} {FlightNumber} {From}->{To}";
    }

    /// <summary>A parsed movement sheet: its header information, rows and any warnings.</summary>
    public sealed class MovementSheet
    {
        public string SourceName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        /// <summary>The "Period: ..." line exactly as written, for the report header.</summary>
        public string PeriodText { get; set; } = string.Empty;
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }

        public string WeightUnit { get; set; } = string.Empty;

        /// <summary>1-based row number of the located header row.</summary>
        public int HeaderRowNumber { get; set; }

        public List<MovementRow> Rows { get; } = new List<MovementRow>();
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>The station that dominates the From column, used to sanity-check the setting.</summary>
        public string DetectedStation { get; set; } = string.Empty;
        public int DetectedStationRowCount { get; set; }

        public IEnumerable<string> MovementTypes =>
            Rows.Select(r => r.Type)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> Operators =>
            Rows.Select(r => r.Operator)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> Stations =>
            Rows.Select(r => r.From)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> Registrations =>
            Rows.Select(r => r.Registration)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Distinct delay codes present in the file, normalized so the sheet's zero-padded
        /// "09" and a published list's "9" collapse to one entry.
        /// </summary>
        public IEnumerable<string> DelayCodes =>
            Rows.SelectMany(r => r.Delay.Events)
                .Select(e => Mapping.MappingTable.Normalize(e.Code))
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(c => c, StringComparer.Ordinal);
    }
}
