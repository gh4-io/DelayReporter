using System.ComponentModel;
using DelayReporter.Core.Report;

namespace DelayReporter.Controls
{
    /// <summary>The filter a column header's tag stands for, and opens when clicked.</summary>
    public enum HeaderFilter
    {
        None = 0,
        Date = 1,
        Registration = 2,
        Operator = 3,
        DelayCodes = 4,

        /// <summary>The minimum delay, whichever figure it is measured against.</summary>
        Delay = 5,

        Mx = 6,
    }

    /// <summary>
    /// What a grid column header shows: its title, then, right-aligned, a sort tag and, on a
    /// column that can be filtered, a filter tag. Both are always there, quiet until they
    /// apply, so every heading reads the same way. The window updates these after every
    /// rebuild; the header template only reads them.
    /// </summary>
    public sealed class ColumnHeader : INotifyPropertyChanged
    {
        // Segoe Fluent Icons / MDL2 ChevronUp, ChevronDown and Sort.
        private const string AscendingGlyph = "";
        private const string DescendingGlyph = "";
        private const string UnsortedGlyph = "";

        private bool _isSorted;
        private bool _descending;
        private bool _isFiltered;
        private string _filterToolTip = string.Empty;

        public ColumnHeader(string title, ReportSortColumn sortColumn, HeaderFilter filter = HeaderFilter.None)
        {
            Title = title;
            SortColumn = sortColumn;
            Filter = filter;
        }

        public string Title { get; }
        public ReportSortColumn SortColumn { get; }
        public HeaderFilter Filter { get; }

        public bool IsSorted => _isSorted;
        public string SortGlyph => !_isSorted ? UnsortedGlyph : _descending ? DescendingGlyph : AscendingGlyph;

        public bool IsFilterable => Filter != HeaderFilter.None;
        public bool IsFiltered => _isFiltered;

        public string FilterToolTip =>
            _isFiltered ? _filterToolTip : $"No filter on {Title}. Click to set one.";

        public string SortToolTip =>
            _isSorted
                ? $"Sorted by {Title.ToLowerInvariant()}, {(_descending ? "descending" : "ascending")}. Click to reverse."
                : $"Sort by {Title.ToLowerInvariant()}";

        /// <summary>Read out by screen readers in place of the glyphs.</summary>
        public string AutomationName =>
            Title + (_isSorted ? (_descending ? ", sorted descending" : ", sorted ascending") : string.Empty) +
            (_isFiltered ? ", filtered" : string.Empty);

        public void SetSort(bool sorted, bool descending)
        {
            if (_isSorted == sorted && _descending == descending) return;
            _isSorted = sorted;
            _descending = descending;
            Changed(nameof(IsSorted));
            Changed(nameof(SortGlyph));
            Changed(nameof(SortToolTip));
            Changed(nameof(AutomationName));
        }

        public void SetFilter(bool filtered, string toolTip)
        {
            if (_isFiltered == filtered && _filterToolTip == toolTip) return;
            _isFiltered = filtered;
            _filterToolTip = toolTip;
            Changed(nameof(IsFiltered));
            Changed(nameof(FilterToolTip));
            Changed(nameof(AutomationName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public override string ToString() => Title;
    }
}
