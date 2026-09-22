using System;
using System.Reflection;
using System.Windows;
using DelayReporter.Core;
using DelayReporter.Core.Mapping;

namespace DelayReporter.Views
{
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();

            VersionText.Text = "Version " + InformationalVersion();
            MappingsText.Text = MappingStore.DefaultFolder;
            SettingsText.Text = SettingsStore.DefaultPath;
        }

        /// <summary>
        /// The release number with the build timestamp the project file embeds, so the
        /// running executable can be identified even when the source has moved on.
        /// </summary>
        private static string InformationalVersion()
        {
            Assembly assembly = typeof(AboutDialog).Assembly;
            var attribute = (AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(
                assembly, typeof(AssemblyInformationalVersionAttribute));
            return attribute?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
