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
            };
            foreach (string v in MovementTypes) copy.MovementTypes.Add(v);
            foreach (string v in Operators) copy.Operators.Add(v);
            foreach (string v in Registrations) copy.Registrations.Add(v);
            foreach (string v in DelayCodes) copy.DelayCodes.Add(v);
            return copy;
        }
    }
}
