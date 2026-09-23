using System.Windows;
using DelayReporter.Controls;
using DelayReporter.Core.Report;

namespace DelayReporter.Views
{
    /// <summary>
    /// View preferences that change how the report reads rather than what it contains. The
    /// owner supplies the current values and reads them back when the dialog returns true.
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
        }

        public UnselectedCodeDisplay UnselectedCodes
        {
            get => CodesHiddenRadio.IsChecked == true ? UnselectedCodeDisplay.Hidden
                 : CodesDimmedRadio.IsChecked == true ? UnselectedCodeDisplay.Dimmed
                 : UnselectedCodeDisplay.Visible;
            set
            {
                CodesVisibleRadio.IsChecked = value == UnselectedCodeDisplay.Visible;
                CodesDimmedRadio.IsChecked = value == UnselectedCodeDisplay.Dimmed;
                CodesHiddenRadio.IsChecked = value == UnselectedCodeDisplay.Hidden;
            }
        }

        public bool UseOperatorLabels
        {
            get => OperatorLabelsCheck.IsChecked == true;
            set => OperatorLabelsCheck.IsChecked = value;
        }

        public AircraftLabelFormat AircraftFormat
        {
            get => AircraftFamilyRadio.IsChecked == true ? AircraftLabelFormat.Family
                 : AircraftVariantRadio.IsChecked == true ? AircraftLabelFormat.FamilyAndVariant
                 : AircraftLabelFormat.Full;
            set
            {
                AircraftFamilyRadio.IsChecked = value == AircraftLabelFormat.Family;
                AircraftVariantRadio.IsChecked = value == AircraftLabelFormat.FamilyAndVariant;
                AircraftFullRadio.IsChecked = value == AircraftLabelFormat.Full;
            }
        }

        public bool ShowDebugSummary
        {
            get => DebugSummaryCheck.IsChecked == true;
            set => DebugSummaryCheck.IsChecked = value;
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
