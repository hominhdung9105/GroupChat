using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

class AsyncChatServer
{
    // Lắng nghe lệnh Chat & Hệ thống (Control Channel)
    static TcpListener chatListener = null!;
    // Lắng nghe luồng truyền nhận File/Ảnh (Data Channel)
    static TcpListener fileListener = null!;

    static ConcurrentDictionary<int, ClientInfo> clients = new();

    // Thư mục lưu trữ file vật lý tập trung trên Server
    static readonly string StoragePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerStorage");
    static int nextId = 0;

    static async Task Main()
    {
        // Khởi tạo thư mục lưu trữ tập trung trên ổ đĩa Server nếu chưa tồn tại
        Directory.CreateDirectory(StoragePath);

        // Khởi tạo Listener chính cho luồng Chat (Hệ điều hành tự chọn port trống ngẫu nhiên)
        chatListener = new TcpListener(IPAddress.Any, 0);
        chatListener.Start();

        IPEndPoint endPoint = (IPEndPoint)chatListener.LocalEndpoint;
        int chatPort = endPoint.Port;
        // Port dành cho File dữ liệu nặng sẽ bằng Port Chat + 1
        int filePort = chatPort + 1;

        // Khởi tạo Listener phụ độc lập cho luồng File
        fileListener = new TcpListener(IPAddress.Any, filePort);
        fileListener.Start();

        Console.WriteLine("Server started successfully!");
        Console.WriteLine("=======================================");
        Console.WriteLine($"Chat Port (Control): {chatPort}");
        Console.WriteLine($"File Port (Data)   : {filePort}");
        Console.WriteLine("=======================================");

        List<IPAddress> localIps = GetLocalIPv4Addresses();
        Console.WriteLine("Connect using one of these IPs:");
        foreach (IPAddress ip in localIps)
        {
            Console.WriteLine($"IP: {ip}    Port: {chatPort}");
        }

        // Chạy song song 2 luồng độc lập chấp nhận kết nối từ 2 Port riêng biệt
        _ = Task.Run(() => AcceptChatClientsAsync());
        _ = Task.Run(() => AcceptFileClientsAsync());

        // Giữ Server luôn luôn chạy ngầm
        await Task.Delay(-1);
    }

    // LUỒNG 1: Xử lý kết nối Chat Text & Lệnh điều khiển (Port chính)
    static async Task AcceptChatClientsAsync()
    {
        while (true)
        {
            try
            {
                TcpClient client = await chatListener.AcceptTcpClientAsync();
                int id = Interlocked.Increment(ref nextId);
                _ = Task.Run(() => HandleChatClientAsync(id, client));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting chat client: {ex.Message}");
            }
        }
    }

    static async Task HandleChatClientAsync(int id, TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 128 * 1024, true);
        using var writer = new StreamWriter(stream, Encoding.UTF8, 128 * 1024, true) { AutoFlush = true };

        string username = string.Empty;
        try
        {
            string? firstLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(firstLine)) return;

            string[] parts = firstLine.Split('|');
            if (parts.Length >= 2 && parts[0] == "CONNECT")
            {
                username = parts[1];

                // Kiểm tra trùng tên thành viên trong phòng chat
                if (clients.Values.Any(c => string.Equals(c.Username, username, StringComparison.OrdinalIgnoreCase)))
                {
                    await writer.WriteLineAsync("ERROR|Tên người dùng này đã có trong phòng chat. Vui lòng chọn tên khác!");
                    client.Close();
                    return;
                }

                var clientInfo = new ClientInfo
                {
                    Id = id,
                    Username = username,
                    Client = client,
                    Writer = writer
                };

                clients.TryAdd(id, clientInfo);
                Console.WriteLine($"Client '{username}' connected on Chat Channel.");

                await BroadcastAsync($"SYSTEM|{username} đã tham gia phòng chat.");
                await BroadcastAsync($"USERS_COUNT|{clients.Count}");
                await BroadcastAsync(BuildUsersListMessage());

                // Vòng lặp liên tục nhận dữ liệu điều khiển từ client phát lên
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] msgParts = line.Split('|');

                    // CHỈ Broadcast gói tin FILE_START để các user khác hiển thị khung tin nhắn trống kèm nút "Tải Xuống"
                    // KHÔNG Broadcast các dòng FILE_CHUNK dữ liệu nặng qua kênh chat chữ này nữa
                    if (msgParts[0] == "FILE_START")
                    {
                        await BroadcastAsync(line);
                    }
                    else
                    {
                        // Các tin nhắn TEXT thông thường hoặc thông báo hệ thống khác
                        await BroadcastAsync(line);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chat error with client {username}: {ex.Message}");
        }
        finally
        {
            // SỬA LỖI: Dùng out var để tránh xung đột kiểu Nullable context trên compiler cũ
            if (clients.TryRemove(id, out var info))
            {
                info.Client.Close();
                Console.WriteLine($"Client '{username}' disconnected from Chat Channel.");
                await BroadcastAsync($"SYSTEM|{username} đã rời phòng chat.");
                await BroadcastAsync($"USERS_COUNT|{clients.Count}");
                await BroadcastAsync(BuildUsersListMessage());
            }
        }
    }

    // LUỒNG 2: Xử lý kết nối truyền nhận FILE/DATA (Port phụ = Port chính + 1)
    static async Task AcceptFileClientsAsync()
    {
        while (true)
        {
            try
            {
                TcpClient fileClient = await fileListener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleFileStreamAsync(fileClient));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accepting file client: {ex.Message}");
            }
        }
    }

    static async Task HandleFileStreamAsync(TcpClient fileClient)
    {
        NetworkStream stream = fileClient.GetStream();
        // Cấu hình buffer đọc/ghi luồng file lên tối đa để tải file dung lượng lớn cực nhanh
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 256 * 1024, true);
        using var writer = new StreamWriter(stream, Encoding.UTF8, 256 * 1024, true) { AutoFlush = true };

        string? registerLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(registerLine)) return;

        string[] parts = registerLine.Split('|');
        string action = parts[0];

        // HÀNH ĐỘNG 1: USER GỬI FILE LÊN (UPLOADER) -> Ghi trực tiếp xuống ổ đĩa Server
        if (action == "REG_FILE_ID" && parts.Length >= 2)
        {
            string fileId = parts[1];
            string serverFilePath = Path.Combine(StoragePath, fileId);

            try
            {
                using var fs = new FileStream(serverFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("FILE_CHUNK|"))
                    {
                        string[] chunkParts = line.Split('|', 3);
                        if (chunkParts.Length == 3)
                        {
                            byte[] bytes = Convert.FromBase64String(chunkParts[2]);
                            await fs.WriteAsync(bytes, 0, bytes.Length);
                        }
                    }
                    else if (line == "FILE_UPLOAD_END")
                    {
                        break;
                    }
                }
                Console.WriteLine($"[SERVER STORAGE] Đã lưu file ID: {fileId} thành công vào Server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER STORAGE ERROR] Lỗi khi đang lưu file {fileId}: {ex.Message}");
                if (File.Exists(serverFilePath)) File.Delete(serverFilePath);
            }
            finally
            {
                fileClient.Close();
            }
        }
        // HÀNH ĐỘNG 2: USER NHẤN NÚT ĐỂ TẢI FILE VỀ (DOWNLOADER) -> Server đọc từ ổ đĩa đẩy xuống
        else if (action == "DOWNLOAD_REQUEST" && parts.Length >= 2)
        {
            string fileId = parts[1];
            string serverFilePath = Path.Combine(StoragePath, fileId);

            try
            {
                if (!File.Exists(serverFilePath))
                {
                    await writer.WriteLineAsync("DOWNLOAD_ERROR|File không tồn tại trên hệ thống hoặc đã bị xóa khỏi Server.");
                    return;
                }

                // Gửi phản hồi đồng ý cấp phép tải dữ liệu cho client
                await writer.WriteLineAsync($"DOWNLOAD_ACCEPTED|{fileId}");

                byte[] buffer = new byte[64 * 1024];
                using var fs = new FileStream(serverFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, useAsync: true);

                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    string chunkBase64 = Convert.ToBase64String(buffer, 0, bytesRead);
                    await writer.WriteLineAsync($"FILE_CHUNK|{fileId}|{chunkBase64}");
                }
                // Phát tín hiệu thông báo luồng kéo file kết thúc
                await writer.WriteLineAsync("DOWNLOAD_END");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER DOWNLOAD ERROR] Lỗi khi đang truyền file cho downloader: {ex.Message}");
            }
            finally
            {
                fileClient.Close();
            }
        }
    }

    static string BuildUsersListMessage()
    {
        var names = clients.Values.Select(c => Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Username)));
        return $"USERS_LIST|{string.Join(';', names)}";
    }

    static async Task BroadcastAsync(string message)
    {
        // Xử lý hiển thị thông tin log đẹp lên màn hình Terminal của Server
        try
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                string[] parts = message.Split('|');
                string prefix = parts[0];

                switch (prefix)
                {
                    case "TEXT":
                        if (parts.Length >= 3)
                        {
                            string sender = parts[1];
                            string content = parts[2];
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [CHAT] {sender}: {content}");
                        }
                        break;

                    case "SYSTEM":
                        if (parts.Length >= 2)
                        {
                            string content = parts[1];
                            Console.ForegroundColor = ConsoleColor.Yellow; // Đổi chữ sang màu vàng cho thông báo hệ thống
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SYSTEM] {content}");
                            Console.ResetColor();
                        }
                        break;

                    case "FILE_START":
                        if (parts.Length >= 5)
                        {
                            string sender = parts[1];
                            string fileName = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
                            long.TryParse(parts[4], out long size);
                            double sizeInMb = size / (1024.0 * 1024.0);

                            Console.ForegroundColor = ConsoleColor.Cyan; // Màu xanh lam cho việc đăng ký file
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [FILE REGISTER] {sender} đăng tải: {fileName} ({sizeInMb:0.##} MB) -> FileID: {parts[2]}");
                            Console.ResetColor();
                        }
                        break;

                    default:
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOG ERROR] Lỗi hiển thị terminal: {ex.Message}");
        }

        // Thực hiện Broadcast đẩy chuỗi xuống cho tất cả Client đang lắng nghe phòng chat chữ công cộng
        List<int> deadClients = new();
        var activeClients = clients.ToList();

        var tasks = activeClients.Select(async pair =>
        {
            int id = pair.Key;
            var clientInfo = pair.Value;

            await clientInfo.SendLock.WaitAsync();
            try
            {
                if (clientInfo.Client.Connected)
                {
                    await clientInfo.Writer.WriteLineAsync(message);
                }
            }
            catch
            {
                lock (deadClients) { deadClients.Add(id); }
            }
            finally
            {
                clientInfo.SendLock.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Dọn dẹp các Client đột ngột ngắt kết nối (Mất mạng, crash ứng dụng...)
        foreach (int id in deadClients)
        {
            // SỬA LỖI: Dùng out var đồng bộ sửa đổi an toàn
            if (clients.TryRemove(id, out var dead))
            {
                dead.Client.Close();
            }
        }
    }

    private static List<IPAddress> GetLocalIPv4Addresses()
    {
        List<IPAddress> ipList = new();
        foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (item.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                item.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipList.Add(ip.Address);
                    }
                }
            }
        }
        return ipList;
    }
}

// Cấu trúc đối tượng lưu giữ thông tin kết nối và bộ khóa luồng của mỗi Client tại Server
class ClientInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public TcpClient Client { get; set; } = null!;
    public StreamWriter Writer { get; set; } = null!;
    public SemaphoreSlim SendLock { get; } = new(1, 1);
}