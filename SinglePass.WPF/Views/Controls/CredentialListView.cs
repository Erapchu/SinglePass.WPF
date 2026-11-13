using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SinglePass.WPF.Views.Controls
{
    // A flat, uniform-height list gives VirtualizingStackPanel an exact extent from
    // its first measure. Nested virtualized groups otherwise estimate unseen groups.
    public class CredentialListView : ListView
    {
        public static readonly DependencyProperty GroupedItemsSourceProperty = DependencyProperty.Register(
            nameof(GroupedItemsSource), typeof(ICollectionView), typeof(CredentialListView),
            new PropertyMetadata(null, OnGroupedItemsSourceChanged));

        public ICollectionView GroupedItemsSource
        {
            get => (ICollectionView)GetValue(GroupedItemsSourceProperty);
            set => SetValue(GroupedItemsSourceProperty, value);
        }

        private readonly List<CollectionViewGroup> _rowGroups = new();

        public CollectionViewGroup GroupAt(int index) =>
            index >= 0 && index < _rowGroups.Count ? _rowGroups[index] : null;

        private static void OnGroupedItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var list = (CredentialListView)d;
            if (e.OldValue is INotifyCollectionChanged previous)
                CollectionChangedEventManager.RemoveHandler(previous, list.OnCollectionChanged);
            if (e.NewValue is INotifyCollectionChanged current)
                CollectionChangedEventManager.AddHandler(current, list.OnCollectionChanged);
            list.RebuildRows();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => RebuildRows();

        private void RebuildRows()
        {
            var selected = SelectedItem;
            var rows = new List<object>();
            _rowGroups.Clear();
            if (GroupedItemsSource?.Groups is { } groups)
            {
                foreach (CollectionViewGroup group in groups)
                {
                    // The first heading already occupies the permanent row above the list.
                    if (rows.Count > 0)
                    {
                        rows.Add(group);
                        _rowGroups.Add(group);
                    }
                    foreach (var item in group.Items)
                    {
                        rows.Add(item);
                        _rowGroups.Add(group);
                    }
                }
            }
            ItemsSource = rows;
            if (selected != null && rows.Contains(selected))
                SetCurrentValue(SelectedItemProperty, selected);
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            var container = (ListViewItem)element;
            var isHeader = item is CollectionViewGroup;
            container.IsEnabled = !isHeader;
            container.Focusable = !isHeader;
            container.IsHitTestVisible = !isHeader;
            if (isHeader)
            {
                container.ContentTemplate = (DataTemplate)FindResource("CredentialGroupHeaderTemplate");
                container.Padding = new Thickness(0);
            }
            else
            {
                container.ContentTemplate = ItemTemplate;
                container.ClearValue(Control.PaddingProperty);
            }
        }
    }
}
