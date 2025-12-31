using System.Text.Json;
using LanTransfer.Core.Models;

namespace LanTransfer.Core.Friends;

/// <summary>
/// Manages persistent storage of remote friends.
/// </summary>
public class FriendsManager
{
    private readonly string _friendsFilePath;
    private List<RemoteFriend> _friends = new();
    
    public event EventHandler<List<RemoteFriend>>? FriendsChanged;
    
    public IReadOnlyList<RemoteFriend> Friends => _friends.AsReadOnly();
    
    public FriendsManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LanTransfer"
        );
        Directory.CreateDirectory(appDataPath);
        _friendsFilePath = Path.Combine(appDataPath, "friends.json");
        
        Load();
    }
    
    /// <summary>
    /// Loads friends from disk.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_friendsFilePath))
            {
                var json = File.ReadAllText(_friendsFilePath);
                _friends = JsonSerializer.Deserialize<List<RemoteFriend>>(json) ?? new();
                Console.WriteLine($"[Friends] Loaded {_friends.Count} friends");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Friends] Failed to load: {ex.Message}");
            _friends = new();
        }
    }
    
    /// <summary>
    /// Saves friends to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_friends, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_friendsFilePath, json);
            Console.WriteLine($"[Friends] Saved {_friends.Count} friends");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Friends] Failed to save: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Adds a new friend.
    /// </summary>
    public RemoteFriend AddFriend(string name, string ipAddress, int port)
    {
        var friend = new RemoteFriend
        {
            Id = Guid.NewGuid().ToString("N")[..16],
            Name = name,
            IpAddress = ipAddress,
            TcpPort = port,
            IsOnline = false,
            AddedAt = DateTime.UtcNow
        };
        
        // Check if already exists
        var existing = _friends.FirstOrDefault(f => 
            f.IpAddress == ipAddress && f.TcpPort == port);
        
        if (existing != null)
        {
            existing.Name = name;
            existing.LastSeen = DateTime.UtcNow;
        }
        else
        {
            _friends.Add(friend);
        }
        
        Save();
        FriendsChanged?.Invoke(this, _friends);
        
        return existing ?? friend;
    }
    
    /// <summary>
    /// Removes a friend.
    /// </summary>
    public void RemoveFriend(string id)
    {
        _friends.RemoveAll(f => f.Id == id);
        Save();
        FriendsChanged?.Invoke(this, _friends);
    }
    
    /// <summary>
    /// Updates friend online status.
    /// </summary>
    public void UpdateOnlineStatus(string ipAddress, int port, bool isOnline)
    {
        var friend = _friends.FirstOrDefault(f => 
            f.IpAddress == ipAddress && f.TcpPort == port);
        
        if (friend != null)
        {
            friend.IsOnline = isOnline;
            if (isOnline)
            {
                friend.LastSeen = DateTime.UtcNow;
            }
            FriendsChanged?.Invoke(this, _friends);
        }
    }
    
    /// <summary>
    /// Converts friends to Peer list for unified handling.
    /// </summary>
    public List<Peer> ToPeers()
    {
        return _friends.Select(f => new Peer
        {
            Id = f.Id,
            Name = $"⭐ {f.Name}",  // Star to indicate friend
            IpAddress = f.IpAddress,
            TcpPort = f.TcpPort,
            LastSeen = f.LastSeen
        }).ToList();
    }
}

/// <summary>
/// Represents a saved remote friend.
/// </summary>
public class RemoteFriend
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public bool IsOnline { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime LastSeen { get; set; }
}
