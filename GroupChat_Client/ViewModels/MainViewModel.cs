using GroupChat_Client.Commands;
using GroupChat_Client.Views;
using System;
using System.ComponentModel;
using System.Net; // Thêm thư viện này để Validate IP
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
        private bool _isConnecting;

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

        // Property quản lý trạng thái đang kết nối (Loading)
        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                _isConnecting = value;
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
            // Chặn ngay từ đầu nếu đang trong quá trình kết nối
            if (IsConnecting) return;

            // 1. Trim dữ liệu để loại bỏ khoảng trắng thừa ở đầu và cuối
            string trimmedUsername = Username?.Trim() ?? string.Empty;
            string trimmedIp = ServerIp?.Trim() ?? string.Empty;
            string trimmedPort = Port?.Trim() ?? string.Empty;

            // 2. Gán ngược lại để giao diện tự động xóa các dấu cách thừa mà user lỡ nhập
            Username = trimmedUsername;
            ServerIp = trimmedIp;
            Port = trimmedPort;

            // 3. Validate Username
            if (string.IsNullOrWhiteSpace(trimmedUsername))
            {
                MessageBox.Show("Please enter a valid username.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 4. Validate Server IP (Kiểm tra xem chuỗi nhập vào có phải là một địa chỉ IP hợp lệ không)
            if (string.IsNullOrWhiteSpace(trimmedIp) || !IPAddress.TryParse(trimmedIp, out IPAddress? parsedIp))
            {
                MessageBox.Show("Please enter a valid Server IP address (e.g., 127.0.0.1).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 5. Validate Port (Kiểm tra xem có phải là số và nằm trong dải port hợp lệ 1 - 65535 không)
            if (string.IsNullOrWhiteSpace(trimmedPort) ||
                !int.TryParse(trimmedPort, out int portNumber) ||
                portNumber < 1 || portNumber > 65535)
            {
                MessageBox.Show("Please enter a valid port number (1 - 65535).", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Bật trạng thái loading để UI cập nhật giao diện (Disable nút, đổi chữ thành Connecting...)
            IsConnecting = true;

            try
            {
                TcpClient client = new TcpClient();
                // Sử dụng trimmedIp thay vì ServerIp cũ để đảm bảo an toàn
                await client.ConnectAsync(trimmedIp, portNumber);

                ChatWindow chatWindow = new ChatWindow(client, trimmedUsername, trimmedIp);
                chatWindow.Show();

                // Logic đóng cửa sổ an toàn
                Application.Current.MainWindow = chatWindow;
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is MainWindow)
                    {
                        window.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connect failed: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Luôn luôn tắt trạng thái loading sau khi hoàn tất (dù lỗi hay thành công)
                IsConnecting = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}