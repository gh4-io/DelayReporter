using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DelayReporter.Core;
using DelayReporter.Core.Mapping;
using DelayReporter.Core.Movement;
using DelayReporter.Core.Report;
using DelayReporter.Core.Spreadsheet;
using DelayReporter.Views;

namespace DelayReporter
{
    public partial class MainWindow : Window
    {
        private const string KeyStation = "station";
        private const string KeyMinimumDelay = "minimumDelayMinutes";
        private const string KeyBasis = "thresholdBasis";
        private const string KeyLastFolder = "lastFolder";

        private readonly SettingsStore _settings = new SettingsStore();
        private readonly MappingStore _mappings = new MappingStore();

        private MovementSheet? _sheet;
        private ReportModel? _model;
        private readonly List<CheckBox> _movementTypeBoxes = new List<CheckBox>();
        private readonly List<CheckBox> _operatorBoxes = new List<CheckBox>();

        /// <summary>Set while the window is populating controls, to avoid recomputing per change.</summary>
        private bool _loading;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _loading = true;
            _settings.Load();
            StationBox.Text = _settings.Get(KeyStation, ReportOptions.DefaultStation);
            MinimumDelayBox.Text = _settings
                .Get(KeyMinimumDelay, ReportOptions.DefaultMinimumDelayMinutes)
                .ToString(CultureInfo.InvariantCulture);

            bool actualBasis = _settings.Get(KeyBasis, "codes")
                .Equals("actual", StringComparison.OrdinalIgnoreCase);
            BasisActualRadio.IsChecked = actualBasis;
            BasisCodesRadio.IsChecked = !actualBasis;
            _loading = false;

            try
            {
                _mappings.Load();
                Status($"Loaded {_mappings.DelayCodes.Count} delay codes and " +
                       $"{_mappings.AircraftTypes.Count} aircraft types.");
            }
            catch (Exception ex)
            {
                Status("Mappings could not be loaded: " + ex.Message);
            }

            string? startup = (Application.Current as App)?.StartupFile;
            if (!string.IsNullOrEmpty(startup)) Load(startup!);
        }

        // ---- opening -------------------------------------------------------

        private void OnOpen(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open movement sheet",
                Filter = "Movement sheets (*.xlsx;*.csv)|*.xlsx;*.csv|Excel workbooks (*.xlsx)|*.xlsx|" +
                         "Comma separated (*.csv)|*.csv|All files (*.*)|*.*",
                InitialDirectory = _settings.Get(KeyLastFolder, string.Empty),
            };
            if (dialog.ShowDialog(this) == true) Load(dialog.FileName);
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
                Load(paths[0]);
        }

        private void Load(string path)
        {
            try
            {
                _sheet = MovementReader.ReadFile(path);
            }
            // A file that is not a readable zip surfaces as InvalidDataException, which is
            // not an IOException; without it a damaged workbook reaches the crash handler.
            catch (Exception ex) when (ex is SpreadsheetFormatException || ex is IOException ||
                                       ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                _sheet = null;
                _model = null;
                GenerateButton.IsEnabled = false;
                FlightList.ItemsSource = null;
                SummaryText.Text = "The file could not be read.";
                ShowWarnings(new[] { ex.Message });
                Status("Could not open " + Path.GetFileName(path));
                return;
            }

            FileText.Text = path;
            _settings.Set(KeyLastFolder, Path.GetDirectoryName(path) ?? string.Empty);

            // Adopt the file's own station and period as the starting point.
            _loading = true;
            if (_sheet.DetectedStation.Length > 0 && StationBox.Text.Trim().Length == 0)
                StationBox.Text = _sheet.DetectedStation;

            DateFromPicker.SelectedDate = _sheet.PeriodStart?.Date;
            DateToPicker.SelectedDate = _sheet.PeriodEnd?.Date;

            BuildMovementTypes(_sheet);
            BuildOperators(_sheet);
            _loading = false;

            Recompute();
        }

        private void BuildMovementTypes(MovementSheet sheet)
        {
            _movementTypeBoxes.Clear();
            var defaults = new HashSet<string>(ReportOptions.DefaultMovementTypes(sheet),
                                               StringComparer.OrdinalIgnoreCase);

            foreach (string type in sheet.MovementTypes)
            {
                var box = new CheckBox
                {
                    Content = type,
                    Tag = type,
                    IsChecked = defaults.Contains(type),
                    ToolTip = ReportOptions.IsFlightType(type)
                        ? "Flight movements"
                        : "Ground runs and tows carry no departure or delay codes",
                };
                box.Checked += OnOptionChanged;
                box.Unchecked += OnOptionChanged;
                _movementTypeBoxes.Add(box);
            }
            MovementTypeList.ItemsSource = _movementTypeBoxes;
        }

        private void BuildOperators(MovementSheet sheet)
        {
            _operatorBoxes.Clear();
            foreach (string op in sheet.Operators)
            {
                var box = new CheckBox { Content = op, Tag = op, IsChecked = true };
                box.Checked += OnOptionChanged;
                box.Unchecked += OnOptionChanged;
                _operatorBoxes.Add(box);
            }
            OperatorList.ItemsSource = _operatorBoxes;
        }

        // ---- options and preview -------------------------------------------

        private void OnOptionChanged(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Recompute();
        }

        private ReportOptions CurrentOptions()
        {
            var options = new ReportOptions
            {
                Station = StationBox.Text.Trim().ToUpperInvariant(),
                ThresholdBasis = BasisActualRadio.IsChecked == true
                    ? DelayThresholdBasis.ActualDelay
                    : DelayThresholdBasis.IncludedCodes,
            };

            options.MinimumDelayMinutes =
                int.TryParse(MinimumDelayBox.Text.Trim(), NumberStyles.Integer,
                             CultureInfo.InvariantCulture, out int minutes) && minutes >= 0
                    ? minutes
                    : ReportOptions.DefaultMinimumDelayMinutes;

            options.DateFrom = DateFromPicker.SelectedDate;
            options.DateTo = DateToPicker.SelectedDate;

            // An all-checked list means no restriction, which keeps the report stable if a
            // later file contains a type or operator this one did not.
            AddChecked(_movementTypeBoxes, options.MovementTypes);
            AddChecked(_operatorBoxes, options.Operators);

            foreach (string code in Split(CodesBox.Text))
                options.DelayCodes.Add(MappingTable.Normalize(code));
            foreach (string registration in Split(RegistrationsBox.Text))
                options.Registrations.Add(registration);

            return options;
        }

        private static void AddChecked(List<CheckBox> boxes, HashSet<string> target)
        {
            if (boxes.Count == 0) return;
            if (boxes.All(b => b.IsChecked == true)) return;      // everything selected: no filter
            foreach (CheckBox box in boxes.Where(b => b.IsChecked == true))
                target.Add((string)box.Tag);
        }

        private static IEnumerable<string> Split(string? text) =>
            (text ?? string.Empty)
                .Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0);

        private void Recompute()
        {
            if (_sheet == null) return;

            _model = ReportBuilder.Build(_sheet, _mappings, CurrentOptions());

            FlightList.ItemsSource = _model.Flights;
            GenerateButton.IsEnabled = _model.ReportedFlights > 0;

            StationHint.Text = _sheet.DetectedStation.Length > 0
                ? $"Most rows in this file depart from {_sheet.DetectedStation} " +
                  $"({_sheet.DetectedStationRowCount} of {_sheet.Rows.Count})."
                : "Departures from this station are reported.";

            SummaryText.Text = BuildSummary(_model);
            ShowWarnings(_model.Warnings);

            Status(_model.ReportedFlights > 0
                ? $"{_model.ReportedFlights} flight(s) ready to report."
                : "No flights match the current filters.");
        }

        private static string BuildSummary(ReportModel model)
        {
            var text = new StringBuilder();
            text.Append(model.Station).Append(" departures ").Append(model.StationDepartures);
            text.Append("   ·   with coded delay ").Append(model.FlightsWithCodedDelay);
            text.Append("   ·   reported ").Append(model.ReportedFlights);
            text.Append("   ·   delay events ").Append(model.ReportedEvents);
            text.Append("   ·   total coded ").Append(model.TotalCodedDelayText);
            text.AppendLine();
            text.Append("below threshold ").Append(model.ExcludedByThreshold);
            text.Append("   ·   excluded by mapper ").Append(model.ExcludedEvents).Append(" event(s)");
            text.Append("   ·   needing SI ").Append(model.FlightsRequiringSupplementary);
            text.Append("   ·   coded/actual mismatches ").Append(model.ReconciliationMismatches);

            int unmapped = model.UnmappedCodes.Count();
            if (unmapped > 0)
                text.Append("   ·   unmapped codes ")
                    .Append(string.Join(", ", model.UnmappedCodes.Select(c => c.Code)));

            var unmappedAircraft = model.UnmappedAircraft.ToList();
            if (unmappedAircraft.Count > 0)
                text.Append("   ·   unmapped aircraft ").Append(string.Join(", ", unmappedAircraft));

            return text.ToString();
        }

        private void ShowWarnings(IEnumerable<string> warnings)
        {
            List<string> list = warnings.ToList();
            if (list.Count == 0)
            {
                WarningPanel.Visibility = Visibility.Collapsed;
                WarningText.Text = string.Empty;
                return;
            }
            WarningPanel.Visibility = Visibility.Visible;
            WarningText.Text = string.Join(Environment.NewLine, list);
        }

        // ---- generating ----------------------------------------------------

        private void OnGenerate(object sender, RoutedEventArgs e)
        {
            if (_model == null || _sheet == null) return;

            var dialog = new SaveFileDialog
            {
                Title = "Save delay report",
                Filter = "Excel workbook (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = SuggestedName(_model),
                InitialDirectory = _settings.Get(KeyLastFolder, string.Empty),
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                ReportWriter.Write(dialog.FileName, _model);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this,
                    "The report could not be saved. " + ex.Message +
                    "\n\nIf the file is open in Excel, close it and try again.",
                    "Delay Reporter", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveSettings();
            Status("Saved " + dialog.FileName);

            if (MessageBox.Show(this, "Report saved. Open it now?", "Delay Reporter",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true }); }
                catch (Exception ex) { Status("Saved, but could not open it: " + ex.Message); }
            }
        }

        private static string SuggestedName(ReportModel model)
        {
            string date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return $"{model.Station}-departure-delays-{date}.xlsx";
        }

        private void SaveSettings()
        {
            _settings.Set(KeyStation, StationBox.Text.Trim().ToUpperInvariant());
            _settings.Set(KeyMinimumDelay, CurrentOptions().MinimumDelayMinutes);
            _settings.Set(KeyBasis, BasisActualRadio.IsChecked == true ? "actual" : "codes");
            _settings.Save();
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveSettings();
            base.OnClosed(e);
        }

        // ---- helpers -------------------------------------------------------

        private void OnOpenMappings(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_mappings.Folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_mappings.Folder) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Status("Could not open the mappings folder: " + ex.Message);
            }
        }

        private void OnAbout(object sender, RoutedEventArgs e) =>
            new AboutDialog { Owner = this }.ShowDialog();

        private void Status(string message) => StatusText.Text = message;
    }
}
