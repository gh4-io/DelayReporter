using System.Windows;
using System.Windows.Input;
using DelayReporter.Core;

namespace DelayReporter.Views
{
    /// <summary>A one-line prompt for a preset name. WPF has no InputBox, and this is the whole need.</summary>
    public partial class PromptDialog : Window
    {
        private PromptDialog()
        {
            InitializeComponent();
        }

        public string Value => InputBox.Text.Trim();

        /// <summary>Returns the entered name, or null if the user cancelled.</summary>
        public static string? Ask(Window owner, string title, string message, string initialValue)
        {
            var dialog = new PromptDialog { Owner = owner, Title = title };
            dialog.MessageText.Text = message;
            dialog.InputBox.Text = initialValue;
            dialog.Loaded += delegate
            {
                dialog.InputBox.Focus();
                dialog.InputBox.SelectAll();
            };
            return dialog.ShowDialog() == true ? dialog.Value : null;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Validate()) DialogResult = true;
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) ErrorText.Visibility = Visibility.Collapsed;
        }

        private bool Validate()
        {
            if (Value.Length == 0) return Fail("Enter a name first.");
            if (PresetStore.Sanitise(Value).Length == 0) return Fail("That name cannot be used for a file. Try letters and numbers.");
            if (BuiltInPresets.IsBuiltIn(Value)) return Fail("That is the name of a built-in preset. Choose another.");
            return true;
        }

        private bool Fail(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
            InputBox.Focus();
            return false;
        }
    }
}
