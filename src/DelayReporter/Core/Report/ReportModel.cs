using System;
using System.Collections.Generic;
using System.Linq;
using DelayReporter.Core.Movement;

namespace DelayReporter.Core.Report
{
    /// <summary>One reportable delay on a flight, with its code resolved.</summary>
    public sealed class ReportDelayEvent
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Minutes { get; set; }
        public bool IsUnmapped { get; set; }
        public string Duration => DurationFormat.Format(Minutes);
    }

    /// <summary>One flight: one row of the report.</summary>
    public sealed class ReportFlight
    {
        public int SourceRowNumber { get; set; }
        public DateTime? Date { get; set; }
        public string DateText { get; set; } = string.Empty;
        public string FlightNumber { get; set; } = string.Empty;
        public string Registration { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string ScheduledText { get; set; } = string.Empty;
        public string ActualText { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public string EquipmentCode { get; set; } = string.Empty;
        public string AircraftLabel { get; set; } = string.Empty;
        public bool AircraftUnmapped { get; set; }

        public List<ReportDelayEvent> Events { get; } = new List<ReportDelayEvent>();

        public int? ActualDelayMinutes { get; set; }
        public int CodedMinutes => Events.Sum(e => e.Minutes);

        public string ActualDelayText =>
            ActualDelayMinutes.HasValue ? DurationFormat.Format(ActualDelayMinutes.Value) : string.Empty;

        public string CodedDelayText => DurationFormat.Format(CodedMinutes);

        /// <summary>
        /// True when the coded durations add up to the clock delay, false when they do not,
        /// null when the clock delay could not be computed.
        /// </summary>
        public bool? Reconciles =>
            ActualDelayMinutes.HasValue ? CodedMinutes == ActualDelayMinutes.Value : (bool?)null;

        /// <summary>Supplementary information the codes on this flight oblige, if any.</summary>
        public List<string> SupplementaryPrompts { get; } = new List<string>();

        /// <summary>Parse warning carried from the source cell, if any.</summary>
        public string? Warning { get; set; }

        public string RawDelayText { get; set; } = string.Empty;

        /// <summary>One line describing the codes, for the on screen preview.</summary>
        public string CodeSummary =>
            string.Join(", ", Events.Select(e => e.Code + " " + e.Duration));
    }

    public sealed class CodeTally
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int Events { get; set; }
        public int Minutes { get; set; }
        public bool IsUnmapped { get; set; }
        public string Duration => DurationFormat.Format(Minutes);
    }

    public sealed class OperatorTally
    {
        public string Operator { get; set; } = string.Empty;
        public int Flights { get; set; }
        public int Events { get; set; }
        public int Minutes { get; set; }
        public string Duration => DurationFormat.Format(Minutes);
    }

    /// <summary>
    /// The finished report, independent of how it is rendered. The workbook writer is one
    /// consumer; anything added later reads the same model.
    /// </summary>
    public sealed class ReportModel
    {
        public string Station { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string PeriodText { get; set; } = string.Empty;
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;
        public ReportOptions Options { get; set; } = new ReportOptions();

        public List<ReportFlight> Flights { get; } = new List<ReportFlight>();
        public List<CodeTally> CodeTallies { get; } = new List<CodeTally>();
        public List<OperatorTally> OperatorTallies { get; } = new List<OperatorTally>();
        public List<string> Warnings { get; } = new List<string>();

        // Counts describing how the source was narrowed down, so the report is auditable.
        public int RowsRead { get; set; }
        public int StationDepartures { get; set; }
        public int ExcludedByMovementType { get; set; }
        public int ExcludedByFilters { get; set; }
        public int FlightsWithCodedDelay { get; set; }
        public int ExcludedByThreshold { get; set; }
        public int ExcludedEvents { get; set; }
        public int FlightsDroppedAllCodesExcluded { get; set; }

        public int ReportedFlights => Flights.Count;
        public int ReportedEvents => Flights.Sum(f => f.Events.Count);
        public int TotalCodedMinutes => Flights.Sum(f => f.CodedMinutes);
        public string TotalCodedDelayText => DurationFormat.Format(TotalCodedMinutes);

        public int FlightsRequiringSupplementary => Flights.Count(f => f.SupplementaryPrompts.Count > 0);
        public int ReconciliationMismatches => Flights.Count(f => f.Reconciles == false);

        public IEnumerable<CodeTally> UnmappedCodes => CodeTallies.Where(c => c.IsUnmapped);

        public IEnumerable<string> UnmappedAircraft =>
            Flights.Where(f => f.AircraftUnmapped && f.EquipmentCode.Length > 0)
                   .Select(f => f.EquipmentCode)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);
    }
}
