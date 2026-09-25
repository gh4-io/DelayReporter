using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DelayReporter.Core.Report;

namespace DelayReporter.Core
{
    /// <summary>
    /// A named set of report filters. The date range is never part of one, because it
    /// belongs to the file rather than to the way a station reports.
    ///
    /// Every list follows the filter convention: empty means no restriction. A freshly opened
    /// file starts as the Standard preset describes it: every movement type and tail number
    /// ticked (no restriction), the station's own carriers ticked under operators, and no
    /// delay codes ticked.
    /// </summary>
    public sealed class ReportPreset
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Empty leaves the current station alone, as every built-in preset does.</summary>
        public string Station { get; set; } = string.Empty;

        public int MinimumDelayMinutes { get; set; } = ReportOptions.DefaultMinimumDelayMinutes;
        public DelayThresholdBasis ThresholdBasis { get; set; } = DelayThresholdBasis.IncludedCodes;
        public MxFilter MxFilter { get; set; } = MxFilter.All;
        public List<string> MovementTypes { get; } = new List<string>();
        public List<string> Operators { get; } = new List<string>();
        public List<string> DelayCodes { get; } = new List<string>();
        public List<string> Registrations { get; } = new List<string>();
    }

    /// <summary>The fixed presets. Built in code so they cannot be deleted or corrupted.</summary>
    public static class BuiltInPresets
    {
        public const string Standard = "Standard";
        public const string All = "All";
        public const string MxOnly = "MX only";
        public const string EveryCodedDelay = "Every coded delay";
        public const string OverAnHour = "Over an hour";

        public static readonly string[] Names = { Standard, All, MxOnly, EveryCodedDelay, OverAnHour };

        /// <summary>
        /// The station's own carriers. A freshly opened file starts with these ticked under
        /// operators, as Standard and MX only do. Only those the file holds can be ticked; a file
        /// holding none of them is left unrestricted, since nothing ticked means everything.
        /// </summary>
        public static readonly IReadOnlyList<string> StationOperators =
            new[] { "3S", "CJT", "CKS", "CSB", "DHK", "KII", "SIA" };

        public static bool IsBuiltIn(string name) =>
            Names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        public static string Description(string name)
        {
            string carriers = string.Join(", ", StationOperators);
            if (Is(name, All)) return "15 minutes of included delay codes, every operator, no other filter";
            if (Is(name, MxOnly)) return "Standard, narrowed to MX delays: the station's own carriers (" + carriers + ") and 15 minutes of included delay codes";
            if (Is(name, EveryCodedDelay)) return "Every flight carrying a delay code, however short, with no other filter";
            if (Is(name, OverAnHour)) return "Flights that left an hour or more late by the clock, with no other filter";
            return "How a file opens: the station's own carriers (" + carriers + "), 15 minutes of included delay codes, no other filter";
        }

        public static ReportPreset? Build(string name)
        {
            if (Is(name, Standard))
                return WithStationOperators(new ReportPreset { Name = Standard });
            if (Is(name, All))
                return new ReportPreset { Name = All };
            if (Is(name, MxOnly))
                return WithStationOperators(new ReportPreset { Name = MxOnly, MxFilter = MxFilter.MxOnly });
            if (Is(name, EveryCodedDelay))
                return new ReportPreset { Name = EveryCodedDelay, MinimumDelayMinutes = 0 };
            if (Is(name, OverAnHour))
                return new ReportPreset
                {
                    Name = OverAnHour,
                    MinimumDelayMinutes = 60,
                    ThresholdBasis = DelayThresholdBasis.ActualDelay,
                };
            return null;
        }

        private static ReportPreset WithStationOperators(ReportPreset preset)
        {
            preset.Operators.AddRange(StationOperators);
            return preset;
        }

        private static bool Is(string name, string candidate) =>
            string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Presets the user saved, one plain text file each under
    /// %APPDATA%\Delay Reporter\presets. A file per preset keeps deleting one trivial and
    /// stops a bad edit taking the others with it.
    /// </summary>
    public static class PresetStore
    {
        private const string KeyStation = "station";
        private const string KeyMinimumDelay = "minimumDelayMinutes";
        private const string KeyBasis = "thresholdBasis";
        private const string KeyMx = "mxFilter";
        private const string KeyMovementTypes = "movementTypes";
        private const string KeyOperators = "operators";
        private const string KeyDelayCodes = "delayCodes";
        private const string KeyRegistrations = "registrations";

        public static string Folder => Path.Combine(SettingsStore.DefaultFolder, "presets");

        public static List<string> List()
        {
            var names = new List<string>();
            try
            {
                if (Directory.Exists(Folder))
                    names.AddRange(Directory.GetFiles(Folder, "*.txt").Select(Path.GetFileNameWithoutExtension));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // An unreadable preset folder should not stop the menu building.
            }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        public static ReportPreset? Load(string name)
        {
            string? path = PathFor(name);
            if (path == null || !File.Exists(path)) return null;

            var store = new SettingsStore(path);
            store.Load();

            var preset = new ReportPreset
            {
                Name = name,
                Station = store.Get(KeyStation, string.Empty),
                MinimumDelayMinutes = Math.Max(0, store.Get(KeyMinimumDelay, ReportOptions.DefaultMinimumDelayMinutes)),
                ThresholdBasis = store.Get(KeyBasis, "codes").Equals("actual", StringComparison.OrdinalIgnoreCase)
                    ? DelayThresholdBasis.ActualDelay
                    : DelayThresholdBasis.IncludedCodes,
                MxFilter = Enum.TryParse(store.Get(KeyMx, string.Empty), true, out MxFilter mx) && Enum.IsDefined(typeof(MxFilter), mx)
                    ? mx
                    : MxFilter.All,
            };
            preset.MovementTypes.AddRange(Split(store.Get(KeyMovementTypes, string.Empty)));
            preset.Operators.AddRange(Split(store.Get(KeyOperators, string.Empty)));
            preset.DelayCodes.AddRange(Split(store.Get(KeyDelayCodes, string.Empty)));
            preset.Registrations.AddRange(Split(store.Get(KeyRegistrations, string.Empty)));
            return preset;
        }

        public static void Save(string name, ReportPreset preset)
        {
            string? path = PathFor(name);
            if (path == null) return;

            var store = new SettingsStore(path) { Header = "Delay Reporter preset: " + name.Trim() };
            store.Set(KeyStation, preset.Station);
            store.Set(KeyMinimumDelay, preset.MinimumDelayMinutes);
            store.Set(KeyBasis, preset.ThresholdBasis == DelayThresholdBasis.ActualDelay ? "actual" : "codes");
            store.Set(KeyMx, preset.MxFilter.ToString());
            store.Set(KeyMovementTypes, string.Join(", ", preset.MovementTypes));
            store.Set(KeyOperators, string.Join(", ", preset.Operators));
            store.Set(KeyDelayCodes, string.Join(", ", preset.DelayCodes));
            store.Set(KeyRegistrations, string.Join(", ", preset.Registrations));
            store.Save();
        }

        public static void Delete(string name)
        {
            try
            {
                string? path = PathFor(name);
                if (path != null && File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Nothing useful to say if the file is locked; the menu just keeps showing it.
            }
        }

        public static void Rename(string oldName, string newName)
        {
            ReportPreset? preset = Load(oldName);
            if (preset == null) return;
            Save(newName, preset);
            if (!string.Equals(PathFor(oldName), PathFor(newName), StringComparison.OrdinalIgnoreCase))
                Delete(oldName);
        }

        /// <summary>Null when the name could not be made into a safe file name.</summary>
        public static string? PathFor(string name)
        {
            string safe = Sanitise(name);
            return safe.Length == 0 ? null : Path.Combine(Folder, safe + ".txt");
        }

        public static string Sanitise(string? name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string((name ?? string.Empty).Trim().Where(c => Array.IndexOf(invalid, c) < 0).ToArray()).Trim();
        }

        private static IEnumerable<string> Split(string text) =>
            text.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0);
    }
}
