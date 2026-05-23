using GroupChat_Client.Commands;
using GroupChat_Client.Models;
using GroupChat_Client.Views;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private const int ChunkSize = 64 * 1024;

        private const long MaxFileSizeBytes = 3000L * 1024 * 1024; // 3 GB
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
            set { _messageText = value; OnPropertyChanged(); }
        }

        public bool IsEmojiPickerOpen
        {
            get => _isEmojiPickerOpen;
            set { _isEmojiPickerOpen = value; OnPropertyChanged(); }
        }

        public int OnlineCount
        {
            get => _onlineCount;
            set { _onlineCount = value; OnPropertyChanged(); }
        }

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

            var remoteEndPoint = (System.Net.IPEndPoint)client.Client.RemoteEndPoint!;
            _chatPort = remoteEndPoint.Port;
            _filePort = _chatPort + 1;

            _writer = new StreamWriter(_stream, Encoding.UTF8, 128 * 1024) { AutoFlush = true };
            Username = username;
            ServerIp = serverIp;

            SendCommand = new RelayCommand(SendMessage);
            BackCommand = new RelayCommand(BackToMain);
            EmojiCommand = new RelayCommand(() => IsEmojiPickerOpen = !IsEmojiPickerOpen);
            InsertEmojiCommand = new RelayCommand<string>(InsertEmoji);
            SendFileCommand = new RelayCommand(SendFile);
            SendImageCommand = new RelayCommand(SendImage);
            SaveFileCommand = new RelayCommand<ChatMessage>(SaveFileAs);
            OpenImageCommand = new RelayCommand<string>(OpenImage);

            _writer.WriteLine($"CONNECT|{Username}");

            _ = Task.Run(() => ReceiveMessagesAsync());
        }

        private async void SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText)) return;
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

        private async Task ReceiveMessagesAsync()
        {
            try
            {
                using var reader = new StreamReader(_stream, Encoding.UTF8, false, 128 * 1024, true);

                while (true)
                {
                    string? message = await reader.ReadLineAsync();
                    if (message == null) break;
                    if (string.IsNullOrWhiteSpace(message)) continue;

                    if (message.StartsWith("ERROR|", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] parts = message.Split('|', 2);
                        if (parts.Length == 2)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show(parts[1], "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                    // KHI CÓ THÔNG BÁO FILE TỪ SERVER: Chỉ khởi tạo metadata UI, không tự động nhận chunk data
                    if (message.StartsWith("FILE_START|", StringComparison.OrdinalIgnoreCase))
                    {
                        HandleFileStartMetadata(message);
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

        private void HandleFileStartMetadata(string message)
        {
            string[] parts = message.Split('|', 6);
            if (parts.Length < 6) return;

            string sender = parts[1];
            string fileId = parts[2];

            // Nếu chính mình gửi, UI đã hiển thị từ trước và đã có file gốc, không cần hiển thị hộp thoại nhận
            if (sender == Username) return;

            string fileName = DecodeBase64(parts[3]);
            if (!long.TryParse(parts[4], out long fileSize)) return;
            bool isImage = parts[5] == "1";

            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new ChatMessage
                {
                    FileId = fileId,
                    Sender = sender,
                    FileName = fileName,
                    FileSize = fileSize,
                    SentAt = DateTime.Now,
                    IsOwnMessage = false,
                    MessageKind = isImage ? ChatMessageKind.Image : ChatMessageKind.File,
                    ProgressPercent = 0,
                    IsDownloaded = false // Đánh dấu chưa được tải về máy
                });
            });
        }

        private async Task SendFileAsync(string filePath, bool isImage)
        {
            if (!File.Exists(filePath)) return;

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
                FileId = fileId,
                Sender = Username,
                FileName = fileName,
                FileSize = fileInfo.Length,
                SentAt = DateTime.Now,
                IsOwnMessage = true,
                MessageKind = isImage ? ChatMessageKind.Image : ChatMessageKind.File,
                ImagePath = isImage ? filePath : null, // Gán trực tiếp ảnh local để Uploader xem luôn không cần tải lại
                ProgressPercent = 0,
                IsDownloaded = true // Người up mặc định đã có file hoàn chỉnh
            };

            Messages.Add(message);

            _ = Task.Run(async () =>
            {
                TcpClient? fileTransferClient = null;
                try
                {
                    // 1. Phát tín hiệu lệnh Control thông báo cho toàn room chat đồng bộ UI
                    await SendLineAsync($"FILE_START|{Username}|{fileId}|{fileNameEncoded}|{fileInfo.Length}|{(isImage ? 1 : 0)}");

                    // 2. Mở cổng kết nối Data Channel riêng gửi file thẳng lên Server Storage
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

                    await fileWriter.WriteLineAsync("FILE_UPLOAD_END");
                    Application.Current.Dispatcher.Invoke(() => message.ProgressPercent = 100);
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Gửi file '{fileName}' lên Server thất bại: {ex.Message}"));
                }
                finally
                {
                    fileTransferClient?.Close();
                }
            });
        }

        // KHI USER NHẤN NÚT "TẢI XUỐNG" (Chỉ chạy khi có nhu cầu)
        private void SaveFileAs(ChatMessage? message)
        {
            if (message == null || string.IsNullOrEmpty(message.FileId)) return;

            // Nếu người dùng đã tải về trước đó rồi thì chỉ việc copy từ file tạm ra đường dẫn mới lưu
            if (message.IsDownloaded && !string.IsNullOrEmpty(message.TempFilePath) && File.Exists(message.TempFilePath))
            {
                ExecuteSaveFileDialog(message.TempFilePath, message.FileName);
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = message.FileName ?? "file",
                Filter = "All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                string savePath = dialog.FileName;
                message.ProgressPercent = 0;

                _ = Task.Run(async () =>
                {
                    TcpClient? downloadClient = null;
                    try
                    {
                        downloadClient = new TcpClient();
                        await downloadClient.ConnectAsync(ServerIp, _filePort);

                        using var netStream = downloadClient.GetStream();
                        using var writer = new StreamWriter(netStream, Encoding.UTF8, 256 * 1024) { AutoFlush = true };
                        using var reader = new StreamReader(netStream, Encoding.UTF8, false, 256 * 1024, true);

                        // Gửi yêu cầu kéo file theo ID cụ thể lên Server
                        await writer.WriteLineAsync($"DOWNLOAD_REQUEST|{message.FileId}");

                        string? response = await reader.ReadLineAsync();
                        if (response == null || response.StartsWith("DOWNLOAD_ERROR"))
                        {
                            string errMsg = response?.Split('|').ElementAtOrDefault(1) ?? "Lỗi không xác định từ Server.";
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show(errMsg, "Lỗi tải file"));
                            return;
                        }

                        // Nhận phản hồi chấp thuận từ Server
                        if (response.StartsWith("DOWNLOAD_ACCEPTED"))
                        {
                            // Tạo thư mục tạm để lưu giữ cache (dành cho mục đích hiển thị hình ảnh cục bộ sau này nếu là ảnh)
                            string tempFolder = Path.Combine(Path.GetTempPath(), "GroupChat");
                            Directory.CreateDirectory(tempFolder);
                            string tempPath = Path.Combine(tempFolder, $"{message.FileId}{Path.GetExtension(message.FileName)}");

                            // Lưu đồng thời ra cả file đích thực tế và file tạm cache
                            using var fsTarget = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
                            using var fsTemp = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);

                            long receivedBytes = 0;
                            string? line;

                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                if (line == "DOWNLOAD_END") break;

                                if (line.StartsWith("FILE_CHUNK|"))
                                {
                                    string[] chunkParts = line.Split('|', 3);
                                    if (chunkParts.Length == 3)
                                    {
                                        byte[] bytes = Convert.FromBase64String(chunkParts[2]);

                                        await fsTarget.WriteAsync(bytes, 0, bytes.Length);
                                        await fsTemp.WriteAsync(bytes, 0, bytes.Length);

                                        receivedBytes += bytes.Length;
                                        double progress = Math.Min(100, receivedBytes * 100.0 / message.FileSize);
                                        Application.Current.Dispatcher.Invoke(() => message.ProgressPercent = progress);
                                    }
                                }
                            }

                            // Xử lý hoàn thành tiến trình
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                message.ProgressPercent = 100;
                                message.TempFilePath = tempPath;
                                message.IsDownloaded = true;

                                // Nếu là ảnh thì gán ImagePath để kích hoạt hiển thị Preview lên giao diện chat
                                if (IsImageFile(message.FileName ?? ""))
                                {
                                    message.ImagePath = tempPath;
                                }
                                MessageBox.Show($"Tải file '{message.FileName}' về máy thành công!", "Thông báo");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Tải file thất bại: {ex.Message}"));
                    }
                    finally
                    {
                        downloadClient?.Close();
                    }
                });
            }
        }

        private void ExecuteSaveFileDialog(string tempFilePath, string? defaultName)
        {
            var dialog = new SaveFileDialog
            {
                FileName = defaultName ?? "file",
                Filter = "All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                File.Copy(tempFilePath, dialog.FileName, overwrite: true);
            }
        }

        // Các hàm phụ trợ giữ nguyên...
        private void HandleUsersList(string message)
        {
            string[] parts = message.Split('|', 2);
            if (parts.Length < 2) return;
            string[] encodedNames = parts[1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var names = encodedNames.Select(name => DecodeBase64(name)).Where(name => !string.IsNullOrWhiteSpace(name)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
            Application.Current.Dispatcher.Invoke(() => {
                OnlineUsers.Clear();
                foreach (string name in names) OnlineUsers.Add(name);
            });
        }

        private void HandleTextMessage(string message)
        {
            string[] parts = message.Split('|', 3);
            if (parts.Length < 3) return;
            string sender = parts[1];
            string content = parts[2];
            if (sender == Username) return;

            Application.Current.Dispatcher.Invoke(() => {
                bool isSystem = string.Equals(sender, "System", StringComparison.OrdinalIgnoreCase);
                Messages.Add(new ChatMessage
                {
                    Sender = isSystem ? "System" : sender,
                    Content = content,
                    SentAt = DateTime.Now,
                    IsOwnMessage = false,
                    MessageKind = isSystem ? ChatMessageKind.System : ChatMessageKind.Text
                });
            });
        }

        private void HandleLegacyMessage(string message)
        {
            string[] parts = message.Split('|', 2);
            string sender = parts.Length > 1 ? parts[0] : "System";
            string content = parts.Length > 1 ? parts[1] : message;
            if (sender == Username) return;

            Application.Current.Dispatcher.Invoke(() => {
                bool isSystem = string.Equals(sender, "System", StringComparison.OrdinalIgnoreCase);
                Messages.Add(new ChatMessage
                {
                    Sender = isSystem ? "System" : sender,
                    Content = content,
                    SentAt = DateTime.Now,
                    IsOwnMessage = false,
                    MessageKind = isSystem ? ChatMessageKind.System : ChatMessageKind.Text
                });
            });
        }

        public async Task HandleFileDropAsync(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path)) continue;
                await SendFileAsync(path, IsImageFile(path));
            }
        }

        private void OpenImage(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return;
            try
            {
                var viewer = new ImageViewerWindow(imagePath) { Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) };
                viewer.Show();
            }
            catch (Exception ex) { MessageBox.Show($"Open image failed: {ex.Message}"); }
        }

        private void InsertEmoji(string? emoji)
        {
            if (!string.IsNullOrEmpty(emoji)) MessageText += emoji;
        }

        private void BackToMain()
        {
            _isDisconnecting = true;
            try { _writer.Dispose(); _stream.Close(); _client.Close(); } catch { }
            Application.Current.Dispatcher.Invoke(() => {
                foreach (Window window in Application.Current.Windows) { if (window is ChatWindow) window.Close(); }
            });
        }

        private async Task SendLineAsync(string message)
        {
            await _sendLock.WaitAsync();
            try { await _writer.WriteLineAsync(message); } finally { _sendLock.Release(); }
        }

        private void SendFile()
        {
            var dialog = new OpenFileDialog { Filter = "All files (*.*)|*.*", Multiselect = false };
            if (dialog.ShowDialog() == true) _ = SendFileAsync(dialog.FileName, isImage: false);
        }

        private void SendImage()
        {
            var dialog = new OpenFileDialog { Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Multiselect = false };
            if (dialog.ShowDialog() == true) _ = SendFileAsync(dialog.FileName, isImage: true);
        }

        private static bool IsImageFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif";
        }

        private static string EncodeBase64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        private static string DecodeBase64(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}