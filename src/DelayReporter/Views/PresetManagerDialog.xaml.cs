using System.Windows;
using System.Windows.Controls;
using DelayReporter.Core;

namespace DelayReporter.Views
{
    /// <summary>Rename and delete for user-saved presets. The built-in ones never appear here.</summary>
    public partial class PresetManagerDialog : Window
    {
        public PresetManagerDialog()
        {
            InitializeComponent();
            Reload();
        }

        /// <summary>True when anything was renamed or deleted, so the caller knows to rebuild its menu.</summary>
        public bool Changed { get; private set; }

        private void Reload()
        {
            var names = PresetStore.List();
            PresetList.ItemsSource = names;
            EmptyText.Visibility = names.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateButtons();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

        private void UpdateButtons()
        {
            bool hasSelection = PresetList.SelectedItem != null;
            RenameButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
        }

        private void OnRename(object sender, RoutedEventArgs e)
        {
            if (!(PresetList.SelectedItem is string selected)) return;

            string? name = PromptDialog.Ask(this, "Rename preset", "New name for " + selected + ":", selected);
            if (name == null || name == selected) return;

            PresetStore.Rename(selected, name);
            Changed = true;
            Reload();
        }

        private void OnDelete(object sender, RoutedEventArgs e)
        {
            if (!(PresetList.SelectedItem is string selected)) return;

            if (MessageBox.Show(this, "Delete the preset " + selected + "?", "Delay Reporter",
                                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
                return;

            PresetStore.Delete(selected);
            Changed = true;
            Reload();
        }
    }
}
