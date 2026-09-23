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

                ReportFlight flight = Resolve(row, mappings, model);

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

                model.Flights.Add(flight);
            }

            model.Flights.Sort(CompareFlights);
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

        private static ReportFlight Resolve(MovementRow row, MappingStore mappings, ReportModel model)
        {
            var flight = new ReportFlight
            {
                SourceRowNumber = row.SourceRowNumber,
                Date = row.Date,
                DateText = row.DateText,
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
            flight.AircraftLabel = aircraft != null && aircraft.Label.Length > 0
                ? aircraft.Label
                : row.EquipmentCode;

            MappingEntry? oprEntry = mappings.Operators.Find(row.Operator);
            flight.OperatorUnmapped = oprEntry == null;
            flight.OperatorLabel = oprEntry != null && oprEntry.Label.Length > 0
                ? oprEntry.Label
                : row.Operator;

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
