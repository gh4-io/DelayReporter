using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DelayReporter.Controls;
using DelayReporter.Core;
using DelayReporter.Core.Report;

namespace DelayReporter.Views
{
    /// <summary>
    /// Preferences that change how the report reads rather than what it contains, and where
    /// the email draft goes. The owner supplies the current values and reads them back when
    /// the dialog returns true.
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public const string ReportPageName = "ReportPage";
        public const string PreviewPageName = "PreviewPage";
        public const string EmailPageName = "EmailPage";
        public const string SummaryPageName = "SummaryPage";
        public const string DataPageName = "DataPage";

        public SettingsDialog(string page = ReportPageName)
        {
            InitializeComponent();
            DataFolderText.Text = SettingsStore.DefaultFolder;
            DateFormatBox.SelectedIndex = 0;
            Nav.SelectedItem = Nav.Items.OfType<ListBoxItem>().FirstOrDefault(i => (string)i.Tag == page)
                               ?? Nav.Items[0];
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

        public string EmailTo
        {
            get => EmailToBox.Text.Trim();
            set => EmailToBox.Text = value;
        }

        public string EmailCc
        {
            get => EmailCcBox.Text.Trim();
            set => EmailCcBox.Text = value;
        }

        public bool AttachWorkbook
        {
            get => AttachWorkbookCheck.IsChecked == true;
            set => AttachWorkbookCheck.IsChecked = value;
        }

        /// <summary>A pattern from <see cref="ReportOptions.DateFormats"/>; empty prints dates as the file wrote them.</summary>
        public string DateFormat
        {
            get => (DateFormatBox.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            set => DateFormatBox.SelectedItem =
                DateFormatBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (i.Tag as string ?? string.Empty) == value)
                ?? DateFormatBox.Items[0];
        }

        private void OnNavChanged(object sender, SelectionChangedEventArgs e)
        {
            string page = (Nav.SelectedItem as ListBoxItem)?.Tag as string ?? ReportPageName;
            ReportPage.Visibility = page == ReportPageName ? Visibility.Visible : Visibility.Collapsed;
            PreviewPage.Visibility = page == PreviewPageName ? Visibility.Visible : Visibility.Collapsed;
            EmailPage.Visibility = page == EmailPageName ? Visibility.Visible : Visibility.Collapsed;
            SummaryPage.Visibility = page == SummaryPageName ? Visibility.Visible : Visibility.Collapsed;
            DataPage.Visibility = page == DataPageName ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnOpenDataFolder(object sender, RoutedEventArgs e)
        {
            string? error = OpenDataFolder();
            if (error != null)
                MessageBox.Show(this, "Could not open the data folder. " + error, "Delay Reporter",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>Opens %APPDATA%\Delay Reporter in Explorer; returns why not, or null.</summary>
        public static string? OpenDataFolder()
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.DefaultFolder);
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(SettingsStore.DefaultFolder) { UseShellExecute = true });
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
    }
}
