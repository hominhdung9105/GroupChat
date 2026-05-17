using GroupChat_Client.Commands;
using GroupChat_Client.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace GroupChat_Client.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;

        private string _messageText = string.Empty;

        public string Username { get; }

        public string ServerIp { get; }

        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public string MessageText
        {
            get => _messageText;
            set
            {
                _messageText = value;
                OnPropertyChanged();
            }
        }

        public ICommand SendCommand { get; }

        public ChatViewModel(TcpClient client, string username, string serverIp)
        {
            _client = client;
            _stream = client.GetStream();

            Username = username;
            ServerIp = serverIp;

            SendCommand = new RelayCommand(SendMessage);

            // Gửi username lên server ngay khi vào ChatWindow
            byte[] usernameData = Encoding.UTF8.GetBytes(Username);
            _stream.Write(usernameData, 0, usernameData.Length);

            // Không tự add "connected" ở đây nữa
            // Server sẽ gửi System|Username connected về cho tất cả client

            _ = ReceiveMessagesAsync();
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
                return;

            string content = MessageText;

            byte[] data = Encoding.UTF8.GetBytes(content);

            try
            {
                await _stream.WriteAsync(data, 0, data.Length);

                Messages.Add(new ChatMessage
                {
                    Sender = Username,
                    Content = content,
                    SentAt = DateTime.Now,
                    IsOwnMessage = true
                });

                MessageText = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Send failed: {ex.Message}");
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            byte[] buffer = new byte[4096];

            try
            {
                while (true)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                        break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        string sender = "Server";
                        string content = message;

                        string[] parts = message.Split('|', 2);

                        if (parts.Length == 2)
                        {
                            sender = parts[0];
                            content = parts[1];
                        }

                        // Tránh hiện lại tin nhắn của chính mình nếu server broadcast về lại
                        if (sender == Username)
                            return;

                        Messages.Add(new ChatMessage
                        {
                            Sender = sender,
                            Content = content,
                            SentAt = DateTime.Now,
                            IsOwnMessage = false
                        });
                    });
                }
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Messages.Add(new ChatMessage
                    {
                        Sender = "System",
                        Content = "Disconnected from server",
                        SentAt = DateTime.Now,
                        IsOwnMessage = false
                    });
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}