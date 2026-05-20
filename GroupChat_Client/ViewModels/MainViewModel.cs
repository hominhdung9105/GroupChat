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

        private void Connect()
        {
            // 1. Trim dữ liệu để loại bỏ khoảng trắng thừa ở đầu và cuối
            string trimmedUsername = Username?.Trim() ?? string.Empty;
            string trimmedIp = ServerIp?.Trim() ?? string.Empty;
            string trimmedPort = Port?.Trim() ?? string.Empty;

            // 2. Gán ngược lại để giao diện tự động xóa các dấu cách thừa mà user lỡ nhập
            Username = trimmedUsername;
            ServerIp = trimmedIp;
            Port = trimmedPort;

            // Bước 1: Validate (Kiểm tra) dữ liệu đầu vào cơ bản
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Username cannot be empty", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(ServerIp) || !IPAddress.TryParse(ServerIp, out _))
            {
                MessageBox.Show("Invalid IP Address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!int.TryParse(Port, out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("Invalid Port Number", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Bắt đầu trạng thái Connecting (Hiện Loading Spinner nếu có)
            IsConnecting = true;

            try
            {
                // Bước 2: Tạo kết nối TcpClient tới Server
                TcpClient client = new TcpClient();
                client.Connect(ServerIp, port);

                // THÊM MỚI (FIX LỖI): Thiết lập luồng ẩn/hiện Window an toàn
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Lấy tham chiếu đến cái MainWindow đang hiển thị lúc này
                    Window currentMainWindow = Application.Current.MainWindow;

                    // Khởi tạo ChatWindow và truyền các tham số vào
                    ChatWindow chatWindow = new ChatWindow(client, Username, ServerIp);

                    // ẨN MainWindow đi (Thay vì dùng lệnh Close() gây chết Window)
                    currentMainWindow.Hide();

                    // Tắt trạng thái Loading
                    IsConnecting = false;

                    // HIỆN ChatWindow lên và "CHỜ" người dùng tương tác trong đó.
                    // Hàm ShowDialog() sẽ chặn code đứng im tại dòng này cho đến khi 
                    // ChatWindow bị tắt (Bấm Back, lỗi trùng tên văng ra, hoặc ấn nút X)
                    chatWindow.ShowDialog();

                    // MỘT KHI DÒNG NÀY ĐƯỢC CHẠY (Nghĩa là ChatWindow đã biến mất):
                    // Ta an toàn gọi lệnh Show() để bật lại MainWindow cũ!
                    currentMainWindow.Show();
                });
            }
            catch (Exception ex)
            {
                IsConnecting = false;

                // Nếu không thể kết nối tới Server (Sai IP, Server chưa bật...)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Connect failed: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
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