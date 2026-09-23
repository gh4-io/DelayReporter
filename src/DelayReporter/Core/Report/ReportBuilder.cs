using System;
using System.Collections.Generic;
using System.Linq;
using DelayReporter.Core.Mapping;
using DelayReporter.Core.Movement;

namespace DelayReporter.Core.Report
{
    /// <summary>
    /// Narrows a movement sheet to this station's coded departure delays and resolves the
    /// codes through the mapping tables.
    ///
    /// The order of the filters is deliberate: the counts recorded on the way describe
    /// where rows went, so a thin report can be explained rather than guessed at.
    /// </summary>
    public static class ReportBuilder
    {
        public static ReportModel Build(MovementSheet sheet, MappingStore mappings, ReportOptions options)
        {
            var model = new ReportModel
            {
                Station = options.Station.Trim().ToUpperInvariant(),
                SourceName = sheet.SourceName,
                PeriodText = sheet.PeriodText,
                Options = options.Clone(),
                RowsRead = sheet.Rows.Count,
            };

            model.Warnings.AddRange(sheet.Warnings);
            model.Warnings.AddRange(mappings.Warnings);

            if (sheet.DetectedStation.Length > 0 &&
                !sheet.DetectedStation.Equals(model.Station, StringComparison.OrdinalIgnoreCase))
            {
                model.Warnings.Add(
                    $"The station is set to {model.Station}, but most rows in this file depart from " +
                    $"{sheet.DetectedStation} ({sheet.DetectedStationRowCount} of {sheet.Rows.Count}). " +
                    "Check the station setting if the report looks empty.");
            }

            // A delay-codes.csv from before the category column existed marks nothing MX, and the
            // user's file always wins over the seed. Filtering on MX would then quietly leave
            // only hand-ticked flights, so the report says why.
            if (options.MxFilter != MxFilter.All &&
                !mappings.DelayCodes.Entries.Any(e => e.IsCategory(ReportFlight.MxCategory)))
            {
                model.Warnings.Add(
                    "No code in delay-codes.csv has the category MX, so only flights ticked MX by hand count as MX. " +
                    "Add a category column to that file and put MX against the maintenance codes (the 40s), " +
                    "or delete the file to have it written afresh with them marked.");
            }

            foreach (MovementRow row in sheet.Rows)
            {
                if (!IsStationDeparture(row, model.Station))
                {
                    model.ExcludedNotStationDeparture++;
                    continue;
                }
                model.StationDepartures++;

                if (options.MovementTypes.Count > 0 && !options.MovementTypes.Contains(row.Type))
                {
                    model.ExcludedByMovementType++;
                    continue;
                }

                if (!MatchesDate(row, options))
                {
                    model.ExcludedByDate++;
                    continue;
                }
                if (!MatchesSet(options.Operators, row.Operator))
                {
                    model.ExcludedByOperator++;
                    continue;
                }
                if (!MatchesSet(options.Registrations, row.Registration))
                {
                    model.ExcludedByRegistration++;
                    continue;
                }

                if (!row.HasCodedDelay)
                {
                    model.ExcludedNoCodedDelay++;
                    continue;
                }
                model.FlightsWithCodedDelay++;

                ReportFlight flight = Resolve(row, mappings, model, options);

                if (flight.Events.Count == 0)
                {
                    model.FlightsDroppedAllCodesExcluded++;
                    continue;
                }

                if (options.DelayCodes.Count > 0 &&
                    !flight.Events.Any(e => options.DelayCodes.Contains(MappingTable.Normalize(e.Code))))
                {
                    model.ExcludedByDelayCode++;
                    continue;
                }

                if (!MeetsThreshold(flight, options))
                {
                    model.ExcludedByThreshold++;
                    continue;
                }

                if (options.MxFilter != MxFilter.All &&
                    flight.IsMxDelay != (options.MxFilter == MxFilter.MxOnly))
                {
                    model.ExcludedByMx++;
                    continue;
                }

                // The search and the hand decisions come last, so a flight is only ever counted
                // against them when every other filter would have reported it.
                if (!flight.MatchesSearch(options.SearchText))
                {
                    model.ExcludedBySearch++;
                    continue;
                }

                if (options.HiddenRows.Contains(row.SourceRowNumber))
                {
                    flight.IsHidden = true;
                    model.HiddenFlights.Add(flight);
                    continue;
                }

                if (options.SelectedRows.Count > 0 && !options.SelectedRows.Contains(row.SourceRowNumber))
                {
                    model.ExcludedNotSelected++;
                    continue;
                }

                model.Flights.Add(flight);
            }

            Comparison<ReportFlight> order = Ordering(options);
            model.Flights.Sort(order);
            model.HiddenFlights.Sort(order);
            model.FlightsIncludingHidden.AddRange(model.Flights.Concat(model.HiddenFlights));
            model.FlightsIncludingHidden.Sort(order);

            BuildTallies(model);
            return model;
        }

        /// <summary>
        /// A departure from this station. A movement that both starts and ends here is a
        /// ground run or a tow, not a departure, so it is not counted.
        /// </summary>
        private static bool IsStationDeparture(MovementRow row, string station)
        {
            if (station.Length == 0) return true;
            if (!string.Equals(row.From, station, StringComparison.OrdinalIgnoreCase)) return false;
            return !string.Equals(row.To, station, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesDate(MovementRow row, ReportOptions options)
        {
            if (options.DateFrom == null && options.DateTo == null) return true;
            if (row.Date == null) return true;      // undated rows are not filtered out silently
            if (options.DateFrom != null && row.Date.Value.Date < options.DateFrom.Value.Date) return false;
            if (options.DateTo != null && row.Date.Value.Date > options.DateTo.Value.Date) return false;
            return true;
        }

        private static bool MatchesSet(HashSet<string> allowed, string value) =>
            allowed.Count == 0 || allowed.Contains(value);

        private static ReportFlight Resolve(MovementRow row, MappingStore mappings, ReportModel model,
                                            ReportOptions options)
        {
            var flight = new ReportFlight
            {
                SourceRowNumber = row.SourceRowNumber,
                Date = row.Date,
                DateText = options.DateFormat.Length > 0 && row.Date.HasValue
                    ? options.FormatDate(row.Date.Value)
                    : row.DateText,
                FlightNumber = row.FlightNumber,
                Registration = row.Registration,
                From = row.From,
                To = row.To,
                ScheduledText = row.ScheduledText,
                ActualText = row.ActualText,
                Operator = row.Operator,
                EquipmentCode = row.EquipmentCode,
                ActualDelayMinutes = row.ActualDelayMinutes,
                RawDelayText = row.Delay.Raw,
                Warning = row.Delay.Warning,
            };

            MappingEntry? aircraft = mappings.AircraftTypes.Find(row.EquipmentCode);
            flight.AircraftUnmapped = aircraft == null;
            // Only a mapped label is shortened; a raw EQP code is not a label to parse.
            flight.AircraftLabel = aircraft != null && aircraft.Label.Length > 0
                ? AircraftLabels.Format(aircraft.Label, options.AircraftFormat)
                : row.EquipmentCode;

            MappingEntry? oprEntry = mappings.Operators.Find(row.Operator);
            flight.OperatorUnmapped = oprEntry == null;
            flight.OperatorLabel = oprEntry != null && oprEntry.Label.Length > 0
                ? oprEntry.Label
                : row.Operator;
            flight.OperatorDisplay = options.UseOperatorLabels ? flight.OperatorLabel : flight.Operator;

            if (options.MxOverrides.TryGetValue(row.SourceRowNumber, out bool mx))
                flight.MxOverride = mx;

            foreach (DelayEvent source in row.Delay.Events)
            {
                MappingEntry? entry = mappings.DelayCodes.Find(source.Code);
                if (entry != null && entry.Exclude)
                {
                    model.ExcludedEvents++;
                    continue;
                }

                flight.Events.Add(new ReportDelayEvent
                {
                    Code = source.Code,
                    Label = entry != null && entry.Label.Length > 0 ? entry.Label : "(unmapped)",
                    Minutes = source.Minutes,
                    IsUnmapped = entry == null,
                    Category = entry?.Category ?? string.Empty,
                    MatchesCodeFilter = options.DelayCodes.Count == 0 ||
                                        options.DelayCodes.Contains(MappingTable.Normalize(source.Code)),
                });

                if (entry != null && entry.SupplementaryRequired)
                {
                    string prompt = entry.SupplementaryRemark.Length > 0
                        ? entry.SupplementaryRemark
                        : "supplementary information required";
                    if (!flight.SupplementaryPrompts.Contains(prompt))
                        flight.SupplementaryPrompts.Add(prompt);
                }
            }

            return flight;
        }

        private static bool MeetsThreshold(ReportFlight flight, ReportOptions options)
        {
            if (options.MinimumDelayMinutes <= 0) return true;

            int measured = options.ThresholdBasis == DelayThresholdBasis.ActualDelay
                ? flight.ActualDelayMinutes ?? flight.CodedMinutes
                : flight.CodedMinutes;

            return measured >= options.MinimumDelayMinutes;
        }

        /// <summary>
        /// The chosen column first, in the chosen direction, then the natural reading order
        /// and finally the source row, so equal values always come out in the same order and
        /// the preview, workbook and email agree row for row.
        /// </summary>
        public static Comparison<ReportFlight> Ordering(ReportOptions options)
        {
            Comparison<ReportFlight> primary = Primary(options.SortColumn);
            int direction = options.SortDescending ? -1 : 1;
            return (a, b) =>
            {
                int result = direction * primary(a, b);
                if (result != 0) return result;
                result = CompareFlights(a, b);
                return result != 0 ? result : a.SourceRowNumber.CompareTo(b.SourceRowNumber);
            };
        }

        private static Comparison<ReportFlight> Primary(ReportSortColumn column)
        {
            StringComparer text = StringComparer.OrdinalIgnoreCase;
            switch (column)
            {
                case ReportSortColumn.Flight: return (a, b) => text.Compare(a.FlightNumber, b.FlightNumber);
                case ReportSortColumn.Registration: return (a, b) => text.Compare(a.Registration, b.Registration);
                case ReportSortColumn.Destination: return (a, b) => text.Compare(a.To, b.To);
                case ReportSortColumn.Scheduled: return (a, b) => string.CompareOrdinal(a.ScheduledText, b.ScheduledText);
                case ReportSortColumn.ActualDelay: return (a, b) => Nullable.Compare(a.ActualDelayMinutes, b.ActualDelayMinutes);
                case ReportSortColumn.CodedDelay: return (a, b) => a.CodedMinutes.CompareTo(b.CodedMinutes);
                case ReportSortColumn.Mx: return (a, b) => a.IsMxDelay.CompareTo(b.IsMxDelay);
                case ReportSortColumn.Operator: return (a, b) => text.Compare(a.OperatorDisplay, b.OperatorDisplay);
                case ReportSortColumn.Codes: return (a, b) => text.Compare(a.CodeSummary, b.CodeSummary);
                case ReportSortColumn.Outstanding: return (a, b) => a.OutstandingItems.Count.CompareTo(b.OutstandingItems.Count);
                default: return CompareFlights;
            }
        }

        private static int CompareFlights(ReportFlight a, ReportFlight b)
        {
            int byDate = Nullable.Compare(a.Date, b.Date);
            if (byDate != 0) return byDate;

            int byTime = string.CompareOrdinal(a.ScheduledText, b.ScheduledText);
            if (byTime != 0) return byTime;

            return string.CompareOrdinal(a.FlightNumber, b.FlightNumber);
        }

        private static void BuildTallies(ReportModel model)
        {
            var codes = new Dictionary<string, CodeTally>(StringComparer.Ordinal);
            foreach (ReportFlight flight in model.Flights)
            {
                foreach (ReportDelayEvent e in flight.Events)
                {
                    string key = MappingTable.Normalize(e.Code);
                    if (!codes.TryGetValue(key, out CodeTally? tally))
                    {
                        tally = new CodeTally { Code = e.Code, Label = e.Label, IsUnmapped = e.IsUnmapped };
                        codes[key] = tally;
                    }
                    tally.Events++;
                    tally.Minutes += e.Minutes;
                }
            }

            model.CodeTallies.AddRange(codes.Values
                .OrderByDescending(c => c.Minutes)
                .ThenByDescending(c => c.Events)
                .ThenBy(c => c.Code, StringComparer.Ordinal));

            var operators = new Dictionary<string, OperatorTally>(StringComparer.OrdinalIgnoreCase);
            foreach (ReportFlight flight in model.Flights)
            {
                string key = flight.Operator.Length > 0 ? flight.Operator : "(none)";
                if (!operators.TryGetValue(key, out OperatorTally? tally))
                {
                    tally = new OperatorTally { Operator = key };
                    operators[key] = tally;
                }
                tally.Flights++;
                tally.Events += flight.Events.Count;
                tally.Minutes += flight.CodedMinutes;
            }

            model.OperatorTallies.AddRange(operators.Values
                .OrderByDescending(o => o.Minutes)
                .ThenBy(o => o.Operator, StringComparer.OrdinalIgnoreCase));
        }
    }
}
