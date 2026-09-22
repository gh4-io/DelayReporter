using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DelayReporter.Core.Movement
{
    /// <summary>One coded delay: a reason code and how long it accounted for.</summary>
    public sealed class DelayEvent
    {
        public DelayEvent(string code, int minutes)
        {
            Code = code;
            Minutes = minutes;
        }

        /// <summary>The code exactly as the sheet wrote it, e.g. "93A" or "09".</summary>
        public string Code { get; }

        public int Minutes { get; }

        public string Duration => DurationFormat.Format(Minutes);

        public override string ToString() => Code + " " + Duration;
    }

    public sealed class DelayParseResult
    {
        public DelayParseResult(IReadOnlyList<DelayEvent> events, string raw, string? warning)
        {
            Events = events;
            Raw = raw;
            Warning = warning;
        }

        public IReadOnlyList<DelayEvent> Events { get; }

        /// <summary>The original cell text, kept so a report can always show what was there.</summary>
        public string Raw { get; }

        /// <summary>Set when the cell could not be read cleanly. Never fatal.</summary>
        public string? Warning { get; }

        public bool HasWarning => Warning != null;

        public int TotalMinutes => Events.Sum(e => e.Minutes);

        public static DelayParseResult Empty { get; } =
            new DelayParseResult(Array.Empty<DelayEvent>(), string.Empty, null);
    }

    /// <summary>
    /// Reads the movement sheet's delay column.
    ///
    /// The cell packs N reason codes followed by N durations, slash separated, paired in
    /// order: "93A/09/28A/00:17/00:15/00:10" means 93A for 17 minutes, 09 for 15 and 28A
    /// for 10. Durations are the trailing HH:MM tokens; everything before them is a code.
    /// </summary>
    public static class DelayCodeParser
    {
        public static DelayParseResult Parse(string? cell)
        {
            if (string.IsNullOrWhiteSpace(cell)) return DelayParseResult.Empty;

            string raw = cell!.Trim();
            var tokens = raw.Split('/')
                            .Select(t => t.Trim())
                            .Where(t => t.Length > 0)
                            .ToList();

            if (tokens.Count == 0)
                return new DelayParseResult(Array.Empty<DelayEvent>(), raw, "Delay value contained no readable tokens.");

            var codes = new List<string>();
            var durations = new List<int>();
            bool seenDuration = false;
            bool interleaved = false;

            foreach (string token in tokens)
            {
                if (TryParseDuration(token, out int minutes))
                {
                    durations.Add(minutes);
                    seenDuration = true;
                }
                else
                {
                    if (seenDuration) interleaved = true;   // a code after a duration
                    codes.Add(token);
                }
            }

            string? warning = null;
            if (interleaved)
                warning = "Codes and durations are interleaved; they are expected as all codes then all durations.";
            else if (codes.Count == 0)
                warning = "Delay value has durations but no codes.";
            else if (durations.Count == 0)
                warning = "Delay value has codes but no durations.";
            else if (codes.Count != durations.Count)
                warning = string.Format(CultureInfo.InvariantCulture,
                    "Delay value has {0} code(s) but {1} duration(s); they are paired as far as they match.",
                    codes.Count, durations.Count);

            int pairs = Math.Min(codes.Count, durations.Count);
            var events = new List<DelayEvent>(pairs);
            for (int i = 0; i < pairs; i++)
                events.Add(new DelayEvent(codes[i], durations[i]));

            // Codes with no duration still happened; keep them at zero so they are reported.
            for (int i = pairs; i < codes.Count; i++)
                events.Add(new DelayEvent(codes[i], 0));

            // Durations with no code still happened; keep the minutes under an empty code
            // (resolved as unmapped) rather than dropping them from the flight's total.
            for (int i = pairs; i < durations.Count; i++)
                events.Add(new DelayEvent(string.Empty, durations[i]));

            return new DelayParseResult(events, raw, warning);
        }

        /// <summary>Matches HH:MM and H:MM, the only duration form the sheet uses.</summary>
        public static bool TryParseDuration(string token, out int minutes)
        {
            minutes = 0;
            int colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1) return false;

            string hourPart = token.Substring(0, colon);
            string minutePart = token.Substring(colon + 1);

            if (hourPart.Length > 3 || minutePart.Length != 2) return false;
            if (!hourPart.All(char.IsDigit) || !minutePart.All(char.IsDigit)) return false;

            int hours = int.Parse(hourPart, CultureInfo.InvariantCulture);
            int mins = int.Parse(minutePart, CultureInfo.InvariantCulture);
            if (mins > 59) return false;

            minutes = hours * 60 + mins;
            return true;
        }
    }
}
