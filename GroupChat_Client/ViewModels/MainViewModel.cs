using GroupChat_Client.Commands;
using GroupChat_Client.Views;
using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace GroupChat_Client.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _serverIp = "127.0.0.1";
        private string _port = "5000";

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        public string ServerIp
        {
            get => _serverIp;
            set
            {
                _serverIp = value;
                OnPropertyChanged();
            }
        }

        public string Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged();
            }
        }

        public ICommand ConnectCommand { get; }

        public MainViewModel()
        {
            ConnectCommand = new RelayCommand(Connect);
        }

        private async void Connect()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Please enter username");
                return;
            }

            if (!int.TryParse(Port, out int portNumber))
            {
                MessageBox.Show("Invalid port");
                return;
            }

            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync(ServerIp, portNumber);

                ChatWindow chatWindow = new ChatWindow(client, Username, ServerIp);
                chatWindow.Show();

                Application.Current.MainWindow?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connect failed: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}