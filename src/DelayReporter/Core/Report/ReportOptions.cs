using System;
using System.Collections.Generic;
using System.Linq;
using DelayReporter.Core.Movement;

namespace DelayReporter.Core.Report
{
    /// <summary>What the minimum-delay threshold is measured against.</summary>
    public enum DelayThresholdBasis
    {
        /// <summary>Sum of the delay codes that survive the mapper's exclusion column.</summary>
        IncludedCodes = 0,

        /// <summary>The clock difference between STD and ATD.</summary>
        ActualDelay = 1,
    }

    /// <summary>How much of a mapped aircraft label the report prints.</summary>
    public enum AircraftLabelFormat
    {
        /// <summary>The model family alone, e.g. "767".</summary>
        Family = 0,

        /// <summary>The family and its variant when the label has one, e.g. "767-300".</summary>
        FamilyAndVariant = 1,

        /// <summary>The label as written in the mapping file, e.g. "Boeing 767-300 Freighter".</summary>
        Full = 2,
    }

    /// <summary>Which flights the MX classification lets through.</summary>
    public enum MxFilter
    {
        All = 0,

        /// <summary>Only flights counted as MX: a mapped MX code, or ticked by hand.</summary>
        MxOnly = 1,

        /// <summary>Only flights not counted as MX.</summary>
        NotMx = 2,
    }

    /// <summary>
    /// The column the flights are ordered by. The workbook and the email follow the same
    /// order as the preview, so sorting is a report option rather than a screen trick.
    /// </summary>
    public enum ReportSortColumn
    {
        /// <summary>Date, then scheduled time, then flight number: the natural reading order.</summary>
        Date = 0,
        Flight = 1,
        Registration = 2,
        Destination = 3,
        Scheduled = 4,
        ActualDelay = 5,
        CodedDelay = 6,
        Mx = 7,
        Operator = 8,
        Codes = 9,

        /// <summary>By how many things are still owed on the flight.</summary>
        Outstanding = 10,
    }

    /// <summary>Everything the user chooses before a report is generated.</summary>
    public sealed class ReportOptions
    {
        public const string DefaultStation = "CVG";
        public const int DefaultMinimumDelayMinutes = 15;

        public string Station { get; set; } = DefaultStation;

        /// <summary>Movement types to include. Empty means every type in the file.</summary>
        public HashSet<string> MovementTypes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        /// <summary>Operator codes to include. Empty means every operator.</summary>
        public HashSet<string> Operators { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Tail numbers to include. Empty means every registration.</summary>
        public HashSet<string> Registrations { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Delay codes to include, normalized. Empty means every code. A flight is kept
        /// when any of its reportable codes matches.
        /// </summary>
        public HashSet<string> DelayCodes { get; } = new HashSet<string>(StringComparer.Ordinal);

        public int MinimumDelayMinutes { get; set; } = DefaultMinimumDelayMinutes;

        public DelayThresholdBasis ThresholdBasis { get; set; } = DelayThresholdBasis.IncludedCodes;

        /// <summary>
        /// Per-flight corrections to the MX classification, keyed by the flight's source row
        /// number: true counts the flight as MX, false keeps it out. A flight with no entry
        /// takes its classification from the delay codes' category column. These live for the
        /// open file only and are never saved.
        /// </summary>
        public Dictionary<int, bool> MxOverrides { get; } = new Dictionary<int, bool>();

        /// <summary>
        /// Narrows the report to MX flights or to the rest, judged after the per-flight
        /// corrections above, so a flight ticked MX by hand counts as MX here too.
        /// </summary>
        public MxFilter MxFilter { get; set; } = MxFilter.All;

        /// <summary>
        /// A .NET date pattern for the Date column, such as "dd/MM/yyyy". Empty prints the date
        /// exactly as the movement sheet wrote it, as does a row whose date could not be read.
        /// </summary>
        public string DateFormat { get; set; } = string.Empty;

        /// <summary>The date patterns offered in the settings, the empty one meaning "as in the file".</summary>
        public static readonly string[] DateFormats = { string.Empty, "dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd MMM yyyy" };

        /// <summary>A date in the chosen pattern, or dd.MM.yyyy when printing as in the file.</summary>
        public string FormatDate(DateTime date) =>
            date.ToString(DateFormat.Length > 0 ? DateFormat : "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Flights the user hid by hand, keyed by source row number. A hidden flight is never
        /// reported, but it is counted on the summary, so hiding one is a visible decision
        /// rather than a silent gap. Lives for the open file only and is never saved.
        /// </summary>
        public HashSet<int> HiddenRows { get; } = new HashSet<int>();

        /// <summary>
        /// When not empty, only these flights are reported, keyed by source row number; every
        /// other flight that would have been reported is counted as not selected. Empty, the
        /// usual case, means no restriction. Set only for a one-off "report selected" run.
        /// </summary>
        public HashSet<int> SelectedRows { get; } = new HashSet<int>();

        /// <summary>
        /// Free text searched across every field of a flight; each word must appear somewhere.
        /// Empty means no search. Because only what is listed is reported, a search narrows the
        /// report like any other filter, and what it leaves out is counted on the summary.
        /// </summary>
        public string SearchText { get; set; } = string.Empty;

        public ReportSortColumn SortColumn { get; set; } = ReportSortColumn.Date;

        public bool SortDescending { get; set; }

        /// <summary>The OPR column prints the mapped carrier name rather than the raw code.</summary>
        public bool UseOperatorLabels { get; set; } = true;

        public AircraftLabelFormat AircraftFormat { get; set; } = AircraftLabelFormat.Full;

        /// <summary>
        /// Appends a line to the summary carrying every count the default summary leaves out,
        /// so a thin report can still be explained in full.
        /// </summary>
        public bool ShowDebugSummary { get; set; }

        /// <summary>
        /// Ground runs and tows carry no departure, no load and no delay codes, so the
        /// default selection is the movement types that are actual flights. The test is on
        /// the type's segments rather than a fixed list, so an unfamiliar type in a future
        /// export is included rather than silently dropped.
        /// </summary>
        public static bool IsFlightType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return true;
            foreach (string segment in type!.Split('/'))
            {
                string s = segment.Trim().ToUpperInvariant();
                if (s == "GR" || s == "XL") return false;
            }
            return true;
        }

        public static IEnumerable<string> DefaultMovementTypes(MovementSheet sheet) =>
            sheet.MovementTypes.Where(IsFlightType);

        public void SelectDefaultMovementTypes(MovementSheet sheet)
        {
            MovementTypes.Clear();
            foreach (string type in DefaultMovementTypes(sheet)) MovementTypes.Add(type);
        }

        public ReportOptions Clone()
        {
            var copy = new ReportOptions
            {
                Station = Station,
                DateFrom = DateFrom,
                DateTo = DateTo,
                MinimumDelayMinutes = MinimumDelayMinutes,
                ThresholdBasis = ThresholdBasis,
                UseOperatorLabels = UseOperatorLabels,
                AircraftFormat = AircraftFormat,
                ShowDebugSummary = ShowDebugSummary,
                SearchText = SearchText,
                MxFilter = MxFilter,
                DateFormat = DateFormat,
                SortColumn = SortColumn,
                SortDescending = SortDescending,
            };
            foreach (string v in MovementTypes) copy.MovementTypes.Add(v);
            foreach (string v in Operators) copy.Operators.Add(v);
            foreach (string v in Registrations) copy.Registrations.Add(v);
            foreach (string v in DelayCodes) copy.DelayCodes.Add(v);
            foreach (KeyValuePair<int, bool> pair in MxOverrides) copy.MxOverrides[pair.Key] = pair.Value;
            foreach (int row in HiddenRows) copy.HiddenRows.Add(row);
            foreach (int row in SelectedRows) copy.SelectedRows.Add(row);
            return copy;
        }
    }
}
