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
    // Lắng nghe lệnh Chat & Hệ thống
    static TcpListener chatListener = null!;
    // Lắng nghe luồng truyền nhận File/Ảnh
    static TcpListener fileListener = null!;

    static ConcurrentDictionary<int, ClientInfo> clients = new();
    // Quản lý các kết nối truyền dữ liệu file
    static ConcurrentDictionary<string, NetworkStream> fileStreams = new();

    static int nextId = 0;

    static async Task Main()
    {
        // Khởi tạo Listener chính cho luồng Chat (Hệ điều hành chọn port trống ngẫu nhiên)
        chatListener = new TcpListener(IPAddress.Any, 0);
        chatListener.Start();

        IPEndPoint endPoint = (IPEndPoint)chatListener.LocalEndpoint;
        int chatPort = endPoint.Port;
        // Port dành cho File sẽ bằng Port Chat + 1
        int filePort = chatPort + 1;

        // Khởi tạo Listener phụ cho luồng File
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

        // Chạy song song 2 luồng chấp nhận kết nối từ 2 Port độc lập
        _ = Task.Run(() => AcceptChatClientsAsync());
        _ = Task.Run(() => AcceptFileClientsAsync());

        // Giữ Server luôn chạy
        await Task.Delay(-1);
    }

    // LUỒNG 1: Xử lý kết nối Chat Text (Port chính)
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

                // Kiểm tra trùng tên
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

                // Vòng lặp nhận dữ liệu chat
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Phân tách gói tin điều khiển gửi file từ luồng chat
                    string[] msgParts = line.Split('|');
                    if (msgParts[0] == "FILE_START" || msgParts[0] == "FILE_END")
                    {
                        // Lệnh điều khiển gửi file vẫn broadcast qua kênh chat để các client khác chuẩn bị giao diện nhận
                        await BroadcastAsync(line);
                    }
                    else
                    {
                        // Các tin nhắn TEXT thông thường
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
            if (clients.TryRemove(id, out ClientInfo? info))
            {
                info.Client.Close();
                Console.WriteLine($"Client '{username}' disconnected from Chat Channel.");
                await BroadcastAsync($"SYSTEM|{username} đã rời phòng chat.");
                await BroadcastAsync($"USERS_COUNT|{clients.Count}");
                await BroadcastAsync(BuildUsersListMessage());
            }
        }
    }

    // LUỒNG 2: Xử lý kết nối truyền nhận FILE (Port phụ = Port chính + 1)
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
        // Tăng kích thước buffer đọc/ghi luồng file lên tối đa để tải 3GB cực nhanh
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 256 * 1024, true);

        string? registerLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(registerLine)) return;

        string[] parts = registerLine.Split('|');
        if (parts.Length >= 2 && parts[0] == "REG_FILE_ID")
        {
            string fileId = parts[1];

            // Đăng ký luồng này vào danh sách phân phối file toàn cục
            fileStreams.TryAdd(fileId, stream);

            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("FILE_CHUNK|"))
                    {
                        // Chuyển tiếp (Route) trực tiếp dòng chunk này sang cho tất cả các client đang kết nối luồng chat chữ
                        // Lưu ý: Việc Route này diễn ra hoàn toàn trên Port File, không đi qua Port Chat
                        await BroadcastFileChunkAsync(fileId, line);
                    }
                }
            }
            catch
            {
                // Xử lý khi ngắt kết nối tải file
            }
            finally
            {
                fileStreams.TryRemove(fileId, out _);
                fileClient.Close();
            }
        }
    }

    static async Task BroadcastFileChunkAsync(string activeFileId, string chunkMessage)
    {
        // Phát dữ liệu chunk đến tất cả các Client khác thông qua luồng phát tin nhắn điều khiển
        // Tuy nhiên, để tối ưu, ta vẫn mượn cơ chế ghi an toàn bằng bộ lock
        await BroadcastAsync(chunkMessage);
    }

    static string BuildUsersListMessage()
    {
        var names = clients.Values.Select(c => Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Username)));
        return $"USERS_LIST|{string.Join(';', names)}";
    }

    static async Task BroadcastAsync(string message)
    {
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

        foreach (int id in deadClients)
        {
            if (clients.TryRemove(id, out ClientInfo? dead))
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

class ClientInfo
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public TcpClient Client { get; set; } = null!;
    public StreamWriter Writer { get; set; } = null!;
    public SemaphoreSlim SendLock { get; } = new(1, 1);
}