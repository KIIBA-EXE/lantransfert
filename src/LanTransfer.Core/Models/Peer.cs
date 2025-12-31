namespace LanTransfer.Core.Models;

/// <summary>
/// Represents a discovered peer on the local network.
/// </summary>
public class Peer
{
    /// <summary>
    /// Unique identifier for the peer (machine name + random suffix).
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name of the peer (usually the machine name).
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// IP address of the peer.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// TCP port for file transfer.
    /// </summary>
    public int TcpPort { get; set; }
    
    /// <summary>
    /// Last time this peer was seen (for timeout detection).
    /// </summary>
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether the peer is currently online.
    /// </summary>
    public bool IsOnline => (DateTime.UtcNow - LastSeen).TotalSeconds < 10;
    
    public override string ToString() => $"{Name} ({IpAddress})";
    
    public override bool Equals(object? obj)
    {
        if (obj is Peer other)
            return Id == other.Id;
        return false;
    }
    
    public override int GetHashCode() => Id.GetHashCode();
}
