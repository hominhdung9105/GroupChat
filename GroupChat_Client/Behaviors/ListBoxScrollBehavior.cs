using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace GroupChat_Client.Behaviors
{
    public static class ListBoxScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.RegisterAttached(
                "AutoScroll",
                typeof(bool),
                typeof(ListBoxScrollBehavior),
                new PropertyMetadata(false, OnAutoScrollChanged));

        public static void SetAutoScroll(DependencyObject dp, bool value)
        {
            dp.SetValue(AutoScrollProperty, value);
        }

        public static bool GetAutoScroll(DependencyObject dp)
        {
            return (bool)dp.GetValue(AutoScrollProperty);
        }

        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ListBox listBox)
            {
                var newValue = (bool)e.NewValue;
                if (newValue)
                {
                    // Lắng nghe khi ItemsSource thay đổi (nếu nó là ObservableCollection)
                    listBox.Loaded += (s, args) =>
                    {
                        if (listBox.ItemsSource is INotifyCollectionChanged collection)
                        {
                            collection.CollectionChanged += (sender, args2) =>
                            {
                                if (args2.Action == NotifyCollectionChangedAction.Add)
                                {
                                    Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        if (listBox.Items.Count > 0)
                                        {
                                            listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
                                        }
                                    });
                                }
                            };
                        }
                    };
                }
            }
        }
    }
}