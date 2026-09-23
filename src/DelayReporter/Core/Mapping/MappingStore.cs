using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace DelayReporter.Core.Mapping
{
    /// <summary>
    /// The mapping CSVs, kept beside the settings in the per-user application data folder
    /// so they can be edited without administrator rights and survive replacing the
    /// executable.
    ///
    /// Each file is seeded from a copy embedded in the executable the first time it is
    /// needed, and never overwritten afterwards: the edited file always wins.
    /// </summary>
    public sealed class MappingStore
    {
        public const string DelayCodesFile = "delay-codes.csv";
        public const string AircraftTypesFile = "aircraft-types.csv";
        public const string OperatorsFile = "operators.csv";

        private readonly string _folder;

        public MappingStore(string? folder = null)
        {
            _folder = folder ?? DefaultFolder;
        }

        public static string DefaultFolder =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Delay Reporter", "mappings");

        public string Folder => _folder;

        public string PathOf(string fileName) => Path.Combine(_folder, fileName);

        public MappingTable DelayCodes { get; private set; } = new MappingTable(DelayCodesFile);

        public MappingTable AircraftTypes { get; private set; } = new MappingTable(AircraftTypesFile);

        public MappingTable Operators { get; private set; } = new MappingTable(OperatorsFile);

        public List<string> Warnings { get; } = new List<string>();

        public void Load()
        {
            Warnings.Clear();
            DelayCodes = LoadOne(DelayCodesFile);
            AircraftTypes = LoadOne(AircraftTypesFile);
            Operators = LoadOne(OperatorsFile);
        }

        private MappingTable LoadOne(string fileName)
        {
            string path = PathOf(fileName);
            try
            {
                if (!File.Exists(path)) SeedFile(fileName, path);
                string text = File.ReadAllText(path, Encoding.UTF8);
                MappingTable table = MappingTable.FromCsv(fileName, text);
                Warnings.AddRange(table.LoadWarnings);
                return table;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // A locked or unreadable mapping file must not stop a report being produced;
                // fall back to the embedded copy and say so.
                Warnings.Add($"{fileName} could not be read ({ex.Message}); the built-in copy was used.");
                MappingTable table = MappingTable.FromCsv(fileName, ReadEmbedded(fileName));
                Warnings.AddRange(table.LoadWarnings);
                return table;
            }
        }

        private void SeedFile(string fileName, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ReadEmbedded(fileName), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        /// <summary>Writes the built-in copy over the file on disk, discarding local edits.</summary>
        public void RestoreDefaults(string fileName)
        {
            string path = PathOf(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, ReadEmbedded(fileName), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public static string ReadEmbedded(string fileName)
        {
            Assembly assembly = typeof(MappingStore).Assembly;
            string resource = "DelayReporter.Resources." + fileName;
            using (Stream? stream = assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                    throw new InvalidOperationException($"The built-in copy of {fileName} is missing from the executable.");
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }
    }
}
