using GroupChat_Client.ViewModels;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Input;

namespace GroupChat_Client.Views
{
    public partial class ChatWindow : Window
    {
        public ChatWindow(TcpClient client, string username, string serverIp)
        {
            InitializeComponent();

            DataContext = new ChatViewModel(client, username, serverIp);
        }
        private void EmojiPicker_Picked(object sender, Emoji.Wpf.EmojiPickedEventArgs e)
        {
            if (DataContext is ChatViewModel viewModel)
            {
                viewModel.MessageText += e.Emoji;
            }
        }

        private void ChatWindow_OnPreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private async void ChatWindow_OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            if (DataContext is not ChatViewModel viewModel)
                return;

            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            {
                await viewModel.HandleFileDropAsync(paths);
            }
        }
    }
}