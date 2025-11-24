using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace MarketScanner.UI.Wpf.Behaviors
{
    public static class MultiSelectBehavior
    {
        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(MultiSelectBehavior),
                new PropertyMetadata(null, OnSelectedItemsChanged));

        public static void SetSelectedItems(DependencyObject obj, IList value)
            => obj.SetValue(SelectedItemsProperty, value);

        public static IList GetSelectedItems(DependencyObject obj)
            => (IList)obj.GetValue(SelectedItemsProperty);

        public static void OnSelectedItemsChanged(
            DependencyObject d , DependencyPropertyChangedEventArgs e)
        {
            if(d is ListBox lb)
            {
                lb.SelectionChanged -= ListBox_SelectionChanged;
                lb.SelectionChanged += ListBox_SelectionChanged;
            }
        }

        private static void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(sender is ListBox lb)
            {
                IList boundList = GetSelectedItems(lb);
                if (boundList == null) return;

                foreach (var item in e.AddedItems)
                {
                    if(!boundList.Contains(item)) 
                        boundList.Add(item);
                }

                foreach (var item in e.RemovedItems)
                {
                    if (boundList.Contains(item))
                        boundList.Remove(item);
                }
            }
        }
    }
}
