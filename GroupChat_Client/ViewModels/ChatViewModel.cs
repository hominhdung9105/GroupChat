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
using System.IO;

namespace GroupChat_Client.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;

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
            _reader = new StreamReader(_stream, Encoding.UTF8);
            _writer = new StreamWriter(_stream, Encoding.UTF8)
            {
                AutoFlush = true
            };

            Username = username;
            ServerIp = serverIp;

            SendCommand = new RelayCommand(SendMessage);
            BackCommand = new RelayCommand(BackToMain);
            EmojiCommand = new RelayCommand(() => IsEmojiPickerOpen = !IsEmojiPickerOpen);
            InsertEmojiCommand = new RelayCommand<string>(InsertEmoji);

            // Gửi username lên server ngay khi vào ChatWindow
            _writer.WriteLine(Username);

            _ = ReceiveMessagesAsync();
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
                return;

            string content = MessageText.Trim();

            try
            {
                await _writer.WriteLineAsync(content);

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
                    string? message = await _reader.ReadLineAsync();

                    if (message == null)
                        break;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        string sender = "Server";
                        string content = message;

                        string[] parts = message.Split('|', 2);

                        if (parts.Length == 2)
                        {
                            // THÊM MỚI: Bắt tín hiệu lỗi từ Server (Trùng tên)
                            if (parts[0] == "ERROR")
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    // Hiện popup cảnh báo
                                    MessageBox.Show(parts[1], "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);

                                    // Đẩy người dùng về lại MainWindow
                                    BackToMain();
                                });
                                return; // Thoát không xử lý tiếp
                            }

                            // Đã có từ bước trước (Cập nhật số người online)
                            if (parts[0] == "USERS_COUNT")
                            {
                                if (int.TryParse(parts[1], out int count))
                                {
                                    OnlineCount = count;
                                }
                                return;
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