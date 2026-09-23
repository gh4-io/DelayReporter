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

        /// <summary>The code's category from the mapping file, e.g. "MX"; empty when it has none.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// False when the Delay codes filter is restricting and this code is not one of those
        /// chosen. The workbook prints every code regardless; only the preview uses this.
        /// </summary>
        public bool MatchesCodeFilter { get; set; } = true;

        public bool IsMx => Category.Equals(ReportFlight.MxCategory, StringComparison.OrdinalIgnoreCase);

        public string Duration => DurationFormat.Format(Minutes);
    }

    /// <summary>One flight: one row of the report.</summary>
    public sealed class ReportFlight
    {
        /// <summary>The delay code category that marks a maintenance delay.</summary>
        public const string MxCategory = "MX";

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
        public string OperatorLabel { get; set; } = string.Empty;
        public bool OperatorUnmapped { get; set; }

        /// <summary>What the OPR column shows: the carrier name or the raw code, per the options.</summary>
        public string OperatorDisplay { get; set; } = string.Empty;

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

        /// <summary>
        /// The user's correction for this flight: true forces MX, false excludes it, null
        /// leaves the classification to the codes.
        /// </summary>
        public bool? MxOverride { get; set; }

        /// <summary>Any reported code on this flight carries the MX category.</summary>
        public bool IsMxCoded => Events.Any(e => e.IsMx);

        /// <summary>Whether the flight counts as an MX delay once any override is applied.</summary>
        public bool IsMxDelay => MxOverride ?? IsMxCoded;

        /// <summary>Explains the MX classification, for the preview's tooltip.</summary>
        public string MxDescription
        {
            get
            {
                if (MxOverride == true) return "Forced MX for this file. Click to exclude it.";
                if (MxOverride == false) return "Excluded from MX for this file. Click to return to automatic.";

                List<string> codes = Events.Where(e => e.IsMx).Select(e => e.Code).Distinct().ToList();
                string auto = codes.Count > 0
                    ? "Automatic: MX, from code " + string.Join(", ", codes) + "."
                    : "Automatic: no MX code on this flight.";
                return auto + " Click to force MX.";
            }
        }
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
        // RowsRead = StationDepartures + ExcludedNotStationDeparture, and StationDepartures
        // breaks down fully into every exclusion reason below plus ReportedFlights.
        public int RowsRead { get; set; }
        public int StationDepartures { get; set; }

        /// <summary>Arrivals, other-station rows, and same-station ground runs/tows.</summary>
        public int ExcludedNotStationDeparture { get; set; }

        public int ExcludedByMovementType { get; set; }
        public int ExcludedByDate { get; set; }
        public int ExcludedByOperator { get; set; }
        public int ExcludedByRegistration { get; set; }
        public int ExcludedNoCodedDelay { get; set; }
        public int FlightsWithCodedDelay { get; set; }
        public int FlightsDroppedAllCodesExcluded { get; set; }
        public int ExcludedByDelayCode { get; set; }
        public int ExcludedByThreshold { get; set; }
        public int ExcludedEvents { get; set; }

        public int ReportedFlights => Flights.Count;
        public int ReportedEvents => Flights.Sum(f => f.Events.Count);
        public int TotalCodedMinutes => Flights.Sum(f => f.CodedMinutes);
        public string TotalCodedDelayText => DurationFormat.Format(TotalCodedMinutes);

        /// <summary>
        /// Reported flights counted as maintenance delays: an MX-category code survived the
        /// mapper's exclusion column, or the user forced the flight in, and was not excluded.
        /// </summary>
        public int FlightsWithMxDelay => Flights.Count(f => f.IsMxDelay);

        /// <summary>Reported flights whose MX classification the user overrode.</summary>
        public int MxOverriddenFlights => Flights.Count(f => f.MxOverride.HasValue);

        public int FlightsRequiringSupplementary => Flights.Count(f => f.SupplementaryPrompts.Count > 0);
        public int ReconciliationMismatches => Flights.Count(f => f.Reconciles == false);

        public IEnumerable<CodeTally> UnmappedCodes => CodeTallies.Where(c => c.IsUnmapped);

        public IEnumerable<string> UnmappedAircraft =>
            Flights.Where(f => f.AircraftUnmapped && f.EquipmentCode.Length > 0)
                   .Select(f => f.EquipmentCode)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> UnmappedOperators =>
            Flights.Where(f => f.OperatorUnmapped && f.Operator.Length > 0)
                   .Select(f => f.Operator)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);
    }
}
