using System;
using System.Text.RegularExpressions;

namespace DelayReporter.Core.Report
{
    /// <summary>
    /// Shortens a mapped aircraft label to its model number.
    ///
    /// The seeded labels read "&lt;Manufacturer&gt; &lt;Family&gt;[-&lt;Variant&gt;] Freighter", e.g.
    /// "Boeing 767-300 Freighter" or "Boeing 777 Freighter", but the file is the user's to
    /// edit, so the family is found by shape rather than by position: the first word that
    /// looks like a model number. A label with no such word is returned whole. This never
    /// throws and never shortens a label to nothing.
    /// </summary>
    public static class AircraftLabels
    {
        // An optional letter prefix (A330, E190, MD-11), two to four digits and an optional
        // letter suffix (747SP, 777F), then an optional "-variant" (767-300, 737-800BCF).
        private static readonly Regex ModelNumber = new Regex(
            @"^(?<family>[A-Z]{0,2}-?\d{2,4}[A-Z]{0,2})(?:-(?<variant>[A-Z0-9]+))?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Format(string? label, AircraftLabelFormat format)
        {
            string full = label?.Trim() ?? string.Empty;
            if (format == AircraftLabelFormat.Full || full.Length == 0) return full;

            foreach (string word in full.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Match match = ModelNumber.Match(word.Trim('(', ')', ',', ';', '.'));
                if (!match.Success) continue;

                string family = match.Groups["family"].Value;
                Group variant = match.Groups["variant"];
                return format == AircraftLabelFormat.FamilyAndVariant && variant.Success
                    ? family + "-" + variant.Value
                    : family;
            }

            return full;
        }
    }
}
