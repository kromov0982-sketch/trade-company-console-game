using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace TradeCompany;

public sealed class CoopPacket
{
    public string Type { get; set; } = "";
    public string? Command { get; set; }
    public Game? Game { get; set; }
    public string? Message { get; set; }
    public bool HostReady { get; set; }
    public bool ClientReady { get; set; }
}

public sealed class CoopLink : IDisposable
{
    public const int Port = 47777;
    private readonly TcpClient client;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;
    private readonly ConcurrentQueue<CoopPacket> incoming = new();
    private readonly object sendLock = new();
    private bool disposed;
    private int disconnectReported;

    private CoopLink(TcpClient client)
    {
        this.client = client;
        client.NoDelay = true;
        var stream = client.GetStream();
        reader = new StreamReader(stream);
        writer = new StreamWriter(stream) { AutoFlush = true };
        _ = Task.Run(ReadLoop);
    }

    public static async Task<CoopLink> HostAsync(CancellationToken cancellation)
    {
        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start(1);
        try
        {
            var client = await listener.AcceptTcpClientAsync(cancellation);
            return new CoopLink(client);
        }
        finally { listener.Stop(); }
    }

    public static async Task<CoopLink> JoinAsync(string address, CancellationToken cancellation)
    {
        var client = new TcpClient();
        await client.ConnectAsync(address, Port, cancellation);
        return new CoopLink(client);
    }

    public void Send(CoopPacket packet)
    {
        if (disposed) return;
        string json = JsonSerializer.Serialize(packet);
        try { lock (sendLock) writer.WriteLine(json); }
        catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException)
        {
            ReportDisconnect();
        }
    }

    public bool TryReceive(out CoopPacket packet) => incoming.TryDequeue(out packet!);

    private async Task ReadLoop()
    {
        try
        {
            while (!disposed && await reader.ReadLineAsync() is { } line)
            {
                var packet = JsonSerializer.Deserialize<CoopPacket>(line);
                if (packet is not null) incoming.Enqueue(packet);
            }
        }
        catch (Exception e) when (e is IOException or SocketException or JsonException or ObjectDisposedException) { }
        finally
        {
            if (!disposed) ReportDisconnect();
        }
    }

    private void ReportDisconnect()
    {
        if (Interlocked.Exchange(ref disconnectReported, 1) == 0)
            incoming.Enqueue(new CoopPacket { Type = "disconnect" });
    }

    public void Dispose()
    {
        disposed = true;
        client.Dispose();
        reader.Dispose();
        writer.Dispose();
    }

    public static string LocalAddresses()
    {
        try
        {
            var addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                .Select(a => a.ToString()).Distinct().ToArray();
            return addresses.Length == 0 ? "127.0.0.1" : string.Join("  |  ", addresses);
        }
        catch (SocketException) { return "127.0.0.1"; }
    }
}
