using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        Console.WriteLine();
        Console.WriteLine("If client is on the same computer:");
        Console.WriteLine($"IP: 127.0.0.1    Port: {port}");
        Console.WriteLine("==============================");

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
        byte[] buffer = new byte[4096];
        string username = $"Client {id}";

        try
        {
            // Tin nhắn đầu tiên client gửi lên sẽ là username
            int usernameBytes = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (usernameBytes == 0) return;

            username = Encoding.UTF8.GetString(buffer, 0, usernameBytes).Trim();

            // THÊM MỚI: Kiểm tra xem tên đã tồn tại chưa (không phân biệt hoa/thường)
            bool isDuplicate = clients.Values.Any(c => c.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (isDuplicate)
            {
                Console.WriteLine($"Connection rejected: Username '{username}' already exists.");

                // Gửi thông báo lỗi về cho Client bị trùng tên
                byte[] errorData = Encoding.UTF8.GetBytes("ERROR|Tên người dùng này đã có trong phòng. Vui lòng chọn tên khác!\n");
                await stream.WriteAsync(errorData, 0, errorData.Length);

                // Đóng kết nối của người này và dừng hàm lại
                client.Close();
                return;
            }

            // Nếu không trùng thì mới thêm vào danh sách và đi tiếp
            clients[id] = new ClientInfo
            {
                Id = id,
                Username = username,
                Client = client
            };

            Console.WriteLine($"{username} connected");
            await BroadcastAsync($"System|{username} connected");

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                    break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                Console.WriteLine($"{username}: {message}");

                await BroadcastAsync($"{username}|{message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{username} error: {ex.Message}");
        }
        finally
        {
            clients.TryRemove(id, out _);

            client.Close();

            Console.WriteLine($"{username} disconnected");

            await BroadcastAsync($"System|{username} disconnected");

            await BroadcastAsync($"USERS_COUNT|{clients.Count}");
        }
    }

    static async Task BroadcastAsync(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n");

        List<int> deadClients = new();

        foreach (var pair in clients)
        {
            int id = pair.Key;

            TcpClient client = pair.Value.Client;

            try
            {
                NetworkStream stream = client.GetStream();

                await stream.WriteAsync(data, 0, data.Length);
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
}