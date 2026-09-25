using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using DelayReporter.Core.Report;

namespace DelayReporter.Controls
{
    /// <summary>
    /// How the preview's Codes column shows a code the Delay codes filter did not choose.
    /// The workbook always prints every code, so its coded total still reconciles with the
    /// clock delay; this is a reading aid for the screen only.
    /// </summary>
    public enum UnselectedCodeDisplay
    {
        Visible = 0,
        Dimmed = 1,
        Hidden = 2,
    }

    /// <summary>One code and its duration within a flight's Codes cell.</summary>
    public sealed class CodePart
    {
        public CodePart(string text, bool isDimmed)
        {
            Text = text;
            IsDimmed = isDimmed;
        }

        /// <summary>
        /// "93A 0:17", led by the separator from the previous code: a TextBlock does not
        /// always make room for trailing spaces, but leading ones are kept.
        /// </summary>
        public string Text { get; }

        public bool IsDimmed { get; }
    }

    /// <summary>
    /// Turns a flight and the display mode into the parts of its Codes cell. Whether a code
    /// was chosen comes from the report model itself, so the cell always agrees with the
    /// filter the report was built with.
    /// </summary>
    public sealed class CodePartsConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Length < 2 || !(values[0] is ReportFlight flight)) return Array.Empty<CodePart>();
            var mode = values[1] is UnselectedCodeDisplay display ? display : UnselectedCodeDisplay.Visible;

            // A flight survives a restricting filter only through a chosen code, so hiding the
            // others never empties the cell.
            List<ReportDelayEvent> shown = flight.Events
                .Where(e => e.MatchesCodeFilter || mode != UnselectedCodeDisplay.Hidden)
                .ToList();

            var parts = new List<CodePart>(shown.Count);
            for (int i = 0; i < shown.Count; i++)
            {
                ReportDelayEvent e = shown[i];
                string text = (i > 0 ? ", " : string.Empty) + e.Code + " " + e.Duration;
                parts.Add(new CodePart(text, mode == UnselectedCodeDisplay.Dimmed && !e.MatchesCodeFilter));
            }
            return parts;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
