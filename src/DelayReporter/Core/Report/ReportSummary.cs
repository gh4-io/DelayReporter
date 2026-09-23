using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DelayReporter.Core.Report
{
    /// <summary>
    /// The summary figures, worded once for both the workbook and the preview so the two
    /// cannot drift apart.
    ///
    /// The default figures follow a flight from departure to the report. Every other count
    /// lives in the detail line, shown only when <see cref="ReportOptions.ShowDebugSummary"/>
    /// is set: nothing that leaves the report goes uncounted, it is just not in the way.
    /// </summary>
    public static class ReportSummary
    {
        public const string Separator = "   ·   ";

        /// <summary>
        /// The default figures. Flights left out by a search, hidden by hand or left out of a
        /// "report selected" run join the list whenever they apply: those are decisions a person
        /// made about this particular report, so they belong on its face, not in the detail.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Metrics(ReportModel model)
        {
            var metrics = new List<KeyValuePair<string, string>>
            {
                Metric(model.Station + " departures", model.StationDepartures),
                Metric("No coded delay", model.ExcludedNoCodedDelay),
                Metric("With coded delay", model.FlightsWithCodedDelay),
                Metric("With MX coded delay", model.FlightsWithMxDelay),
                Metric("Below threshold", Flights(model.ExcludedByThreshold)),
            };
            if (model.Options.SearchText.Trim().Length > 0)
                metrics.Add(Metric("Not matching \"" + model.Options.SearchText.Trim() + "\"", Flights(model.ExcludedBySearch)));
            if (model.ExcludedHidden > 0)
                metrics.Add(Metric("Hidden by hand", Flights(model.ExcludedHidden)));
            if (model.ExcludedNotSelected > 0)
                metrics.Add(Metric("Not selected", Flights(model.ExcludedNotSelected)));
            metrics.Add(Metric("Flights reported", model.ReportedFlights));
            metrics.Add(Metric("Delay events", model.ReportedEvents));
            return metrics;
        }

        public static IReadOnlyList<KeyValuePair<string, string>> Detail(ReportModel model) =>
            new List<KeyValuePair<string, string>>
            {
                Metric("Rows read", model.RowsRead),
                Metric("Not a " + model.Station + " departure", model.ExcludedNotStationDeparture),
                Metric("Excluded by movement type", model.ExcludedByMovementType),
                Metric("by date", model.ExcludedByDate),
                Metric("by operator", model.ExcludedByOperator),
                Metric("by tail number", model.ExcludedByRegistration),
                Metric("by delay code", model.ExcludedByDelayCode),
                Metric("by MX filter", model.ExcludedByMx),
                Metric("by search", model.ExcludedBySearch),
                Metric("hidden by hand", model.ExcludedHidden),
                Metric("not selected", model.ExcludedNotSelected),
                Metric("Dropped, all codes excluded", model.FlightsDroppedAllCodesExcluded),
                Metric("Excluded by mapper", model.ExcludedEvents + " events"),
                Metric("Total coded delay", model.TotalCodedDelayText),
                Metric("Flights needing SI", model.FlightsRequiringSupplementary),
                Metric("Coded/actual mismatches", model.ReconciliationMismatches),
                Metric("MX overrides", model.MxOverriddenFlights),
                Metric("Unmapped codes", List(model.UnmappedCodes.Select(c => c.Code))),
                Metric("Unmapped aircraft", List(model.UnmappedAircraft)),
                Metric("Unmapped operators", List(model.UnmappedOperators)),
            };

        /// <summary>"Key value   ·   key value", the one-line form both renderings share.</summary>
        public static string Line(IEnumerable<KeyValuePair<string, string>> metrics) =>
            string.Join(Separator, metrics.Select(m => m.Key + " " + m.Value));

        public static string DetailLine(ReportModel model) => Line(Detail(model));

        /// <summary>
        /// The detail line broken into rows of at most <paramref name="width"/> characters,
        /// only ever between items, so a label is never parted from its value. An item
        /// longer than the width, such as a long list of unmapped codes, is wrapped alone.
        /// </summary>
        public static List<string> DetailLines(ReportModel model, int width)
        {
            var lines = new List<string>();
            string current = string.Empty;
            foreach (KeyValuePair<string, string> metric in Detail(model))
            {
                string item = metric.Key + " " + metric.Value;
                string candidate = current.Length == 0 ? item : current + Separator + item;
                if (candidate.Length <= width)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0) lines.Add(current);
                if (item.Length <= width)
                {
                    current = item;
                    continue;
                }

                List<string> wrapped = TextWrap.Wrap(item, width);
                lines.AddRange(wrapped.Take(wrapped.Count - 1));
                current = wrapped[wrapped.Count - 1];
            }
            if (current.Length > 0) lines.Add(current);
            return lines;
        }

        /// <summary>
        /// "MX delays only" or "MX delays left out" when the MX filter is set, empty otherwise.
        /// Printed with the report's own description, so a narrowed report says it is one.
        /// </summary>
        public static string MxFilterText(ReportOptions options) =>
            options.MxFilter == MxFilter.MxOnly ? "MX delays only"
            : options.MxFilter == MxFilter.NotMx ? "MX delays left out"
            : string.Empty;

        private static string Flights(int count) => count == 1 ? "1 flight" : count.ToString(CultureInfo.InvariantCulture) + " flights";

        private static string List(IEnumerable<string> values)
        {
            string text = string.Join(", ", values);
            return text.Length > 0 ? text : "none";
        }

        private static KeyValuePair<string, string> Metric(string label, int value) =>
            new KeyValuePair<string, string>(label, value.ToString(CultureInfo.InvariantCulture));

        private static KeyValuePair<string, string> Metric(string label, string value) =>
            new KeyValuePair<string, string>(label, value);
    }
}
