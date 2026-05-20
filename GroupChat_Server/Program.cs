using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class AsyncChatServer
{
    // Server listener
    static TcpListener listener = null!;

    // Danh sách client đang online
    static ConcurrentDictionary<int, ClientInfo> clients = new();

    // Tăng id tự động cho mỗi client
    static int nextId = 0;

    static async Task Main()
    {
        // FIXED PORT cho dễ test
        int port = 5000;

        listener = new TcpListener(IPAddress.Any, port);

        listener.Start();

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

            Console.WriteLine($"New socket connected -> ID: {id}");

            // Chạy riêng cho từng client
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

        // FIX:
        // Dùng line-based protocol thay vì raw byte buffer
        StreamReader reader = new StreamReader(stream, Encoding.UTF8);

        StreamWriter writer = new StreamWriter(stream, Encoding.UTF8)
        {
            AutoFlush = true
        };

        string username = $"Client {id}";

        try
        {
            // =========================
            // RECEIVE USERNAME
            // =========================

            string? usernameLine = await reader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(usernameLine))
                return;

            username = usernameLine.Trim();

            // Lưu client
            clients[id] = new ClientInfo
            {
                Id = id,
                Username = username,
                Client = client,
                Reader = reader,
                Writer = writer
            };

            Console.WriteLine($"{username} connected");

            // Broadcast system message
            await BroadcastAsync($"System|{username} connected");

            // Broadcast online count
            await BroadcastAsync($"USERS_COUNT|{clients.Count}");

            // =========================
            // RECEIVE LOOP
            // =========================

            while (true)
            {
                // FIX:
                // ReadLineAsync đảm bảo:
                // 1 dòng = 1 message
                string? message = await reader.ReadLineAsync();

                // Client disconnected
                if (message == null)
                    break;

                message = message.Trim();

                if (string.IsNullOrWhiteSpace(message))
                    continue;

                Console.WriteLine($"{username}: {message}");

                // Broadcast chat message
                await BroadcastAsync($"{username}|{message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{username} error: {ex.Message}");
        }
        finally
        {
            // Remove client
            clients.TryRemove(id, out _);

            client.Close();

            Console.WriteLine($"{username} disconnected");

            // Notify everyone
            await BroadcastAsync($"System|{username} disconnected");

            await BroadcastAsync($"USERS_COUNT|{clients.Count}");
        }
    }

    static async Task BroadcastAsync(string message)
    {
        List<int> deadClients = new();

        foreach (var pair in clients)
        {
            int id = pair.Key;

            ClientInfo clientInfo = pair.Value;

            try
            {
                // FIX:
                // WriteLineAsync tự thêm '\n'
                // => protocol ổn định hơn
                await clientInfo.Writer.WriteLineAsync(message);
            }
            catch
            {
                deadClients.Add(id);
            }
        }

        // Remove dead clients
        foreach (int id in deadClients)
        {
            if (clients.TryRemove(id, out ClientInfo? dead))
            {
                dead.Client.Close();

                Console.WriteLine($"{dead.Username} force removed");
            }
        }
    }
}

class ClientInfo
{
    public int Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public TcpClient Client { get; set; } = null!;

    // FIX:
    // Giữ persistent reader/writer
    // tránh tạo lại liên tục
    public StreamReader Reader { get; set; } = null!;

    public StreamWriter Writer { get; set; } = null!;
}