using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace DelayReporter.Core
{
    /// <summary>
    /// Preferences in a plain text file under the per-user application data folder, so
    /// they need no administrator rights and can be read or deleted by hand. Deleting the
    /// file restores the defaults.
    /// </summary>
    public sealed class SettingsStore
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public SettingsStore(string? path = null)
        {
            _path = path ?? DefaultPath;
        }

        public static string DefaultFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Delay Reporter");

        public static string DefaultPath => Path.Combine(DefaultFolder, "settings.txt");

        public string Path_ => _path;

        /// <summary>The comment written at the top of the file.</summary>
        public string Header { get; set; } = "Delay Reporter preferences. Delete this file to restore defaults.";

        public void Load()
        {
            _values.Clear();
            try
            {
                if (!File.Exists(_path)) return;
                foreach (string line in File.ReadAllLines(_path, Encoding.UTF8))
                {
                    string text = line.Trim();
                    if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal)) continue;

                    int equals = text.IndexOf('=');
                    if (equals <= 0) continue;

                    _values[text.Substring(0, equals).Trim()] = text.Substring(equals + 1).Trim();
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Unreadable preferences are not worth failing over; defaults apply.
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
                var text = new StringBuilder();
                text.Append("# ").AppendLine(Header);
                foreach (KeyValuePair<string, string> pair in _values)
                    text.Append(pair.Key).Append('=').AppendLine(pair.Value);
                File.WriteAllText(_path, text.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Losing preferences must never lose a report.
            }
        }

        public string Get(string key, string fallback) =>
            _values.TryGetValue(key, out string? value) && value.Length > 0 ? value : fallback;

        public int Get(string key, int fallback) =>
            _values.TryGetValue(key, out string? value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed : fallback;

        public bool Get(string key, bool fallback) =>
            _values.TryGetValue(key, out string? value)
                ? value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1"
                : fallback;

        public void Set(string key, string value) => _values[key] = value ?? string.Empty;

        public void Set(string key, int value) => _values[key] = value.ToString(CultureInfo.InvariantCulture);

        public void Set(string key, bool value) => _values[key] = value ? "true" : "false";
    }
}
