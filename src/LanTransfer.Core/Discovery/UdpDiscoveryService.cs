using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LanTransfer.Core.Models;

namespace LanTransfer.Core.Discovery;

/// <summary>
/// UDP-based peer discovery service using broadcast.
/// Announces presence on the network and listens for other peers.
/// </summary>
public class UdpDiscoveryService : IDisposable
{
    private const int DISCOVERY_PORT = 45454;
    private const int BROADCAST_INTERVAL_MS = 2000;
    private const int PEER_TIMEOUT_SECONDS = 10;
    
    private readonly UdpClient _udpClient;
    private readonly string _localId;
    private readonly string _localName;
    private readonly int _tcpPort;
    private readonly Dictionary<string, Peer> _peers = new();
    private readonly object _peersLock = new();
    
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _disposed;
    
    /// <summary>
    /// Event raised when a new peer is discovered.
    /// </summary>
    public event EventHandler<Peer>? PeerDiscovered;
    
    /// <summary>
    /// Event raised when a peer goes offline.
    /// </summary>
    public event EventHandler<Peer>? PeerLost;
    
    /// <summary>
    /// Event raised when the peer list changes.
    /// </summary>
    public event EventHandler<List<Peer>>? PeersChanged;
    
    /// <summary>
    /// Gets the current list of online peers.
    /// </summary>
    public List<Peer> OnlinePeers
    {
        get
        {
            lock (_peersLock)
            {
                return _peers.Values.Where(p => p.IsOnline).ToList();
            }
        }
    }
    
    public UdpDiscoveryService(int tcpPort)
    {
        _tcpPort = tcpPort;
        _localId = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..16];
        _localName = Environment.MachineName;
        
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, DISCOVERY_PORT));
        _udpClient.EnableBroadcast = true;
    }
    
    /// <summary>
    /// Starts the discovery service (broadcast and listening).
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        
        _isRunning = true;
        _cts = new CancellationTokenSource();
        
        // Start broadcast task
        Task.Run(() => BroadcastLoopAsync(_cts.Token));
        
        // Start listener task
        Task.Run(() => ListenLoopAsync(_cts.Token));
        
        // Start cleanup task (remove stale peers)
        Task.Run(() => CleanupLoopAsync(_cts.Token));
    }
    
    /// <summary>
    /// Stops the discovery service.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        _cts?.Cancel();
    }
    
    /// <summary>
    /// Broadcasts presence on the network.
    /// </summary>
    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var message = new DiscoveryMessage
                {
                    Id = _localId,
                    Name = _localName,
                    TcpPort = _tcpPort
                };
                
                var json = JsonSerializer.Serialize(message);
                var data = Encoding.UTF8.GetBytes(json);
                
                await _udpClient.SendAsync(data, data.Length, broadcastEndpoint);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[Discovery] Broadcast error: {ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            
            try
            {
                await Task.Delay(BROADCAST_INTERVAL_MS, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
    
    /// <summary>
    /// Listens for broadcasts from other peers.
    /// </summary>
    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(ct);
                var json = Encoding.UTF8.GetString(result.Buffer);
                
                var message = JsonSerializer.Deserialize<DiscoveryMessage>(json);
                if (message == null || message.Id == _localId) continue;
                
                var peer = new Peer
                {
                    Id = message.Id,
                    Name = message.Name,
                    IpAddress = result.RemoteEndPoint.Address.ToString(),
                    TcpPort = message.TcpPort,
                    LastSeen = DateTime.UtcNow
                };
                
                bool isNew;
                lock (_peersLock)
                {
                    isNew = !_peers.ContainsKey(peer.Id);
                    _peers[peer.Id] = peer;
                }
                
                if (isNew)
                {
                    PeerDiscovered?.Invoke(this, peer);
                }
                
                PeersChanged?.Invoke(this, OnlinePeers);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[Discovery] Receive error: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[Discovery] Parse error: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Periodically removes stale peers.
    /// </summary>
    private async Task CleanupLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            
            List<Peer> removedPeers = new();
            
            lock (_peersLock)
            {
                var staleIds = _peers
                    .Where(kvp => (DateTime.UtcNow - kvp.Value.LastSeen).TotalSeconds > PEER_TIMEOUT_SECONDS)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var id in staleIds)
                {
                    removedPeers.Add(_peers[id]);
                    _peers.Remove(id);
                }
            }
            
            foreach (var peer in removedPeers)
            {
                PeerLost?.Invoke(this, peer);
            }
            
            if (removedPeers.Count > 0)
            {
                PeersChanged?.Invoke(this, OnlinePeers);
            }
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Stop();
        _cts?.Dispose();
        _udpClient.Close();
        _udpClient.Dispose();
        
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Internal message format for discovery broadcasts.
    /// </summary>
    private class DiscoveryMessage
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int TcpPort { get; set; }
    }
}
