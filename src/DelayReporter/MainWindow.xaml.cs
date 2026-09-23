using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using DelayReporter.Controls;
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
        private const string KeyUnselectedCodes = "unselectedCodes";
        private const string KeyOperatorLabels = "operatorLabels";
        private const string KeyAircraftFormat = "aircraftFormat";
        private const string KeyDebugSummary = "debugSummary";

        private const double OperatorColumnCodeWidth = 46;
        private const double OperatorColumnLabelWidth = 120;

        /// <summary>
        /// How the Codes column shows codes the Delay codes filter did not choose. A dependency
        /// property so the column's cells follow a change without the report being rebuilt.
        /// </summary>
        public static readonly DependencyProperty UnselectedCodesProperty =
            DependencyProperty.Register(
                nameof(UnselectedCodes), typeof(UnselectedCodeDisplay), typeof(MainWindow),
                new PropertyMetadata(UnselectedCodeDisplay.Visible));

        private readonly SettingsStore _settings = new SettingsStore();
        private readonly MappingStore _mappings = new MappingStore();

        private MovementSheet? _sheet;
        private ReportModel? _model;

        /// <summary>
        /// The user's per-flight MX corrections, keyed by source row number. The flights are
        /// rebuilt on every change, so the corrections live here and are copied into each set
        /// of options. They belong to the open file and are cleared when another is loaded.
        /// </summary>
        private readonly Dictionary<int, bool> _mxOverrides = new Dictionary<int, bool>();

        private bool _useOperatorLabels = true;
        private AircraftLabelFormat _aircraftFormat = AircraftLabelFormat.Full;
        private bool _showDebugSummary;

        /// <summary>Set while the window is populating controls, to avoid recomputing per change.</summary>
        private bool _loading;

        public MainWindow()
        {
            InitializeComponent();

            // An editable ComboBox raises no text event of its own. Its inner text box raises
            // TextChanged both when the user types and when an item is picked from the list,
            // and by the time it bubbles here the ComboBox's Text already holds the new value.
            StationBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnOptionChanged));
            Loaded += OnLoaded;
        }

        public UnselectedCodeDisplay UnselectedCodes
        {
            get => (UnselectedCodeDisplay)GetValue(UnselectedCodesProperty);
            set => SetValue(UnselectedCodesProperty, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // ComboBox has no MaxLength; the limit the old text box carried lives on its editor.
            StationBox.ApplyTemplate();
            if (StationBox.Template.FindName("PART_EditableTextBox", StationBox) is TextBox stationEditor)
                stationEditor.MaxLength = 8;

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

            UnselectedCodes = ParseEnum(_settings.Get(KeyUnselectedCodes, string.Empty), UnselectedCodeDisplay.Visible);
            _useOperatorLabels = _settings.Get(KeyOperatorLabels, true);
            _aircraftFormat = ParseEnum(_settings.Get(KeyAircraftFormat, string.Empty), AircraftLabelFormat.Full);
            _showDebugSummary = _settings.Get(KeyDebugSummary, false);
            SizeOperatorColumn();
            _loading = false;

            try
            {
                _mappings.Load();
                Status($"Loaded {_mappings.DelayCodes.Count} delay codes, " +
                       $"{_mappings.AircraftTypes.Count} aircraft types and " +
                       $"{_mappings.Operators.Count} operators.");
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
                PreviewButton.IsEnabled = false;
                FlightList.ItemsSource = null;
                SummaryText.Text = "The file could not be read.";
                ShowWarnings(new[] { ex.Message });
                Status("Could not open " + Path.GetFileName(path));
                return;
            }

            FileText.Text = path;
            _settings.Set(KeyLastFolder, Path.GetDirectoryName(path) ?? string.Empty);

            // Row numbers mean nothing in another file, so corrections do not carry over.
            _mxOverrides.Clear();

            // Adopt the file's own station and period as the starting point.
            _loading = true;
            BuildStations(_sheet);
            if (_sheet.DetectedStation.Length > 0 && StationBox.Text.Trim().Length == 0)
                StationBox.Text = _sheet.DetectedStation;

            DateFromPicker.SelectedDate = _sheet.PeriodStart?.Date;
            DateToPicker.SelectedDate = _sheet.PeriodEnd?.Date;

            BuildMovementTypes(_sheet);
            BuildOperators(_sheet);
            BuildDelayCodes(_sheet);
            BuildRegistrations(_sheet);
            _loading = false;

            Recompute();
        }

        private void BuildStations(MovementSheet sheet)
        {
            // Replacing the items clears any selected item, and an editable ComboBox clears its
            // text along with it. The typed station is the setting, so it is carried across.
            string station = StationBox.Text;
            StationBox.ItemsSource = sheet.Stations.ToList();
            StationBox.Text = station;
        }

        private void BuildMovementTypes(MovementSheet sheet)
        {
            var defaults = new HashSet<string>(ReportOptions.DefaultMovementTypes(sheet),
                                               StringComparer.OrdinalIgnoreCase);

            MovementTypeFilter.ItemsSource = sheet.MovementTypes
                .Select(type => new MultiSelectItem(type, type, defaults.Contains(type))
                {
                    ToolTip = ReportOptions.IsFlightType(type)
                        ? "Flight movements"
                        : "Ground runs and tows carry no departure or delay codes",
                })
                .ToList();
        }

        private void BuildOperators(MovementSheet sheet) =>
            OperatorFilter.ItemsSource = sheet.Operators
                .Select(op => new MultiSelectItem(op, WithLabel(op, _mappings.Operators), isSelected: true))
                .ToList();

        // Delay codes and tail numbers start with nothing ticked, which like the empty text
        // boxes they replace means no filter; ticking one then narrows the report to it.
        private void BuildDelayCodes(MovementSheet sheet) =>
            DelayCodeFilter.ItemsSource = sheet.DelayCodes
                .Select(code => new MultiSelectItem(code, WithLabel(code, _mappings.DelayCodes)))
                .ToList();

        private void BuildRegistrations(MovementSheet sheet) =>
            RegistrationFilter.ItemsSource = sheet.Registrations
                .Select(registration => new MultiSelectItem(registration))
                .ToList();

        /// <summary>"CKS — Kalitta Air" when mapped, the bare code otherwise.</summary>
        private static string WithLabel(string code, MappingTable table)
        {
            string label = table.Label(code);
            return label == code ? code : $"{code} — {label}";
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
                UseOperatorLabels = _useOperatorLabels,
                AircraftFormat = _aircraftFormat,
                ShowDebugSummary = _showDebugSummary,
            };

            foreach (KeyValuePair<int, bool> pair in _mxOverrides)
                options.MxOverrides[pair.Key] = pair.Value;

            options.MinimumDelayMinutes =
                int.TryParse(MinimumDelayBox.Text.Trim(), NumberStyles.Integer,
                             CultureInfo.InvariantCulture, out int minutes) && minutes >= 0
                    ? minutes
                    : ReportOptions.DefaultMinimumDelayMinutes;

            options.DateFrom = DateFromPicker.SelectedDate;
            options.DateTo = DateToPicker.SelectedDate;

            // An all-ticked or none-ticked list yields no values, meaning no restriction, which
            // keeps the report stable if a later file contains a value this one did not.
            foreach (string type in MovementTypeFilter.FilterValues)
                options.MovementTypes.Add(type);
            foreach (string op in OperatorFilter.FilterValues)
                options.Operators.Add(op);
            foreach (string code in DelayCodeFilter.FilterValues)
                options.DelayCodes.Add(MappingTable.Normalize(code));
            foreach (string registration in RegistrationFilter.FilterValues)
                options.Registrations.Add(registration);

            return options;
        }

        private void Recompute()
        {
            if (_sheet == null) return;

            _model = ReportBuilder.Build(_sheet, _mappings, CurrentOptions());

            FlightList.ItemsSource = _model.Flights;
            GenerateButton.IsEnabled = _model.ReportedFlights > 0;
            PreviewButton.IsEnabled = GenerateButton.IsEnabled;

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

        /// <summary>The workbook's summary, worded the same, as one line plus the optional detail.</summary>
        private static string BuildSummary(ReportModel model)
        {
            string text = ReportSummary.Line(ReportSummary.Metrics(model));
            if (model.Options.ShowDebugSummary)
                text += Environment.NewLine + "Detail — " + ReportSummary.DetailLine(model);
            return text;
        }

        // ---- MX corrections ------------------------------------------------

        /// <summary>
        /// Moves a flight's MX classification on one step: automatic, forced, excluded, then
        /// automatic again. The box's own toggle is ignored; the rebuilt row shows the result.
        /// </summary>
        private void OnMxClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is ReportFlight flight)) return;

            int row = flight.SourceRowNumber;
            switch (flight.MxOverride)
            {
                case null: _mxOverrides[row] = true; break;
                case true: _mxOverrides[row] = false; break;
                default: _mxOverrides.Remove(row); break;
            }

            // Rebuilding replaces every row, which would otherwise throw the list back to the
            // top and lose the selection mid-way through ticking a long report.
            ScrollViewer? scroll = FindDescendant<ScrollViewer>(FlightList);
            double vertical = scroll?.VerticalOffset ?? 0;
            double horizontal = scroll?.HorizontalOffset ?? 0;

            Recompute();

            FlightList.SelectedItem = _model?.Flights.FirstOrDefault(f => f.SourceRowNumber == row);
            if (scroll != null)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    scroll.ScrollToVerticalOffset(vertical);
                    scroll.ScrollToHorizontalOffset(horizontal);
                }));
            }
        }

        private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                T? found = FindDescendant<T>(child);
                if (found != null) return found;
            }
            return null;
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

        /// <summary>
        /// Writes the current report to a temporary file and opens it straight away: no save
        /// dialog, no prompt and no settings saved, so it is a quick look rather than a save.
        /// </summary>
        private void OnPreview(object sender, RoutedEventArgs e)
        {
            if (_model == null) return;

            string folder = Path.Combine(Path.GetTempPath(), "Delay Reporter");
            string path = Path.Combine(folder, "preview.xlsx");
            try
            {
                Directory.CreateDirectory(folder);
                try
                {
                    ReportWriter.Write(path, _model);
                }
                catch (IOException)
                {
                    // Excel still has the previous preview open and locked; use a fresh name
                    // instead of blocking the user.
                    path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".xlsx");
                    ReportWriter.Write(path, _model);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Status("Could not write the preview: " + ex.Message);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                Status("Opened a preview of " + _model.ReportedFlights + " flight(s) in Excel.");
            }
            catch (Exception ex)
            {
                Status("Could not open the preview: " + ex.Message);
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
            _settings.Set(KeyUnselectedCodes, UnselectedCodes.ToString());
            _settings.Set(KeyOperatorLabels, _useOperatorLabels);
            _settings.Set(KeyAircraftFormat, _aircraftFormat.ToString());
            _settings.Set(KeyDebugSummary, _showDebugSummary);
            _settings.Save();
        }

        private static T ParseEnum<T>(string text, T fallback) where T : struct =>
            Enum.TryParse(text, ignoreCase: true, out T value) && Enum.IsDefined(typeof(T), value)
                ? value
                : fallback;

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

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog
            {
                Owner = this,
                UnselectedCodes = UnselectedCodes,
                UseOperatorLabels = _useOperatorLabels,
                AircraftFormat = _aircraftFormat,
                ShowDebugSummary = _showDebugSummary,
            };
            if (dialog.ShowDialog() != true) return;

            bool changed = dialog.UnselectedCodes != UnselectedCodes ||
                           dialog.UseOperatorLabels != _useOperatorLabels ||
                           dialog.AircraftFormat != _aircraftFormat ||
                           dialog.ShowDebugSummary != _showDebugSummary;
            if (!changed) return;

            UnselectedCodes = dialog.UnselectedCodes;
            if (dialog.UseOperatorLabels != _useOperatorLabels)
            {
                _useOperatorLabels = dialog.UseOperatorLabels;
                SizeOperatorColumn();
            }
            _aircraftFormat = dialog.AircraftFormat;
            _showDebugSummary = dialog.ShowDebugSummary;

            SaveSettings();
            Recompute();
        }

        /// <summary>
        /// A carrier name needs a wider column than a code. Set only when the choice changes,
        /// so a width the user dragged is otherwise left alone.
        /// </summary>
        private void SizeOperatorColumn() =>
            OperatorColumn.Width = _useOperatorLabels ? OperatorColumnLabelWidth : OperatorColumnCodeWidth;

        private void OnAbout(object sender, RoutedEventArgs e) =>
            new AboutDialog { Owner = this }.ShowDialog();

        private void Status(string message) => StatusText.Text = message;
    }
}
