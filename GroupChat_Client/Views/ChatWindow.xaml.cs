using GroupChat_Client.ViewModels;
using System.Net.Sockets;
using System.Windows;

namespace GroupChat_Client.Views
{
    public partial class ChatWindow : Window
    {
        public ChatWindow(TcpClient client, string username, string serverIp)
        {
            InitializeComponent();

            DataContext = new ChatViewModel(client, username, serverIp);
        }
    }
}