using GroupChat_Client.Commands;
using GroupChat_Client.Models;
using GroupChat_Client.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq; // Sử dụng cho cấu trúc IGrouping khi phân loại Emoji
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
        private bool _isDisconnecting;
        private bool _isEmojiPickerOpen;
        private int _onlineCount;

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

        public bool IsEmojiPickerOpen
        {
            get => _isEmojiPickerOpen;
            set
            {
                _isEmojiPickerOpen = value;
                OnPropertyChanged();
            }
        }
        public int OnlineCount
        {
            get => _onlineCount;
            set
            {
                _onlineCount = value;
                OnPropertyChanged();
            }
        }

        // Lấy danh sách Emoji đã được nhóm theo Category từ file JSON
        public List<IGrouping<string, EmojiModel>> GroupedEmojis { get; } = EmojiProvider.GetGroupedEmojis();

        public ICommand SendCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand EmojiCommand { get; }
        public ICommand InsertEmojiCommand { get; }

        public ChatViewModel(TcpClient client, string username, string serverIp)
        {
            _client = client;
            _stream = client.GetStream();

            Username = username;
            ServerIp = serverIp;

            SendCommand = new RelayCommand(SendMessage);
            BackCommand = new RelayCommand(BackToMain);
            EmojiCommand = new RelayCommand(() => IsEmojiPickerOpen = !IsEmojiPickerOpen);
            InsertEmojiCommand = new RelayCommand<string>(InsertEmoji);

            // Gửi username lên server ngay khi vào ChatWindow
            byte[] usernameData = Encoding.UTF8.GetBytes(Username);
            _stream.Write(usernameData, 0, usernameData.Length);

            _ = ReceiveMessagesAsync();
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
                return;

            // Loại bỏ các khoảng trắng thừa ở hai đầu tin nhắn trước khi gửi đi
            string content = MessageText.Trim();

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
                            if (parts[0] == "USERS_COUNT")
                            {
                                if (int.TryParse(parts[1], out int count))
                                {
                                    OnlineCount = count;
                                }
                                return; // Kết thúc sớm, không add tin nhắn này vào UI
                            }

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
                if (!_isDisconnecting)
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
        }

        private void InsertEmoji(string? emoji)
        {
            if (!string.IsNullOrEmpty(emoji))
            {
                // Cộng chuỗi emoji trực tiếp vào ô chat hiện tại mà không làm đóng Popup
                MessageText += emoji;
            }
        }

        private void BackToMain()
        {
            _isDisconnecting = true;

            try
            {
                _stream.Close();
                _client.Close();
            }
            catch
            {
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();

                // Đặt cửa sổ chính mới
                Application.Current.MainWindow = mainWindow;

                // Quét và đóng triệt để tất cả ChatWindow đang mở
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is ChatWindow)
                    {
                        window.Close();
                    }
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}