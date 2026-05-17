using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GroupChat_Client.Behaviors
{
    public static class TextBoxEnterBehavior
    {
        // Khai báo một Attached Property tên là "EnterCommand"
        public static readonly DependencyProperty EnterCommandProperty =
            DependencyProperty.RegisterAttached(
                "EnterCommand",
                typeof(ICommand),
                typeof(TextBoxEnterBehavior),
                new PropertyMetadata(null, OnEnterCommandChanged));

        public static void SetEnterCommand(DependencyObject dp, ICommand value)
        {
            dp.SetValue(EnterCommandProperty, value);
        }

        public static ICommand GetEnterCommand(DependencyObject dp)
        {
            return (ICommand)dp.GetValue(EnterCommandProperty);
        }

        // Hàm này tự động chạy khi XAML gắn thuộc tính EnterCommand vào TextBox
        private static void OnEnterCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                if (e.NewValue != null)
                {
                    textBox.PreviewKeyDown += TextBox_PreviewKeyDown;
                }
                else
                {
                    textBox.PreviewKeyDown -= TextBox_PreviewKeyDown;
                }
            }
        }

        // Logic xử lý phím được chuyển hoàn toàn vào đây
        private static void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    return; // Nhấn Shift + Enter -> Cho phép xuống dòng
                }

                e.Handled = true; // Chỉ nhấn Enter -> Chặn xuống dòng

                var textBox = (TextBox)sender;
                var command = GetEnterCommand(textBox); // Lấy Command từ XAML

                // Thực thi lệnh SendCommand
                if (command != null && command.CanExecute(null))
                {
                    command.Execute(null);
                }
            }
        }
    }
}