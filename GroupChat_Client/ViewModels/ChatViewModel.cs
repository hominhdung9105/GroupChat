using GroupChat_Client.Commands;
using GroupChat_Client.Models;
using GroupChat_Client.Views;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq; // Sử dụng cho cấu trúc IGrouping khi phân loại Emoji
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace GroupChat_Client.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly Dictionary<string, IncomingFileTransfer> _incomingFiles = new();
        private readonly HashSet<string> _outgoingFileIds = new();
        private const int ChunkSize = 64 * 1024;

        // SỬA LỖI 2: Đã nâng trực tiếp hằng số lên 3 GB ngay tại đây để không bị lỗi gán giá trị ở Constructor
        private const long MaxFileSizeBytes = 3000L * 1024 * 1024;
        private readonly int _chatPort;
        private readonly int _filePort;

        private string _messageText = string.Empty;
        private bool _isDisconnecting;
        private bool _isEmojiPickerOpen;
        private int _onlineCount;

        public string Username { get; }

        public string ServerIp { get; }

        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public ObservableCollection<string> OnlineUsers { get; } = new();

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
        public ICommand SendFileCommand { get; }
        public ICommand SendImageCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand OpenImageCommand { get; }

        public ChatViewModel(TcpClient client, string username, string serverIp)
        {
            _client = client;
            _stream = client.GetStream();

            // Đọc Port hiện tại từ kết nối chính và tính toán Port cho luồng File
            var remoteEndPoint = (System.Net.IPEndPoint)client.Client.RemoteEndPoint!;
            _chatPort = remoteEndPoint.Port;
            _filePort = _chatPort + 1; // Luôn bằng Port chính + 1

            _writer = new StreamWriter(_stream, Encoding.UTF8, 128 * 1024) { AutoFlush = true };
            Username = username;
            ServerIp = serverIp;

            // SỬA LỖI 1: Đưa việc khởi tạo các lệnh Command quay trở lại Constructor gốc của bạn
            SendCommand = new RelayCommand(SendMessage);
            BackCommand = new RelayCommand(BackToMain);
            EmojiCommand = new RelayCommand(() => IsEmojiPickerOpen = !IsEmojiPickerOpen);
            InsertEmojiCommand = new RelayCommand<string>(InsertEmoji);
            SendFileCommand = new RelayCommand(SendFile);
            SendImageCommand = new RelayCommand(SendImage);
            SaveFileCommand = new RelayCommand<ChatMessage>(SaveFileAs);
            OpenImageCommand = new RelayCommand<string>(OpenImage);

            // Gửi lệnh kết nối cùng username lên server ngay khi vào phòng
            _writer.WriteLine($"CONNECT|{Username}");

            _ = Task.Run(() => ReceiveMessagesAsync());
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
                return;

            string content = MessageText.Trim();

            try
            {
                await SendLineAsync($"TEXT|{Username}|{content}");

                Messages.Add(new ChatMessage
                {
                    Sender = Username,
                    Content = content,
                    SentAt = DateTime.Now,
                    IsOwnMessage = true,
                    MessageKind = ChatMessageKind.Text
                });

                MessageText = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Send failed: {ex.Message}");
            }
        }

        private void OpenImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return;

            try
            {
                var viewer = new ImageViewerWindow(imagePath)
                {
                    Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
                };
                viewer.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Open image failed: {ex.Message}");
            }
        }

        private async Task ReceiveMessagesAsync()
        {
            try
            {
                using var reader = new System.IO.StreamReader(_stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 128 * 1024, leaveOpen: true);

                while (true)
                {
                    string? message = await reader.ReadLineAsync();

                    if (message == null)
                        break;

                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    if (message.StartsWith("ERROR|", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = message.Split('|', 2);
                        if (parts.Length == 2)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var result = MessageBox.Show(parts[1], "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                                if (result.Equals(MessageBoxResult.OK))
                                    BackToMain();
                            });
                        }
                        continue;
                    }

                    if (message.StartsWith("USERS_COUNT|", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = message.Split('|', 2);
                        if (parts.Length == 2 && int.TryParse(parts[1], out int count))
                        {
                            Application.Current.Dispatcher.Invoke(() => OnlineCount = count);
                        }
                        continue;
                    }

                    if (message.StartsWith("USERS_LIST|", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleUsersList(message);
                        continue;
                    }

                    if (message.StartsWith("TEXT|", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleTextMessage(message);
                        continue;
                    }

                    if (message.StartsWith("FILE_START|", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleFileStartAsync(message);
                        continue;
                    }

                    if (message.StartsWith("FILE_CHUNK|", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleFileChunkAsync(message);
                        continue;
                    }

                    if (message.StartsWith("FILE_END|", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleFileEnd(message);
                        continue;
                    }

                    HandleLegacyMessage(message);
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
                            IsOwnMessage = false,
                            MessageKind = ChatMessageKind.System
                        });
                    });
                }
            }
        }

        private void HandleUsersList(string message)
        {
            string[] parts = message.Split('|', 2);
            if (parts.Length < 2)
                return;

            string[] encodedNames = parts[1]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var names = encodedNames
                .Select(name => DecodeBase64(name))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Application.Current.Dispatcher.Invoke(() =>
            {
                OnlineUsers.Clear();
                foreach (string name in names)
                {
                    OnlineUsers.Add(name);
                }
            });
        }

        public async Task HandleFileDropAsync(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path))
                    continue;

                bool isImage = IsImageFile(path);
                await SendFileAsync(path, isImage);
            }
        }

        private void InsertEmoji(string? emoji)
        {
            if (!string.IsNullOrEmpty(emoji))
            {
                MessageText += emoji;
            }
        }

        private void BackToMain()
        {
            _isDisconnecting = true;
            try
            {
                _writer.Dispose();
                _stream.Close();
                _client.Close();
            }
            catch { }

            Application.Current.Dispatcher.Invoke(() =>
            {
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

        private async Task SendLineAsync(string message)
        {
            await _sendLock.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(message);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async void SendFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "All files (*.*)|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                await SendFileAsync(dialog.FileName, isImage: false);
            }
        }

        private async void SendImage()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                await SendFileAsync(dialog.FileName, isImage: true);
            }
        }

        private async Task SendFileAsync(string filePath, bool isImage)
        {
            if (!File.Exists(filePath))
                return;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                MessageBox.Show("Dung lượng file vượt quá giới hạn 3 GB cho phép.");
                return;
            }

            string fileId = Guid.NewGuid().ToString("N");
            string fileName = fileInfo.Name;
            string fileNameEncoded = EncodeBase64(fileName);

            var message = new ChatMessage
            {
                Sender = Username,
                FileName = fileName,
                FileSize = fileInfo.Length,
                SentAt = DateTime.Now,
                IsOwnMessage = true,
                MessageKind = isImage ? ChatMessageKind.Image : ChatMessageKind.File,
                ImagePath = isImage ? filePath : null,
                ProgressPercent = 0
            };

            Messages.Add(message);
            _outgoingFileIds.Add(fileId);

            _ = Task.Run(async () =>
            {
                TcpClient? fileTransferClient = null;
                try
                {
                    await SendLineAsync($"FILE_START|{Username}|{fileId}|{fileNameEncoded}|{fileInfo.Length}|{(isImage ? 1 : 0)}");

                    fileTransferClient = new TcpClient();
                    await fileTransferClient.ConnectAsync(ServerIp, _filePort);

                    using var fileStreamNetwork = fileTransferClient.GetStream();
                    using var fileWriter = new StreamWriter(fileStreamNetwork, Encoding.UTF8, 256 * 1024) { AutoFlush = true };

                    await fileWriter.WriteLineAsync($"REG_FILE_ID|{fileId}");

                    long sentBytes = 0;
                    byte[] buffer = new byte[ChunkSize];
                    using var fileDiskStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, useAsync: true);

                    int bytesRead;
                    while ((bytesRead = await fileDiskStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        string chunkBase64 = Convert.ToBase64String(buffer, 0, bytesRead);

                        await fileWriter.WriteLineAsync($"FILE_CHUNK|{fileId}|{chunkBase64}");

                        sentBytes += bytesRead;

                        double progress = Math.Min(100, sentBytes * 100.0 / fileInfo.Length);
                        Application.Current.Dispatcher.Invoke(() => message.ProgressPercent = progress);
                    }

                    await SendLineAsync($"FILE_END|{fileId}");
                    Application.Current.Dispatcher.Invoke(() => message.ProgressPercent = 100);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show($"Gửi file '{fileName}' thất bại: {ex.Message}")
                    );
                }
                finally
                {
                    fileTransferClient?.Close();
                    _outgoingFileIds.Remove(fileId);
                }
            });
        }

        private void SaveFileAs(ChatMessage? message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.TempFilePath) || !File.Exists(message.TempFilePath))
                return;

            var dialog = new SaveFileDialog
            {
                FileName = message.FileName ?? "file",
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                File.Copy(message.TempFilePath, dialog.FileName, overwrite: true);
            }
        }

        private void HandleTextMessage(string message)
        {
            string[] parts = message.Split('|', 3);
            if (parts.Length < 3)
                return;

            string sender = parts[1];
            string content = parts[2];

            if (sender == Username)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Sender = sender,
                    Content = content,
                    SentAt = DateTime.Now,
                    IsOwnMessage = false,
                    MessageKind = sender == "System" ? ChatMessageKind.System : ChatMessageKind.Text
                });
            });
        }

        private async Task HandleFileStartAsync(string message)
        {
            string[] parts = message.Split('|', 6);
            if (parts.Length < 6)
                return;

            string sender = parts[1];
            string fileId = parts[2];

            if (sender == Username)
                return;

            string fileName = DecodeBase64(parts[3]);
            if (!long.TryParse(parts[4], out long fileSize))
                return;

            bool isImage = parts[5] == "1";
            string tempFolder = Path.Combine(Path.GetTempPath(), "GroupChat");
            Directory.CreateDirectory(tempFolder);
            string tempPath = Path.Combine(tempFolder, $"{fileId}{Path.GetExtension(fileName)}");

            var chatMessage = new ChatMessage
            {
                Sender = sender,
                FileName = fileName,
                FileSize = fileSize,
                SentAt = DateTime.Now,
                IsOwnMessage = false,
                MessageKind = isImage ? ChatMessageKind.Image : ChatMessageKind.File,
                ProgressPercent = 0,
                TempFilePath = tempPath
            };

            _incomingFiles[fileId] = new IncomingFileTransfer(chatMessage, tempPath, fileSize, isImage);

            Application.Current.Dispatcher.Invoke(() => Messages.Add(chatMessage));

            await Task.CompletedTask;
        }

        private async Task HandleFileChunkAsync(string message)
        {
            string[] parts = message.Split('|', 3);
            if (parts.Length < 3)
                return;

            string fileId = parts[1];
            if (!_incomingFiles.TryGetValue(fileId, out IncomingFileTransfer? transfer))
                return;

            byte[] bytes = Convert.FromBase64String(parts[2]);
            await transfer.Stream.WriteAsync(bytes, 0, bytes.Length);
            transfer.ReceivedBytes += bytes.Length;

            double progress = Math.Min(100, transfer.ReceivedBytes * 100.0 / transfer.TotalBytes);
            Application.Current.Dispatcher.Invoke(() => transfer.Message.ProgressPercent = progress);
        }

        private void HandleFileEnd(string message)
        {
            string[] parts = message.Split('|', 2);
            if (parts.Length < 2)
                return;

            string fileId = parts[1];
            if (!_incomingFiles.TryGetValue(fileId, out IncomingFileTransfer? transfer))
                return;

            transfer.Stream.Dispose();
            _incomingFiles.Remove(fileId);

            Application.Current.Dispatcher.Invoke(() =>
            {
                transfer.Message.ProgressPercent = 100;
                if (transfer.IsImage)
                {
                    transfer.Message.ImagePath = transfer.TempFilePath;
                }
            });
        }

        private void HandleLegacyMessage(string message)
        {
            string[] parts = message.Split('|', 2);
            string sender = parts.Length > 1 ? parts[0] : "Server";
            string content = parts.Length > 1 ? parts[1] : message;

            if (sender == Username)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Sender = sender,
                    Content = content,
                    SentAt = DateTime.Now,
                    IsOwnMessage = false,
                    MessageKind = sender == "System" ? ChatMessageKind.System : ChatMessageKind.Text
                });
            });
        }

        private static bool IsImageFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";
        }

        private static string EncodeBase64(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static string DecodeBase64(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private sealed class IncomingFileTransfer
        {
            public IncomingFileTransfer(ChatMessage message, string tempFilePath, long totalBytes, bool isImage)
            {
                Message = message;
                TempFilePath = tempFilePath;
                TotalBytes = totalBytes;
                IsImage = isImage;
                Stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            }

            public ChatMessage Message { get; }

            public FileStream Stream { get; }

            public long TotalBytes { get; }

            public long ReceivedBytes { get; set; }

            public bool IsImage { get; }

            public string TempFilePath { get; }
        }
    }
}