using System;
using System.Globalization;

namespace DelayReporter.Core.Movement
{
    /// <summary>
    /// A time of day from a movement sheet, such as "18:50" or "19:32 A".
    ///
    /// The sheet carries no date on its times, so a departure that slips past midnight
    /// shows an actual time earlier than the scheduled one. Delay is therefore measured
    /// forward from scheduled to actual, which is what
    /// <see cref="DelayMinutesBetween"/> does.
    /// </summary>
    public readonly struct ClockTime : IEquatable<ClockTime>
    {
        private ClockTime(int minutes, bool actual)
        {
            MinutesFromMidnight = minutes;
            IsActual = actual;
        }

        public int MinutesFromMidnight { get; }

        /// <summary>True when the sheet flagged the value as actual (a trailing "A").</summary>
        public bool IsActual { get; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}",
                MinutesFromMidnight / 60, MinutesFromMidnight % 60);

        public static bool TryParse(string? value, out ClockTime time)
        {
            time = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            string text = value!.Trim();
            bool actual = false;

            // Trailing status letter, e.g. "19:32 A".
            int space = text.IndexOf(' ');
            if (space > 0)
            {
                string suffix = text.Substring(space + 1).Trim();
                if (suffix.Length > 0 && char.IsLetter(suffix[0]))
                {
                    actual = suffix.StartsWith("A", StringComparison.OrdinalIgnoreCase);
                    text = text.Substring(0, space).Trim();
                }
            }

            int colon = text.IndexOf(':');
            if (colon <= 0) return false;

            if (!int.TryParse(text.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours))
                return false;
            if (!int.TryParse(text.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
                return false;
            if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) return false;

            time = new ClockTime(hours * 60 + minutes, actual);
            return true;
        }

        /// <summary>
        /// Minutes from <paramref name="scheduled"/> forward to <paramref name="actual"/>,
        /// wrapping over midnight. An actual time earlier in the clock than the scheduled
        /// one is read as the next day, never as a negative delay.
        /// </summary>
        public static int DelayMinutesBetween(ClockTime scheduled, ClockTime actual)
        {
            int delta = actual.MinutesFromMidnight - scheduled.MinutesFromMidnight;
            if (delta < 0) delta += 24 * 60;
            return delta;
        }

        public bool Equals(ClockTime other) =>
            MinutesFromMidnight == other.MinutesFromMidnight && IsActual == other.IsActual;

        public override bool Equals(object? obj) => obj is ClockTime other && Equals(other);

        public override int GetHashCode() => MinutesFromMidnight * 2 + (IsActual ? 1 : 0);
    }

    /// <summary>Formats a minute count as H:MM, the form used throughout the report.</summary>
    public static class DurationFormat
    {
        public static string Format(int minutes) =>
            string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes / 60, minutes % 60);
    }
}
