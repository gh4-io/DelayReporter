using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DelayReporter.Controls
{
    /// <summary>
    /// A closed face that summarises the selection and opens onto a list of checkboxes.
    ///
    /// It follows the report's filter convention: nothing ticked and everything ticked both
    /// mean "no restriction", and <see cref="FilterValues"/> is empty in either case. That keeps
    /// the report stable when a later file holds a value this one did not.
    /// </summary>
    public partial class MultiSelectDropdown : UserControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable<MultiSelectItem>),
                typeof(MultiSelectDropdown),
                new PropertyMetadata(null, (d, e) => ((MultiSelectDropdown)d).OnItemsSourceChanged()));

        /// <summary>Raised once per user change, including once for "Select all" or "Clear".</summary>
        public static readonly RoutedEvent SelectionChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(SelectionChanged), RoutingStrategy.Direct,
                typeof(RoutedEventHandler), typeof(MultiSelectDropdown));

        private const int FaceListMaxItems = 3;
        private const int FaceListMaxLength = 24;

        private List<MultiSelectItem> _items = new List<MultiSelectItem>();

        /// <summary>Set during bulk changes, so the host recomputes once rather than per item.</summary>
        private bool _bulkUpdate;

        private string _note = string.Empty;

        public MultiSelectDropdown()
        {
            InitializeComponent();
            UpdateFace();
        }

        public IEnumerable<MultiSelectItem>? ItemsSource
        {
            get => (IEnumerable<MultiSelectItem>?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// A line shown under the list and in the face's tooltip, saying what the ticks were
        /// asked for but could not show, such as default carriers this file does not hold. The
        /// list only offers values the file contains, so without it "5 of 11 selected" would not
        /// say why seven defaults became five. It describes how the ticks were set, so it clears
        /// as soon as the user changes them, and when the list is replaced.
        /// </summary>
        public string Note
        {
            get => _note;
            set
            {
                _note = value ?? string.Empty;
                NoteText.Text = _note;
                NoteText.Visibility = _note.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
                UpdateFace();
            }
        }

        public event RoutedEventHandler SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        /// <summary>The values currently ticked, in list order.</summary>
        public IReadOnlyList<string> SelectedValues =>
            _items.Where(i => i.IsSelected).Select(i => i.Value).ToList();

        /// <summary>True when nothing or everything is ticked: the list restricts nothing.</summary>
        public bool IsUnrestricted
        {
            get
            {
                int selected = _items.Count(i => i.IsSelected);
                return selected == 0 || selected == _items.Count;
            }
        }

        /// <summary>
        /// The values to filter on: empty when the list is unrestricted, otherwise the ticked
        /// values. This is what belongs in the matching <c>ReportOptions</c> set.
        /// </summary>
        public IReadOnlyList<string> FilterValues =>
            IsUnrestricted ? (IReadOnlyList<string>)Array.Empty<string>() : SelectedValues;

        /// <summary>Ticks exactly the given values; any not in the list are ignored.</summary>
        public void SelectValues(IEnumerable<string> values)
        {
            var wanted = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
            ApplyBulk(i => wanted.Contains(i.Value));
        }

        public void SelectAll() => ApplyBulk(_ => true);

        public void ClearSelection() => ApplyBulk(_ => false);

        /// <summary>Opens the list, as a grid header's filter tag does.</summary>
        public void Open()
        {
            FaceButton.Focus();
            FaceButton.IsChecked = true;
        }

        /// <summary>The ticked items one per line, for a tooltip; empty when unrestricted.</summary>
        public string SelectionDescription =>
            IsUnrestricted
                ? string.Empty
                : string.Join(Environment.NewLine, _items.Where(i => i.IsSelected).Select(i => i.Display));

        private void ApplyBulk(Func<MultiSelectItem, bool> selected)
        {
            bool changed = false;
            _bulkUpdate = true;
            try
            {
                foreach (MultiSelectItem item in _items)
                {
                    bool value = selected(item);
                    if (item.IsSelected == value) continue;
                    item.IsSelected = value;
                    changed = true;
                }
            }
            finally
            {
                _bulkUpdate = false;
            }

            UpdateFace();
            if (changed) RaiseSelectionChanged();
        }

        private void OnItemsSourceChanged()
        {
            foreach (MultiSelectItem item in _items) item.PropertyChanged -= OnItemPropertyChanged;

            _items = ItemsSource?.ToList() ?? new List<MultiSelectItem>();
            foreach (MultiSelectItem item in _items) item.PropertyChanged += OnItemPropertyChanged;

            ItemList.ItemsSource = _items;
            EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Note = string.Empty;
            RaiseSelectionChanged();
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MultiSelectItem.IsSelected) || _bulkUpdate) return;
            // Only the user ticks items one at a time; the window always sets them in bulk.
            Note = string.Empty;
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged() => RaiseEvent(new RoutedEventArgs(SelectionChangedEvent, this));

        private void UpdateFace()
        {
            List<MultiSelectItem> selected = _items.Where(i => i.IsSelected).ToList();

            FilterGlyph.Visibility = IsUnrestricted ? Visibility.Collapsed : Visibility.Visible;
            if (IsUnrestricted)
            {
                FaceText.Text = "All";
                FaceButton.ToolTip = _note.Length > 0 ? _note : null;
                return;
            }

            string list = string.Join(", ", selected.Select(i => i.Value));
            FaceText.Text = selected.Count <= FaceListMaxItems && list.Length <= FaceListMaxLength
                ? list
                : $"{selected.Count} of {_items.Count} selected";
            string tip = string.Join(Environment.NewLine, selected.Select(i => i.Display));
            FaceButton.ToolTip = _note.Length > 0 ? tip + Environment.NewLine + Environment.NewLine + _note : tip;
        }

        // ---- popup ---------------------------------------------------------

        private void OnSelectAll(object sender, RoutedEventArgs e)
        {
            Note = string.Empty;
            SelectAll();
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            Note = string.Empty;
            ClearSelection();
        }

        // With StaysOpen false, a click on the face while open would close the popup on mouse
        // down and reopen it on the click. Taking the face out of hit testing while the popup
        // is open lets that click simply close it.
        private void OnPopupOpened(object? sender, EventArgs e)
        {
            FaceButton.IsHitTestVisible = false;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                PopupPanel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))));
        }

        private void OnPopupClosed(object? sender, EventArgs e) => FaceButton.IsHitTestVisible = true;

        private void OnPopupKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            // Through the face rather than the popup, so the two-way binding stays in charge.
            FaceButton.IsChecked = false;
            FaceButton.Focus();
            e.Handled = true;
        }
    }
}
