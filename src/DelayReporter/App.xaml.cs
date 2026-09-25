using System;
using System.Windows;
using System.Windows.Threading;

namespace DelayReporter
{
    public partial class App : Application
    {
        /// <summary>A file path passed on the command line, opened once the window is up.</summary>
        public string? StartupFile { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length > 0 && e.Args[0].Length > 0) StartupFile = e.Args[0];

            DispatcherUnhandledException += OnUnhandledException;
            base.OnStartup(e);
        }

        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // A failure while reading one odd file should not take the application down
            // with a stack trace the user cannot act on.
            MessageBox.Show(
                e.Exception.Message,
                "Delay Reporter",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            e.Handled = true;
        }
    }
}
