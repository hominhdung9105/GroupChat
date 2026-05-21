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
    static TcpListener listener = null!;

    static ConcurrentDictionary<int, ClientInfo> clients = new();

    static int nextId = 0;

    static async Task Main()
    {
        // Port = 0 nghĩa là hệ điều hành tự chọn port trống ngẫu nhiên
        listener = new TcpListener(IPAddress.Any, 0);

        listener.Start();

        IPEndPoint endPoint = (IPEndPoint)listener.LocalEndpoint;
        int port = endPoint.Port;

        Console.WriteLine("Server started");
        Console.WriteLine("==============================");
        Console.WriteLine($"Port: {port}");
        Console.WriteLine();

        List<IPAddress> localIps = GetLocalIPv4Addresses();

        Console.WriteLine("Connect using one of these IPs:");

        foreach (IPAddress ip in localIps)
        {
            Console.WriteLine($"IP: {ip}    Port: {port}");
        }

        //Console.WriteLine();
        //Console.WriteLine("If client is on the same computer:");
        //Console.WriteLine($"IP: 127.0.0.1    Port: {port}");
        //Console.WriteLine("==============================");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();

            int id = Interlocked.Increment(ref nextId);

            _ = HandleClientAsync(id, client);
        }
    }

    static List<IPAddress> GetLocalIPv4Addresses()
    {
        List<IPAddress> ips = new();

        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
                continue;

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            IPInterfaceProperties properties = networkInterface.GetIPProperties();

            foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
            {
                if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ips.Add(address.Address);
                }
            }
        }

        if (ips.Count == 0)
        {
            ips.Add(IPAddress.Loopback);
        }

        return ips;
    }

    static async Task HandleClientAsync(int id, TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true)
        {
            AutoFlush = true
        };
        string username = $"Client {id}";

        try
        {
            // Tin nhắn đầu tiên client gửi lên sẽ là username
            string? usernameLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(usernameLine))
                return;

            username = usernameLine.Trim();

            // Kiểm tra xem tên đã tồn tại chưa (không phân biệt hoa/thường)
            bool isDuplicate = clients.Values.Any(c => c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (isDuplicate)
            {
                Console.WriteLine($"Connection rejected: Username '{username}' already exists.");

                // Gửi thông báo lỗi về cho Client bị trùng tên
                byte[] errorData = Encoding.UTF8.GetBytes("ERROR|Tên người dùng này đã có trong phòng. Vui lòng chọn tên khác!\n");
                await stream.WriteAsync(errorData, 0, errorData.Length);

                // Đóng kết nối của người này và dừng hàm lại (Nó sẽ nhảy xuống khối finally)
                client.Close();
                return;
            }

            // Nếu không trùng thì mới thêm vào danh sách và đi tiếp
            clients[id] = new ClientInfo
            {
                Id = id,
                Username = username,
                Client = client,
                Writer = writer
            };

            Console.WriteLine($"{username} connected");
            await BroadcastAsync($"TEXT|System|{username} connected");

            // FIX LỖI 1: Phải phát tín hiệu cập nhật số người cho mọi người khi có người VÀO THÀNH CÔNG
            await BroadcastAsync($"USERS_COUNT|{clients.Count}");
            await BroadcastAsync(BuildUsersListMessage());

            while (true)
            {
                string? message = await reader.ReadLineAsync();

                if (message == null)
                    break;

                if (string.IsNullOrWhiteSpace(message))
                    continue;

                Console.WriteLine($"{username}: {message}");

                await BroadcastAsync(message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{username} error: {ex.Message}");
        }
        finally
        {
            // FIX LỖI 2: Hàm TryRemove sẽ trả về TRUE nếu ID này có trong danh sách (Tức là đã vào thành công)
            // Nếu người này bị từ chối do trùng tên ở trên, TryRemove sẽ trả về FALSE.
            bool isJoinedSuccessfully = clients.TryRemove(id, out _);

            client.Close();

            // CHỈ thông báo disconnect và cập nhật lại số người nếu trước đó họ đã vào phòng thành công
            if (isJoinedSuccessfully)
            {
                Console.WriteLine($"{username} disconnected");

                await BroadcastAsync($"TEXT|System|{username} disconnected");

                // Phát tín hiệu cập nhật số người khi có người RỜI ĐI
                await BroadcastAsync($"USERS_COUNT|{clients.Count}");
                await BroadcastAsync(BuildUsersListMessage());
            }
        }
    }

    static string BuildUsersListMessage()
    {
        var names = clients.Values
            .Select(c => Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Username)));

        return $"USERS_LIST|{string.Join(';', names)}";
    }

    static async Task BroadcastAsync(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n");

        List<int> deadClients = new();

        foreach (var pair in clients)
        {
            int id = pair.Key;

            TcpClient client = pair.Value.Client;
            StreamWriter writer = pair.Value.Writer;

            try
            {
                await writer.WriteLineAsync(message);
            }
            catch
            {
                deadClients.Add(id);
            }
        }

        foreach (int id in deadClients)
        {
            if (clients.TryRemove(id, out ClientInfo? dead))
            {
                dead.Client.Close();
            }
        }
    }
}

class ClientInfo
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public TcpClient Client { get; set; } = null!;

    public StreamWriter Writer { get; set; } = null!;
}