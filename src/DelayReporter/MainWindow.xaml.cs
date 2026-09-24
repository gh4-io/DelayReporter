using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using DelayReporter.Controls;
using DelayReporter.Core;
using DelayReporter.Core.Email;
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
        private const string KeyLayout = "reportLayout";
        private const string KeyDebugSummary = "debugSummary";
        private const string KeyEmailTo = "emailTo";
        private const string KeyEmailCc = "emailCc";
        private const string KeyEmailAttach = "emailAttachWorkbook";
        private const string KeyOptionsPane = "optionsPane";
        private const string KeyOptionsWidth = "optionsPaneWidth";
        private const string KeyDateFormat = "dateFormat";

        private const double OperatorColumnCodeWidth = 92;
        private const double OperatorColumnLabelWidth = 160;
        private const double DefaultOptionsWidth = 280;
        private const double MinimumOptionsWidth = 220;
        private const double RailWidth = 40;

        private const string WorkbookMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>
        /// How the Codes column shows codes the Delay codes filter did not choose. A dependency
        /// property so the column's cells follow a change without the report being rebuilt.
        /// </summary>
        public static readonly DependencyProperty UnselectedCodesProperty =
            DependencyProperty.Register(
                nameof(UnselectedCodes), typeof(UnselectedCodeDisplay), typeof(MainWindow),
                new PropertyMetadata(UnselectedCodeDisplay.Visible));

        private static readonly Dictionary<string, string> PresetGlyphs = new Dictionary<string, string>
        {
            [BuiltInPresets.Standard] = "",
            [BuiltInPresets.EveryCodedDelay] = "",
            [BuiltInPresets.OverAnHour] = "",
        };

        private readonly SettingsStore _settings = new SettingsStore();
        private readonly MappingStore _mappings = new MappingStore();
        private readonly DispatcherTimer _dragLeaveTimer;
        private readonly DispatcherTimer _searchTimer;
        private readonly DispatcherTimer _statusTimer;
        private readonly List<ColumnHeader> _headers = new List<ColumnHeader>();

        /// <summary>The select-all box in the grid header, which lives in a template.</summary>
        private CheckBox? _selectAllCheck;

        private MovementSheet? _sheet;
        private ReportModel? _model;
        private string _sourcePath = string.Empty;

        /// <summary>The Date column's pattern; empty prints dates as the file wrote them.</summary>
        private string _dateFormat = string.Empty;

        /// <summary>
        /// The user's per-flight MX corrections, keyed by source row number. The flights are
        /// rebuilt on every change, so the corrections live here and are copied into each set
        /// of options. They belong to the open file and are cleared when another is loaded.
        /// </summary>
        private readonly Dictionary<int, bool> _mxOverrides = new Dictionary<int, bool>();

        /// <summary>Flights hidden by hand, by source row number. Like the MX corrections, per file.</summary>
        private readonly HashSet<int> _hiddenRows = new HashSet<int>();

        private ReportSortColumn _sortColumn = ReportSortColumn.Date;
        private bool _sortDescending;
        private bool _showHidden;

        private bool _useOperatorLabels = true;
        private AircraftLabelFormat _aircraftFormat = AircraftLabelFormat.Full;
        private ReportLayout _layout = ReportLayout.Grouped;
        private bool _showDebugSummary;
        private string _emailTo = string.Empty;
        private string _emailCc = string.Empty;
        private bool _attachWorkbook = true;

        /// <summary>A preset applied before any file was open; its filters wait for one.</summary>
        private ReportPreset? _pendingPreset;

        private double _optionsWidth = DefaultOptionsWidth;

        /// <summary>Set while the window is populating controls, to avoid recomputing per change.</summary>
        private bool _loading;

        /// <summary>Set while the window itself moves toggle buttons, so their handlers do not re-enter.</summary>
        private bool _synchronizing;

        /// <summary>Set while the list is refilled and its selection put back.</summary>
        private bool _restoringSelection;

        /// <summary>Set when a new file opens, so the list starts at the top with nothing selected.</summary>
        private bool _resetView;

        public MainWindow()
        {
            InitializeComponent();

            // An editable ComboBox raises no text event of its own. Its inner text box raises
            // TextChanged both when the user types and when an item is picked from the list,
            // and by the time it bubbles here the ComboBox's Text already holds the new value.
            StationBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnOptionChanged));

            // Leave fires when the pointer crosses between elements too; only hide once it stays gone.
            _dragLeaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _dragLeaveTimer.Tick += (s, e) => ShowDropOverlay(false);

            // Rebuild once typing pauses rather than on every keystroke.
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                ApplySearch();
            };

            _statusTimer = new DispatcherTimer();
            _statusTimer.Tick += (s, e) => ShowSummary();

            SetHeader(DateColumn, "Date", ReportSortColumn.Date, HeaderFilter.Date);
            SetHeader(FlightColumn, "MVT Nr", ReportSortColumn.Flight);
            SetHeader(RegColumn, "Reg", ReportSortColumn.Registration, HeaderFilter.Registration);
            SetHeader(ToColumn, "To", ReportSortColumn.Destination);
            SetHeader(StdColumn, "STD", ReportSortColumn.Scheduled);
            SetHeader(DelayColumn, "Delay", ReportSortColumn.ActualDelay, HeaderFilter.Delay);
            SetHeader(MxColumn, "MX", ReportSortColumn.Mx, HeaderFilter.Mx);
            SetHeader(OperatorColumn, "OPR", ReportSortColumn.Operator, HeaderFilter.Operator);
            SetHeader(CodesColumn, "Codes", ReportSortColumn.Codes, HeaderFilter.DelayCodes);
            SetHeader(OutstandingColumn, "Outstanding", ReportSortColumn.Outstanding);
            UpdateHeaders();

            _synchronizing = true;
            TabHome.IsChecked = true;
            _synchronizing = false;
            ApplyTab();

            Loaded += OnLoaded;
        }

        public UnselectedCodeDisplay UnselectedCodes
        {
            get => (UnselectedCodeDisplay)GetValue(UnselectedCodesProperty);
            set => SetValue(UnselectedCodesProperty, value);
        }

        private void SetHeader(GridViewColumn column, string title, ReportSortColumn sort, HeaderFilter filter = HeaderFilter.None)
        {
            var header = new ColumnHeader(title, sort, filter);
            column.Header = header;
            _headers.Add(header);
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
            _layout = ParseEnum(_settings.Get(KeyLayout, string.Empty), ReportLayout.Grouped);
            _showDebugSummary = _settings.Get(KeyDebugSummary, false);
            _emailTo = _settings.Get(KeyEmailTo, string.Empty);
            _emailCc = _settings.Get(KeyEmailCc, string.Empty);
            _attachWorkbook = _settings.Get(KeyEmailAttach, true);

            _optionsWidth = Math.Max(MinimumOptionsWidth, _settings.Get(KeyOptionsWidth, (int)DefaultOptionsWidth));
            SetOptionsPane(_settings.Get(KeyOptionsPane, true));
            _dateFormat = _settings.Get(KeyDateFormat, "dd MMM yyyy");
            ApplyDatePickerFormat();
            SizeOperatorColumn();
            _loading = false;

            BuildPresetButtons();
            RefreshCustomPresets();
            UpdateEmptyState();
            UpdateCommands();

            try
            {
                _mappings.Load();
                Status($"Loaded {_mappings.DelayCodes.Count} delay codes, " +
                       $"{_mappings.AircraftTypes.Count} aircraft types and " +
                       $"{_mappings.Operators.Count} operators. Drop a movement sheet anywhere in this window to begin.");
            }
            catch (Exception ex)
            {
                Status("Mappings could not be loaded: " + ex.Message);
            }

            string? startup = (Application.Current as App)?.StartupFile;
            if (!string.IsNullOrEmpty(startup)) Load(startup!);
        }

        // ---- ribbon tabs and panes -----------------------------------------

        // Ribbon tabs behave like radio buttons that cannot all be off.
        private void OnTabChanged(object sender, RoutedEventArgs e)
        {
            if (_synchronizing) return;
            _synchronizing = true;
            var source = (ToggleButton)sender;
            if (source.IsChecked == true)
            {
                foreach (ToggleButton other in new[] { TabHome, TabPresets, TabView, TabHelp }.Where(b => b != source))
                    other.IsChecked = false;
            }
            else
            {
                source.IsChecked = true;
            }
            _synchronizing = false;
            ApplyTab();
        }

        private void ApplyTab()
        {
            PanelHome.Visibility = TabHome.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelPresets.Visibility = TabPresets.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelView.Visibility = TabView.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelHelp.Visibility = TabHelp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool IsOptionsPaneOpen => OptionsPane.Visibility == Visibility.Visible;

        private void SetOptionsPane(bool open)
        {
            // Remember a width the user dragged, so collapsing and expanding keeps it.
            if (!open && IsOptionsPaneOpen && OptionsColumn.ActualWidth >= MinimumOptionsWidth)
                _optionsWidth = OptionsColumn.ActualWidth;

            OptionsPane.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            OptionsRail.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            OptionsSplitter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            OptionsColumn.Width = new GridLength(open ? _optionsWidth : RailWidth);
            OptionsSplitColumn.Width = new GridLength(open ? 5 : 0);

            _synchronizing = true;
            ToggleOptionsPane.IsChecked = open;
            _synchronizing = false;
        }

        private void OnToggleOptionsPane(object sender, RoutedEventArgs e)
        {
            if (!_synchronizing) SetOptionsPane(ToggleOptionsPane.IsChecked == true);
        }

        private void OnCollapseOptions(object sender, RoutedEventArgs e) => SetOptionsPane(false);

        private void OnExpandOptions(object sender, RoutedEventArgs e) => SetOptionsPane(true);

        // ---- opening and closing -------------------------------------------

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

        private static bool IsMovementFile(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            bool accepted = e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Any(IsMovementFile);
            e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;

            _dragLeaveTimer.Stop();
            if (accepted) ShowDropOverlay(true);
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            _dragLeaveTimer.Stop();
            _dragLeaveTimer.Start();
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            _dragLeaveTimer.Stop();
            ShowDropOverlay(false);

            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] files)) return;
            e.Handled = true;

            string? path = files.FirstOrDefault(IsMovementFile);
            if (path != null) Load(path);
        }

        private void ShowDropOverlay(bool show)
        {
            _dragLeaveTimer.Stop();
            DropOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
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
                ResetToEmpty();
                ShowWarnings(new[] { "Could not read " + path + ". " + ex.Message });
                Status("Could not open " + Path.GetFileName(path));
                return;
            }

            _sourcePath = path;
            Title = Path.GetFileName(path) + " - Delay Reporter";
            _settings.Set(KeyLastFolder, Path.GetDirectoryName(path) ?? string.Empty);

            // Row numbers mean nothing in another file, so corrections and hidden rows do not carry over.
            _mxOverrides.Clear();
            _hiddenRows.Clear();
            _resetView = true;

            // Adopt the file's own station and period as the starting point.
            List<string> presetNotes = new List<string>();
            _loading = true;
            try
            {
                // A search left over from the last file would thin this one's report unasked.
                _searchTimer.Stop();
                SearchBox.Text = string.Empty;

                BuildStations(_sheet);
                if (_sheet.DetectedStation.Length > 0 && StationBox.Text.Trim().Length == 0)
                    StationBox.Text = _sheet.DetectedStation;

                DateFromPicker.SelectedDate = _sheet.PeriodStart?.Date;
                DateToPicker.SelectedDate = _sheet.PeriodEnd?.Date;

                BuildMovementTypes(_sheet);
                BuildOperators(_sheet);
                BuildDelayCodes(_sheet);
                BuildRegistrations(_sheet);

                if (_pendingPreset != null)
                {
                    presetNotes = ApplyPresetLists(_pendingPreset);
                    presetNotes.Insert(0, $"Applied the filters of the preset {_pendingPreset.Name}.");
                    _pendingPreset = null;
                }
            }
            finally
            {
                _loading = false;
            }

            Recompute();
            if (presetNotes.Count > 0) Status(string.Join(" ", presetNotes));
        }

        private void OnCloseFile(object sender, RoutedEventArgs e)
        {
            if (_sheet == null) return;
            string name = Path.GetFileName(_sourcePath);
            ResetToEmpty();
            ShowWarnings(Array.Empty<string>());
            Status($"Closed {name}. Drop another movement sheet here, or press Ctrl+O.");
        }

        /// <summary>
        /// Back to how the window starts: no file, and nothing carried over from the last one,
        /// so no filter, search, hidden row, MX tick or sort order. The station and minimum
        /// delay stay, because they are saved preferences rather than part of a file.
        /// </summary>
        private void ResetToEmpty()
        {
            _sheet = null;
            _model = null;
            _sourcePath = string.Empty;
            _mxOverrides.Clear();
            _hiddenRows.Clear();
            _sortColumn = ReportSortColumn.Date;
            _sortDescending = false;

            _loading = true;
            try
            {
                _searchTimer.Stop();
                SearchBox.Text = string.Empty;
                MxAllRadio.IsChecked = true;

                string station = StationBox.Text;
                StationBox.ItemsSource = null;
                StationBox.Text = station;

                DateFromPicker.SelectedDate = null;
                DateToPicker.SelectedDate = null;
                MovementTypeFilter.ItemsSource = null;
                OperatorFilter.ItemsSource = null;
                DelayCodeFilter.ItemsSource = null;
                RegistrationFilter.ItemsSource = null;
            }
            finally
            {
                _loading = false;
            }

            FlightList.ItemsSource = null;
            SetShowHidden(false);
            Title = "Delay Reporter";
            UpdateHeaders();
            UpdateSelectionState();
            ShowSummary();
        }

        private void BuildStations(MovementSheet sheet)
        {
            // Replacing the items clears any selected item, and an editable ComboBox clears its
            // text along with it. The typed station is the setting, so it is carried across.
            string station = StationBox.Text;
            StationBox.ItemsSource = sheet.Stations.ToList();
            StationBox.Text = station;
        }

        // Every station's own carriers, ticked by default so a freshly opened file starts
        // narrowed to them; a file with none of these present falls back to unrestricted.
        private static readonly HashSet<string> DefaultOperatorCodes = new HashSet<string>(
            new[] { "S3", "CJT", "CKS", "CSB", "DHK", "KII", "SIA" }, StringComparer.OrdinalIgnoreCase);

        private void BuildMovementTypes(MovementSheet sheet) =>
            MovementTypeFilter.ItemsSource = sheet.MovementTypes
                .Select(type => new MultiSelectItem(type, type, isSelected: true)
                {
                    ToolTip = ReportOptions.IsFlightType(type)
                        ? "Flight movements"
                        : "Ground runs and tows carry no departure or delay codes",
                })
                .ToList();

        private void BuildOperators(MovementSheet sheet) =>
            OperatorFilter.ItemsSource = sheet.Operators
                .Select(op => new MultiSelectItem(op, WithLabel(op, _mappings.Operators),
                                                   isSelected: DefaultOperatorCodes.Contains(op)))
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
            // The initial text and the ticked radio button raise their events while the window
            // is still being built, before the controls further down the page exist.
            if (_loading || !IsLoaded) return;
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
                Layout = _layout,
                ShowDebugSummary = _showDebugSummary,
                SearchText = SearchBox.Text.Trim(),
                SortColumn = _sortColumn,
                SortDescending = _sortDescending,
            };

            foreach (KeyValuePair<int, bool> pair in _mxOverrides)
                options.MxOverrides[pair.Key] = pair.Value;
            foreach (int row in _hiddenRows)
                options.HiddenRows.Add(row);

            options.MinimumDelayMinutes =
                int.TryParse(MinimumDelayBox.Text.Trim(), NumberStyles.Integer,
                             CultureInfo.InvariantCulture, out int minutes) && minutes >= 0
                    ? minutes
                    : ReportOptions.DefaultMinimumDelayMinutes;

            options.DateFrom = DateFromPicker.SelectedDate;
            options.DateTo = DateToPicker.SelectedDate;
            options.DateFormat = _dateFormat;
            options.MxFilter = MxOnlyRadio.IsChecked == true ? MxFilter.MxOnly
                             : MxNotRadio.IsChecked == true ? MxFilter.NotMx
                             : MxFilter.All;

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
            if (_sheet == null)
            {
                UpdateHeaders();
                UpdateCommands();
                return;
            }

            _model = ReportBuilder.Build(_sheet, _mappings, CurrentOptions());
            RefreshList();

            ShowWarnings(_model.Warnings);

            UpdateHiddenBar();
            UpdateHeaders();
            UpdateEmptyState();
            UpdateCommands();
            UpdateSearchCount();
            ShowSummary();
        }

        // ---- status bar ----------------------------------------------------

        /// <summary>
        /// Shows a message about something just done. It stays long enough to be read, then
        /// the summary returns; hovering over it keeps the whole message readable meanwhile.
        /// </summary>
        private void Status(string message)
        {
            _statusTimer.Stop();
            StatusGlyph.Text = "";
            StatusText.Text = message;
            StatusText.ToolTip = message;
            _statusTimer.Interval = TimeSpan.FromSeconds(Math.Max(6, message.Length / 15.0));
            _statusTimer.Start();
        }

        /// <summary>
        /// The status bar's standing text: the report's summary, worded as the workbook words
        /// it, so nothing that leaves the report is out of sight. The tooltip adds the file,
        /// its period and every count in the detail line.
        /// </summary>
        private void ShowSummary()
        {
            _statusTimer.Stop();
            StatusGlyph.Text = "";

            if (_model == null)
            {
                StatusText.Text = "Drop a movement sheet anywhere in this window, or press Ctrl+O.";
                StatusText.ToolTip = null;
                return;
            }

            StatusText.Text = ReportSummary.Line(ReportSummary.Metrics(_model)).Replace(ReportSummary.Separator, "  ·  ");

            var tip = new List<string> { _sourcePath };
            if (_model.PeriodText.Length > 0) tip.Add("Period " + _model.PeriodText);
            tip.Add(string.Empty);
            tip.AddRange(ReportSummary.Metrics(_model).Select(m => m.Key + ": " + m.Value));
            tip.Add(string.Empty);
            tip.Add("Detail");
            tip.AddRange(ReportSummary.Detail(_model).Select(m => "  " + m.Key + ": " + m.Value));
            StatusText.ToolTip = string.Join(Environment.NewLine, tip);
        }

        /// <summary>
        /// Puts the rebuilt flights in the list. Every rebuild replaces every row, which would
        /// otherwise throw the list back to the top and lose the selection part way through
        /// working down a long report, so both are carried across by source row.
        /// </summary>
        private void RefreshList()
        {
            if (_model == null) return;

            bool reset = _resetView;
            _resetView = false;

            var selected = new HashSet<int>(SelectedFlights().Select(f => f.SourceRowNumber));
            ScrollViewer? scroll = FindDescendant<ScrollViewer>(FlightList);
            double vertical = scroll?.VerticalOffset ?? 0;
            double horizontal = scroll?.HorizontalOffset ?? 0;

            List<ReportFlight> shown = _showHidden ? _model.FlightsIncludingHidden : _model.Flights;
            _restoringSelection = true;
            try
            {
                FlightList.ItemsSource = shown;
                if (!reset)
                {
                    foreach (ReportFlight flight in shown.Where(f => selected.Contains(f.SourceRowNumber)))
                        FlightList.SelectedItems.Add(flight);
                }
            }
            finally
            {
                _restoringSelection = false;
            }
            UpdateSelectionState();

            if (scroll == null) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                scroll.ScrollToVerticalOffset(reset ? 0 : vertical);
                scroll.ScrollToHorizontalOffset(reset ? 0 : horizontal);
            }));
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
            WarningPanel.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            WarningText.Text = string.Join(Environment.NewLine, list);
        }

        private void UpdateEmptyState()
        {
            if (_sheet == null)
            {
                FlightList.Visibility = Visibility.Hidden;
                EmptyState.Visibility = Visibility.Visible;
                EmptyGlyph.Text = "";
                EmptyTitle.Text = "Nothing loaded yet";
                EmptyDetail.Text = "Drop a movement sheet anywhere in this window, or press Ctrl+O.";
                return;
            }

            FlightList.Visibility = Visibility.Visible;
            if (FlightList.Items.Count > 0)
            {
                EmptyState.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyState.Visibility = Visibility.Visible;
            EmptyGlyph.Text = "";
            bool searching = _model != null && _model.Options.SearchText.Length > 0;
            EmptyTitle.Text = searching
                ? $"No flights match \"{_model!.Options.SearchText}\""
                : "No flights match the current filters";
            EmptyDetail.Text = searching
                ? "Press Esc in the search box to clear it."
                : "The summary in the status bar says where every departure went; hover over it for the detail.";
        }

        private void UpdateCommands()
        {
            bool hasReport = _model != null && _model.ReportedFlights > 0;
            List<ReportFlight> selected = SelectedFlights();
            bool reportable = selected.Any(f => !f.IsHidden);

            BtnClose.IsEnabled = MenuClose.IsEnabled = _sheet != null;
            BtnGenerate.IsEnabled = MenuGenerate.IsEnabled = hasReport;
            BtnPreview.IsEnabled = MenuPreview.IsEnabled = hasReport;
            BtnEmail.IsEnabled = MenuEmail.IsEnabled = hasReport;

            BtnSelectAll.IsEnabled = FlightList.Items.Count > 0;
            BtnSelectNone.IsEnabled = selected.Count > 0;
            BtnHide.IsEnabled = reportable;
            BtnUnhideAll.IsEnabled = _hiddenRows.Count > 0;
            BtnReportSelected.IsEnabled = MenuReportSelected.IsEnabled = reportable;
            BtnResetSort.IsEnabled = _sortColumn != ReportSortColumn.Date || _sortDescending;
        }

        /// <summary>Hidden flights are never silent: the bar says how many and offers them back.</summary>
        private void UpdateHiddenBar()
        {
            int hidden = _model?.ExcludedHidden ?? 0;
            HiddenBar.Visibility = hidden > 0 ? Visibility.Visible : Visibility.Collapsed;
            HiddenText.Text = hidden == 1
                ? "1 flight is hidden by hand. It is left out of the report and counted on its summary."
                : $"{hidden} flights are hidden by hand. They are left out of the report and counted on its summary.";
            HiddenShowButton.Content = _showHidden ? "Stop showing them" : "Show them";
        }

        // ---- search --------------------------------------------------------

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            bool empty = SearchBox.Text.Length == 0;
            SearchPlaceholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            SearchClearButton.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
            if (_loading) return;

            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void ApplySearch()
        {
            Recompute();
            if (_model == null || _model.Options.SearchText.Length == 0) return;
            Status($"{Flights(_model.ReportedFlights)} match \"{_model.Options.SearchText}\". " +
                   $"{Flights(_model.ExcludedBySearch)} that do not are left out of the report and counted on its summary.");
        }

        /// <summary>"2 of 8" beside the search bar while a search is narrowing the list.</summary>
        private void UpdateSearchCount()
        {
            bool searching = _model != null && _model.Options.SearchText.Length > 0;
            SearchCount.Text = searching
                ? $"{_model!.ReportedFlights} of {_model.ReportedFlights + _model.ExcludedBySearch}"
                : string.Empty;
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ClearSearch();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter || e.Key == Key.Down)
            {
                // Straight into the results, as Outlook does.
                _searchTimer.Stop();
                ApplySearch();
                if (FlightList.Items.Count > 0)
                {
                    if (FlightList.SelectedItems.Count == 0) FlightList.SelectedIndex = 0;
                    (FlightList.ItemContainerGenerator.ContainerFromIndex(FlightList.SelectedIndex) as UIElement)?.Focus();
                }
                e.Handled = true;
            }
        }

        private void OnClearSearch(object sender, RoutedEventArgs e) => ClearSearch();

        private void ClearSearch()
        {
            if (SearchBox.Text.Length == 0) return;
            SearchBox.Text = string.Empty;
            // Clearing is applied at once, not after the typing pause.
            _searchTimer.Stop();
            Recompute();
            SearchBox.Focus();
        }

        private void OnFocusSearch(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }

        // ---- column headers: sorting and filter tags -----------------------

        private void OnHeaderClick(object sender, RoutedEventArgs e)
        {
            // Every button click in the list bubbles here, including the row and MX boxes.
            if (!(e.OriginalSource is GridViewColumnHeader header) || !(header.Column?.Header is ColumnHeader column))
                return;

            if (_sortColumn == column.SortColumn)
            {
                _sortDescending = !_sortDescending;
            }
            else
            {
                _sortColumn = column.SortColumn;
                _sortDescending = false;
            }

            Recompute();
            if (_sheet != null)
            {
                Status($"Sorted by {column.Title}, {(_sortDescending ? "descending" : "ascending")}. " +
                       "The workbook and the email follow the same order.");
            }
        }

        private void OnResetSort(object sender, RoutedEventArgs e)
        {
            _sortColumn = ReportSortColumn.Date;
            _sortDescending = false;
            Recompute();
        }

        private void UpdateHeaders()
        {
            foreach (ColumnHeader header in _headers)
            {
                header.SetSort(header.SortColumn == _sortColumn, _sortDescending);
                string filter = FilterDescription(header.Filter);
                header.SetFilter(filter.Length > 0,
                                 filter.Length > 0 ? filter + Environment.NewLine + "Click to change it." : string.Empty);
            }
        }

        /// <summary>What a filter is doing to a column, or empty when it is not narrowing anything.</summary>
        private string FilterDescription(HeaderFilter filter)
        {
            if (_sheet == null || _model == null) return string.Empty;
            ReportOptions options = _model.Options;

            switch (filter)
            {
                case HeaderFilter.Date:
                    bool narrowed =
                        (options.DateFrom.HasValue &&
                         (!_sheet.PeriodStart.HasValue || options.DateFrom.Value.Date > _sheet.PeriodStart.Value.Date)) ||
                        (options.DateTo.HasValue &&
                         (!_sheet.PeriodEnd.HasValue || options.DateTo.Value.Date < _sheet.PeriodEnd.Value.Date));
                    return narrowed
                        ? $"Dates from {Day(options.DateFrom)} to {Day(options.DateTo)}, narrower than the file's period."
                        : string.Empty;
                case HeaderFilter.Registration:
                    return Describe("Tail numbers", RegistrationFilter);
                case HeaderFilter.Operator:
                    return Describe("Operators", OperatorFilter);
                case HeaderFilter.DelayCodes:
                    return Describe("Delay codes", DelayCodeFilter);
                case HeaderFilter.Delay:
                    if (options.MinimumDelayMinutes <= 0) return string.Empty;
                    return options.ThresholdBasis == DelayThresholdBasis.ActualDelay
                        ? $"At least {options.MinimumDelayMinutes} minutes late by the clock (ATD − STD)."
                        : $"At least {options.MinimumDelayMinutes} minutes of included delay codes.";
                case HeaderFilter.Mx:
                    return options.MxFilter == MxFilter.MxOnly ? "MX delays only."
                         : options.MxFilter == MxFilter.NotMx ? "MX delays left out."
                         : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static string Describe(string name, MultiSelectDropdown dropdown) =>
            dropdown.IsUnrestricted ? string.Empty : name + " limited to:" + Environment.NewLine + dropdown.SelectionDescription;

        private static string Day(DateTime? date) =>
            date?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? "any date";

        private void OnHeaderFilterClick(object sender, RoutedEventArgs e)
        {
            // The header behind the tag would otherwise take this as a click to sort.
            e.Handled = true;
            if (!((sender as FrameworkElement)?.DataContext is ColumnHeader column)) return;

            SetOptionsPane(true);
            // The pane may have only just opened, so wait for it to be laid out before focusing into it.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => OpenFilter(column.Filter)));
        }

        private void OpenFilter(HeaderFilter filter)
        {
            switch (filter)
            {
                case HeaderFilter.Registration: OpenDropdown(RegistrationFilter); break;
                case HeaderFilter.Operator: OpenDropdown(OperatorFilter); break;
                case HeaderFilter.DelayCodes: OpenDropdown(DelayCodeFilter); break;
                case HeaderFilter.Date:
                    DateFromPicker.BringIntoView();
                    DateFromPicker.Focus();
                    break;
                case HeaderFilter.Delay:
                    MinimumDelayBox.BringIntoView();
                    MinimumDelayBox.Focus();
                    MinimumDelayBox.SelectAll();
                    break;
                case HeaderFilter.Mx:
                    RadioButton chosen = MxOnlyRadio.IsChecked == true ? MxOnlyRadio
                                       : MxNotRadio.IsChecked == true ? MxNotRadio
                                       : MxAllRadio;
                    chosen.BringIntoView();
                    chosen.Focus();
                    break;
            }
        }

        private static void OpenDropdown(MultiSelectDropdown dropdown)
        {
            dropdown.BringIntoView();
            dropdown.Open();
        }

        // ---- rows: selection, hiding, reporting a selection ----------------

        private List<ReportFlight> SelectedFlights() => FlightList.SelectedItems.OfType<ReportFlight>().ToList();

        private void OnFlightSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_restoringSelection) UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            int total = FlightList.Items.Count;
            int selected = FlightList.SelectedItems.Count;

            if (_selectAllCheck != null)
            {
                _selectAllCheck.IsChecked = total > 0 && selected == total ? true
                                          : selected == 0 ? false
                                          : (bool?)null;
            }
            SelectionText.Text = selected > 0 ? $"{selected} of {total} selected"
                               : total > 0 ? $"{Flights(total)} listed"
                               : string.Empty;
            UpdateCommands();
        }

        private void OnSelectAllCheckLoaded(object sender, RoutedEventArgs e)
        {
            _selectAllCheck = sender as CheckBox;
            UpdateSelectionState();
        }

        private void OnSelectAll(object sender, RoutedEventArgs e) => FlightList.SelectAll();

        private void OnSelectNone(object sender, RoutedEventArgs e) => FlightList.UnselectAll();

        /// <summary>
        /// The header box has already toggled by the time this runs: ticked selects everything,
        /// cleared selects nothing, and the selection then sets the box's own state again.
        /// </summary>
        private void OnSelectAllHeaderClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if ((sender as CheckBox)?.IsChecked == true) FlightList.SelectAll();
            else FlightList.UnselectAll();
            UpdateSelectionState();
        }

        private void OnFlightListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                HideSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && FlightList.SelectedItems.Count > 0)
            {
                FlightList.UnselectAll();
                e.Handled = true;
            }
        }

        private void OnRowMenuOpened(object sender, RoutedEventArgs e)
        {
            if (!(sender is ContextMenu menu)) return;

            List<ReportFlight> selected = SelectedFlights();
            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
            {
                switch (item.Tag as string)
                {
                    case "Hide":
                    case "Report":
                        item.IsEnabled = selected.Any(f => !f.IsHidden);
                        break;
                    case "Unhide":
                        item.Visibility = _showHidden ? Visibility.Visible : Visibility.Collapsed;
                        item.IsEnabled = selected.Any(f => f.IsHidden);
                        break;
                    case "UnhideAll":
                        item.IsEnabled = _hiddenRows.Count > 0;
                        break;
                }
            }
        }

        private void OnHideSelected(object sender, RoutedEventArgs e) => HideSelected();

        private void HideSelected()
        {
            List<ReportFlight> flights = SelectedFlights().Where(f => !f.IsHidden).ToList();
            if (flights.Count == 0) return;

            foreach (ReportFlight flight in flights) _hiddenRows.Add(flight.SourceRowNumber);
            Recompute();
            Status($"Hid {Flights(flights.Count)}. Hidden flights are left out of the report and counted on its " +
                   "summary; Unhide all brings them back.");
        }

        private void OnUnhideSelected(object sender, RoutedEventArgs e)
        {
            List<ReportFlight> flights = SelectedFlights().Where(f => f.IsHidden).ToList();
            if (flights.Count == 0) return;

            foreach (ReportFlight flight in flights) _hiddenRows.Remove(flight.SourceRowNumber);
            Recompute();
            Status($"Brought back {Flights(flights.Count)}.");
        }

        private void OnUnhideAll(object sender, RoutedEventArgs e)
        {
            if (_hiddenRows.Count == 0) return;
            int count = _model?.ExcludedHidden ?? _hiddenRows.Count;
            _hiddenRows.Clear();
            Recompute();
            Status($"Brought back {Flights(count)}.");
        }

        private void OnToggleShowHidden(object sender, RoutedEventArgs e)
        {
            if (!_synchronizing) SetShowHidden(ToggleShowHidden.IsChecked == true);
        }

        private void OnHiddenBarShow(object sender, RoutedEventArgs e) => SetShowHidden(!_showHidden);

        private void SetShowHidden(bool show)
        {
            _showHidden = show;
            _synchronizing = true;
            ToggleShowHidden.IsChecked = show;
            _synchronizing = false;

            RefreshList();
            UpdateHiddenBar();
            UpdateEmptyState();
        }

        private void OnReportSelectedMenu(object sender, RoutedEventArgs e) => OpenButtonMenu(BtnReportSelected);

        /// <summary>
        /// The report as it would be with only the selected flights. Every other flight that
        /// would have been reported is counted on its summary as not selected, so a partial
        /// report still says it is partial.
        /// </summary>
        private ReportModel? SelectedModel()
        {
            if (_sheet == null) return null;

            List<int> rows = SelectedFlights().Where(f => !f.IsHidden).Select(f => f.SourceRowNumber).ToList();
            if (rows.Count == 0)
            {
                Status("Select one or more flights first. Hidden flights are never reported.");
                return null;
            }

            ReportOptions options = CurrentOptions();
            foreach (int row in rows) options.SelectedRows.Add(row);
            return ReportBuilder.Build(_sheet, _mappings, options);
        }

        private void OnGenerateSelected(object sender, RoutedEventArgs e)
        {
            ReportModel? model = SelectedModel();
            if (model != null) SaveReport(model, selection: true);
        }

        private void OnPreviewSelected(object sender, RoutedEventArgs e)
        {
            ReportModel? model = SelectedModel();
            if (model != null) PreviewInExcel(model);
        }

        private void OnEmailSelected(object sender, RoutedEventArgs e)
        {
            ReportModel? model = SelectedModel();
            if (model != null) SendEmail(model, selection: true);
        }

        // ---- MX corrections ------------------------------------------------

        /// <summary>
        /// Flips a flight's MX marker. The box's own toggle is ignored; the rebuilt row shows
        /// the result. Flipping back to what the mapping would say anyway drops the override,
        /// so the debug summary's overridden count only counts genuine corrections.
        /// </summary>
        private void OnMxClick(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is ReportFlight flight)) return;

            int row = flight.SourceRowNumber;
            bool next = !flight.IsMxDelay;
            if (next == flight.IsMxCoded) _mxOverrides.Remove(row);
            else _mxOverrides[row] = next;

            Recompute();
        }

        // ---- outputs -------------------------------------------------------

        private void OnGenerate(object sender, RoutedEventArgs e)
        {
            if (_model != null && _model.ReportedFlights > 0) SaveReport(_model, selection: false);
        }

        private void OnPreview(object sender, RoutedEventArgs e)
        {
            if (_model != null && _model.ReportedFlights > 0) PreviewInExcel(_model);
        }

        private void OnEmail(object sender, RoutedEventArgs e)
        {
            if (_model != null && _model.ReportedFlights > 0) SendEmail(_model, selection: false);
        }

        private void SaveReport(ReportModel model, bool selection)
        {
            var dialog = new SaveFileDialog
            {
                Title = selection ? "Save the selected flights as a report" : "Save delay report",
                Filter = "Excel workbook (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = SuggestedName(model, selection),
                InitialDirectory = _settings.Get(KeyLastFolder, string.Empty),
            };
            if (dialog.ShowDialog(this) != true) return;

            try
            {
                ReportWriter.Write(dialog.FileName, model);
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
        /// Writes the report to a temporary file and opens it straight away: no save dialog,
        /// no prompt and no settings saved, so it is a quick look rather than a save.
        /// </summary>
        private void PreviewInExcel(ReportModel model)
        {
            string folder = TempFolder;
            string path = Path.Combine(folder, "preview.xlsx");
            try
            {
                Directory.CreateDirectory(folder);
                try
                {
                    ReportWriter.Write(path, model);
                }
                catch (IOException)
                {
                    // Excel still has the previous preview open and locked; use a fresh name
                    // instead of blocking the user.
                    path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".xlsx");
                    ReportWriter.Write(path, model);
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
                Status($"Opened a preview of {Flights(model.ReportedFlights)} in Excel.");
            }
            catch (Exception ex)
            {
                Status("Could not open the preview: " + ex.Message);
            }
        }

        /// <summary>
        /// Writes the compact email as an .eml draft and hands it to the default mail app, as
        /// OFT Scrubber does. Nothing is sent from here: the user reviews the draft in their
        /// mail app and presses Send there.
        /// </summary>
        private void SendEmail(ReportModel model, bool selection)
        {
            if (model.ReportedFlights == 0)
            {
                Status("There are no flights to email.");
                return;
            }

            string workbookName = SuggestedName(model, selection);
            EmailAttachment? attachment = _attachWorkbook
                ? new EmailAttachment(workbookName, WorkbookMimeType, ReportWriter.ToBytes(model))
                : null;
            EmailDraft draft = DelayEmail.Compose(model, _emailTo, _emailCc, attachment);
            byte[] bytes = EmlDraftWriter.ToBytes(draft, out List<string> notes);

            string path = Path.Combine(TempFolder,
                Path.GetFileNameWithoutExtension(workbookName) + "-" +
                DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture) + ".eml");
            try
            {
                Directory.CreateDirectory(TempFolder);
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Status("Could not write the email draft: " + ex.Message);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                Status("The draft was saved as " + path + ", but no mail app opened it: " + ex.Message);
                return;
            }

            var message = new List<string>
            {
                $"Opened an email draft of {Flights(model.ReportedFlights)} in your mail app. Review it there and press Send.",
            };
            message.AddRange(notes);
            if (draft.To.Count == 0) message.Add("Recipients set in Settings, Email, are filled in for you.");
            Status(string.Join(" ", message));
        }

        private static string TempFolder => Path.Combine(Path.GetTempPath(), "Delay Reporter");

        private static string SuggestedName(ReportModel model, bool selection)
        {
            string date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return $"{model.Station}-departure-delays-{date}{(selection ? "-selected" : string.Empty)}.xlsx";
        }

        // ---- presets -------------------------------------------------------

        private void BuildPresetButtons()
        {
            PresetButtons.Children.Clear();
            foreach (string name in BuiltInPresets.Names)
            {
                string captured = name;
                var button = new Button
                {
                    Style = (Style)FindResource("RibbonButton"),
                    Tag = PresetGlyphs.TryGetValue(name, out string? glyph) ? glyph : "",
                    Content = name,
                    ToolTip = BuiltInPresets.Description(name),
                };
                button.Click += (s, e) => ApplyPreset(BuiltInPresets.Build(captured)!);
                PresetButtons.Children.Add(button);
            }
        }

        private void RefreshCustomPresets()
        {
            bool any = PresetStore.List().Count > 0;
            BtnCustomPresets.IsEnabled = any;
            BtnManagePresets.IsEnabled = any;
        }

        private void OnCustomPresetsMenu(object sender, RoutedEventArgs e)
        {
            CustomPresetMenu.Items.Clear();
            foreach (string name in PresetStore.List())
            {
                string captured = name;
                // Doubled so an underscore in a name shows, rather than being taken as an access key.
                var item = new MenuItem { Header = name.Replace("_", "__") };
                item.Click += (s, args) =>
                {
                    ReportPreset? preset = PresetStore.Load(captured);
                    if (preset != null) ApplyPreset(preset);
                    else Status("The preset " + captured + " could not be read.");
                };
                CustomPresetMenu.Items.Add(item);
            }
            OpenButtonMenu(BtnCustomPresets);
        }

        private void OnSavePreset(object sender, RoutedEventArgs e)
        {
            string? name = PromptDialog.Ask(this, "Save preset",
                "Name for the current station, minimum delay and filters. The dates are not saved; they always come from the file.",
                string.Empty);
            if (name == null) return;

            bool exists = PresetStore.List().Any(n => string.Equals(n, PresetStore.Sanitise(name), StringComparison.OrdinalIgnoreCase));
            if (exists && MessageBox.Show(this, "Replace the preset " + name + "?", "Delay Reporter",
                                          MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;

            PresetStore.Save(name, CapturePreset(name));
            RefreshCustomPresets();
            Status("Saved the preset " + name + ".");
        }

        private void OnManagePresets(object sender, RoutedEventArgs e)
        {
            var dialog = new PresetManagerDialog { Owner = this };
            dialog.ShowDialog();
            if (dialog.Changed) RefreshCustomPresets();
        }

        private ReportPreset CapturePreset(string name)
        {
            ReportOptions options = CurrentOptions();
            var preset = new ReportPreset
            {
                Name = name,
                Station = options.Station,
                MinimumDelayMinutes = options.MinimumDelayMinutes,
                ThresholdBasis = options.ThresholdBasis,
                MxFilter = options.MxFilter,
            };

            // Nothing or everything ticked saves as nothing, meaning no restriction, the same
            // convention every one of the four lists follows.
            preset.MovementTypes.AddRange(MovementTypeFilter.FilterValues);
            preset.Operators.AddRange(OperatorFilter.FilterValues);
            preset.DelayCodes.AddRange(DelayCodeFilter.FilterValues);
            preset.Registrations.AddRange(RegistrationFilter.FilterValues);
            return preset;
        }

        private void ApplyPreset(ReportPreset preset)
        {
            List<string> notes;
            _loading = true;
            try
            {
                if (preset.Station.Length > 0) StationBox.Text = preset.Station;
                MinimumDelayBox.Text = preset.MinimumDelayMinutes.ToString(CultureInfo.InvariantCulture);
                BasisActualRadio.IsChecked = preset.ThresholdBasis == DelayThresholdBasis.ActualDelay;
                BasisCodesRadio.IsChecked = preset.ThresholdBasis != DelayThresholdBasis.ActualDelay;
                MxAllRadio.IsChecked = preset.MxFilter == MxFilter.All;
                MxOnlyRadio.IsChecked = preset.MxFilter == MxFilter.MxOnly;
                MxNotRadio.IsChecked = preset.MxFilter == MxFilter.NotMx;

                if (_sheet != null)
                {
                    notes = ApplyPresetLists(preset);
                    _pendingPreset = null;
                }
                else
                {
                    _pendingPreset = preset;
                    notes = new List<string> { "Its filters apply once a movement sheet is open." };
                }
            }
            finally
            {
                _loading = false;
            }

            Recompute();
            notes.Insert(0, $"Applied the preset {preset.Name}.");
            Status(string.Join(" ", notes));
        }

        /// <summary>
        /// Ticks a preset's values in the loaded file's lists. A value the file does not hold is
        /// named rather than skipped quietly, and a list left with nothing to tick says that it
        /// is no longer narrowing anything, since nothing ticked means everything.
        /// </summary>
        private List<string> ApplyPresetLists(ReportPreset preset)
        {
            var notes = new List<string>();
            if (_sheet == null) return notes;

            ApplyList(MovementTypeFilter, preset.MovementTypes, "movement types", notes, v => v);
            ApplyList(OperatorFilter, preset.Operators, "operators", notes, v => v);
            ApplyList(DelayCodeFilter, preset.DelayCodes, "delay codes", notes, MappingTable.Normalize);
            ApplyList(RegistrationFilter, preset.Registrations, "tail numbers", notes, v => v);
            return notes;
        }

        /// <summary>Returns false when none of the wanted values are in the file.</summary>
        private static bool ApplyList(MultiSelectDropdown dropdown, List<string> values, string noun,
                                      List<string> notes, Func<string, string> key)
        {
            if (values.Count == 0)
            {
                dropdown.ClearSelection();
                return true;
            }

            List<string> available = dropdown.ItemsSource?.Select(i => i.Value).ToList() ?? new List<string>();
            var wanted = new HashSet<string>(values.Select(key), StringComparer.OrdinalIgnoreCase);
            List<string> present = available.Where(v => wanted.Contains(key(v))).ToList();
            var found = new HashSet<string>(present.Select(key), StringComparer.OrdinalIgnoreCase);
            List<string> missing = values.Where(v => !found.Contains(key(v))).ToList();

            dropdown.SelectValues(present);
            if (present.Count == 0)
            {
                notes.Add($"None of its {noun} ({string.Join(", ", values)}) are in this file, so that list is not narrowing anything.");
                return false;
            }
            if (missing.Count > 0) notes.Add($"Not in this file: {noun} {string.Join(", ", missing)}.");
            return true;
        }

        // ---- view ----------------------------------------------------------

        private void OnCodesDisplayMenu(object sender, RoutedEventArgs e)
        {
            MenuCodesVisible.IsChecked = UnselectedCodes == UnselectedCodeDisplay.Visible;
            MenuCodesDimmed.IsChecked = UnselectedCodes == UnselectedCodeDisplay.Dimmed;
            MenuCodesHidden.IsChecked = UnselectedCodes == UnselectedCodeDisplay.Hidden;
            OpenButtonMenu(BtnCodesDisplay);
        }

        private void OnCodesDisplayChoice(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem item)) return;
            UnselectedCodes = ParseEnum(item.Tag as string ?? string.Empty, UnselectedCodeDisplay.Visible);
            SaveSettings();
        }

        private static void OpenButtonMenu(Button button)
        {
            if (button.ContextMenu == null) return;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }

        // ---- settings and helpers ------------------------------------------

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsDialog
            {
                Owner = this,
                UnselectedCodes = UnselectedCodes,
                UseOperatorLabels = _useOperatorLabels,
                AircraftFormat = _aircraftFormat,
                Layout = _layout,
                ShowDebugSummary = _showDebugSummary,
                EmailTo = _emailTo,
                EmailCc = _emailCc,
                AttachWorkbook = _attachWorkbook,
                DateFormat = _dateFormat,
            };
            if (dialog.ShowDialog() != true) return;

            if (dialog.DateFormat != _dateFormat)
            {
                _dateFormat = dialog.DateFormat;
                ApplyDatePickerFormat();
            }

            UnselectedCodes = dialog.UnselectedCodes;
            if (dialog.UseOperatorLabels != _useOperatorLabels)
            {
                _useOperatorLabels = dialog.UseOperatorLabels;
                SizeOperatorColumn();
            }
            _aircraftFormat = dialog.AircraftFormat;
            _layout = dialog.Layout;
            _showDebugSummary = dialog.ShowDebugSummary;
            _emailTo = dialog.EmailTo;
            _emailCc = dialog.EmailCc;
            _attachWorkbook = dialog.AttachWorkbook;

            SaveSettings();
            Recompute();
        }

        private void SaveSettings()
        {
            if (IsOptionsPaneOpen && OptionsColumn.ActualWidth >= MinimumOptionsWidth)
                _optionsWidth = OptionsColumn.ActualWidth;

            _settings.Set(KeyStation, StationBox.Text.Trim().ToUpperInvariant());
            _settings.Set(KeyMinimumDelay, CurrentOptions().MinimumDelayMinutes);
            _settings.Set(KeyBasis, BasisActualRadio.IsChecked == true ? "actual" : "codes");
            _settings.Set(KeyUnselectedCodes, UnselectedCodes.ToString());
            _settings.Set(KeyOperatorLabels, _useOperatorLabels);
            _settings.Set(KeyAircraftFormat, _aircraftFormat.ToString());
            _settings.Set(KeyLayout, _layout.ToString());
            _settings.Set(KeyDebugSummary, _showDebugSummary);
            _settings.Set(KeyEmailTo, _emailTo);
            _settings.Set(KeyEmailCc, _emailCc);
            _settings.Set(KeyEmailAttach, _attachWorkbook);
            _settings.Set(KeyOptionsPane, IsOptionsPaneOpen);
            _settings.Set(KeyOptionsWidth, (int)Math.Round(_optionsWidth));
            _settings.Set(KeyDateFormat, _dateFormat);
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

        /// <summary>
        /// Settings, presets and the mapping CSVs all live under one folder in %APPDATA%, so
        /// one command opens it rather than one per kind of file.
        /// </summary>
        private void OnOpenDataFolder(object sender, RoutedEventArgs e)
        {
            string? error = SettingsDialog.OpenDataFolder();
            if (error != null) Status("Could not open the data folder: " + error);
        }

        // ---- minimum delay -------------------------------------------------

        private const int MinimumDelayStep = 5;

        private void OnMinimumDelayUp(object sender, RoutedEventArgs e) => StepMinimumDelay(+1);

        private void OnMinimumDelayDown(object sender, RoutedEventArgs e) => StepMinimumDelay(-1);

        private void OnMinimumDelayKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up) StepMinimumDelay(+1);
            else if (e.Key == Key.Down) StepMinimumDelay(-1);
            else return;
            e.Handled = true;
        }

        /// <summary>Moves to the next multiple of five in that direction, never below zero.</summary>
        private void StepMinimumDelay(int direction)
        {
            int current = CurrentOptions().MinimumDelayMinutes;
            int next = direction > 0
                ? (current / MinimumDelayStep + 1) * MinimumDelayStep
                : Math.Max(0, (current - 1) / MinimumDelayStep * MinimumDelayStep);
            MinimumDelayBox.Text = next.ToString(CultureInfo.InvariantCulture);
            MinimumDelayBox.CaretIndex = MinimumDelayBox.Text.Length;
        }

        // ---- date format ---------------------------------------------------

        private void OnDateFormatMenuOpened(object sender, RoutedEventArgs e)
        {
            if (!(sender is ContextMenu menu)) return;
            foreach (MenuItem item in menu.Items.OfType<MenuItem>())
                item.IsChecked = (item.Tag as string ?? string.Empty) == _dateFormat;
        }

        private void OnDateFormatChoice(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item) SetDateFormat(item.Tag as string ?? string.Empty);
        }

        private void SetDateFormat(string format)
        {
            if (format == _dateFormat) return;
            _dateFormat = format;
            ApplyDatePickerFormat();
            SaveSettings();
            Recompute();
        }

        /// <summary>
        /// The date pickers read and write dates in their culture's short pattern, so each
        /// format is paired with a culture that uses it. Printing as in the file leaves the
        /// pickers on this machine's own format.
        /// </summary>
        private void ApplyDatePickerFormat()
        {
            string culture;
            switch (_dateFormat)
            {
                case "dd.MM.yyyy": culture = "de-DE"; break;
                case "dd/MM/yyyy": case "dd MMM yyyy": culture = "en-GB"; break;
                case "MM/dd/yyyy": culture = "en-US"; break;
                case "yyyy-MM-dd": culture = "sv-SE"; break;
                default: culture = CultureInfo.CurrentCulture.Name; break;
            }
            var language = System.Windows.Markup.XmlLanguage.GetLanguage(culture);
            DateFromPicker.Language = language;
            DateToPicker.Language = language;
        }

        /// <summary>
        /// A carrier name needs a wider column than a code. Set only when the choice changes,
        /// so a width the user dragged is otherwise left alone.
        /// </summary>
        private void SizeOperatorColumn() =>
            OperatorColumn.Width = _useOperatorLabels ? OperatorColumnLabelWidth : OperatorColumnCodeWidth;

        private void OnAbout(object sender, RoutedEventArgs e) =>
            new AboutDialog { Owner = this }.ShowDialog();

        private void OnExit(object sender, RoutedEventArgs e) => Close();

        private static string Flights(int count) => count == 1 ? "1 flight" : count + " flights";

    }
}
