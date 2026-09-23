using System.ComponentModel;

namespace DelayReporter.Controls
{
    /// <summary>
    /// One choice in a <see cref="MultiSelectDropdown"/>: the raw value used for filtering and
    /// the text shown for it, which may carry a mapped label.
    /// </summary>
    public sealed class MultiSelectItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public MultiSelectItem(string value, string? display = null, bool isSelected = false)
        {
            Value = value;
            Display = string.IsNullOrEmpty(display) ? value : display!;
            _isSelected = isSelected;
        }

        /// <summary>The raw code matched against the movement sheet, e.g. "CKS" or "93A".</summary>
        public string Value { get; }

        /// <summary>What the list shows, e.g. "CKS — Kalitta Air".</summary>
        public string Display { get; }

        /// <summary>Optional explanation shown when hovering over the item.</summary>
        public string? ToolTip { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => Display;
    }
}
