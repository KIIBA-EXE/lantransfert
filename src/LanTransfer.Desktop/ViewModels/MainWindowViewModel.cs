using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LanTransfer.Core.Discovery;
using LanTransfer.Core.Friends;
using LanTransfer.Core.Models;
using LanTransfer.Core.Signaling;
using LanTransfer.Core.Transfer;

namespace LanTransfer.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly UdpDiscoveryService _discoveryService;
    private readonly TcpTransferServer _transferServer;
    private readonly TcpTransferClient _transferClient;
    private readonly FriendsManager _friendsManager;
    private readonly SignalingClient _signalingClient;
    
    // Server URL - change this when deploying
    private const string SIGNALING_SERVER_URL = "https://lantransfer-signaling.onrender.com";
    
    private List<Peer> _discoveredPeers = new();
    
    [ObservableProperty]
    private ObservableCollection<Peer> _peers = new();
    
    [ObservableProperty]
    private Peer? _selectedPeer;
    
    [ObservableProperty]
    private TransferInfo? _currentTransfer;
    
    [ObservableProperty]
    private bool _isTransferring;
    
    [ObservableProperty]
    private bool _isDragOver;
    
    [ObservableProperty]
    private string _statusMessage = "Initialisation...";
    
    [ObservableProperty]
    private string _localAddress = "";
    
    // Simplified binding properties
    public int PeerCount => Peers.Count;
    public bool HasPeers => Peers.Count > 0;
    public string PeerCountText => $"{PeerCount} en ligne";
    
    public string TransferFileName => CurrentTransfer?.FileName ?? "";
    public string TransferSpeed => CurrentTransfer?.SpeedFormatted ?? "";
    public double TransferProgress => CurrentTransfer?.ProgressPercent ?? 0;
    public string TransferProgressText => CurrentTransfer?.ProgressFormatted ?? "";
    public string TransferPercent => $"{TransferProgress:F1}%";
    
    public IBrush StatusColor => HasPeers 
        ? new SolidColorBrush(Color.Parse("#22c55e")) 
        : new SolidColorBrush(Color.Parse("#eab308"));
    
    public IBrush DropZoneBackground => IsDragOver 
        ? new SolidColorBrush(Color.Parse("#1e3a5f")) 
        : new SolidColorBrush(Colors.Transparent);
    
    public MainWindowViewModel()
    {
        // Initialize TCP server first to get the port
        _transferServer = new TcpTransferServer();
        
        // Initialize UDP discovery with the TCP port
        _discoveryService = new UdpDiscoveryService(_transferServer.Port);
        
        // Initialize transfer client
        _transferClient = new TcpTransferClient();
        
        // Initialize friends manager
        _friendsManager = new FriendsManager();
        
        // Initialize signaling client
        _signalingClient = new SignalingClient(SIGNALING_SERVER_URL);
        
        // Wire up events
        _discoveryService.PeersChanged += OnPeersChanged;
        _friendsManager.FriendsChanged += OnFriendsChanged;
        _transferServer.TransferProgress += OnTransferProgress;
        _transferServer.TransferCompleted += OnTransferCompleted;
        _transferServer.TransferFailed += OnTransferFailed;
        _transferServer.TransferRequested += OnTransferRequested;
        _transferClient.TransferProgress += OnTransferProgress;
        
        // Load friends on startup
        LoadFriendsIntoPeers();
        
        // Get local IP
        LocalAddress = $"IP: {GetLocalIPAddress()}:{_transferServer.Port}";
    }
    
    public void StartServices()
    {
        _discoveryService.Start();
        _transferServer.Start();
        StatusMessage = "Recherche d'appareils sur le réseau...";
    }
    
    public void StopServices()
    {
        _discoveryService.Stop();
        _transferServer.Stop();
        _signalingClient.UnregisterAsync().Wait();
        _discoveryService.Dispose();
        _transferServer.Dispose();
        _signalingClient.Dispose();
    }
    
    private void OnPeersChanged(object? sender, List<Peer> peers)
    {
        _discoveredPeers = peers;
        RefreshPeersList();
    }
    
    private void OnFriendsChanged(object? sender, List<RemoteFriend> friends)
    {
        RefreshPeersList();
    }
    
    private void LoadFriendsIntoPeers()
    {
        RefreshPeersList();
    }
    
    private void RefreshPeersList()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Peers.Clear();
            
            // Add friends first (marked with star)
            foreach (var friend in _friendsManager.Friends)
            {
                Peers.Add(new Peer
                {
                    Id = friend.Id,
                    Name = $"⭐ {friend.Name}",
                    IpAddress = friend.IpAddress,
                    TcpPort = friend.TcpPort,
                    LastSeen = friend.LastSeen
                });
            }
            
            // Add discovered peers (not already in friends)
            foreach (var peer in _discoveredPeers)
            {
                bool isFriend = _friendsManager.Friends.Any(f => 
                    f.IpAddress == peer.IpAddress && f.TcpPort == peer.TcpPort);
                
                if (!isFriend)
                {
                    Peers.Add(peer);
                }
            }
            
            OnPropertyChanged(nameof(PeerCount));
            OnPropertyChanged(nameof(PeerCountText));
            OnPropertyChanged(nameof(HasPeers));
            OnPropertyChanged(nameof(StatusColor));
            
            int friendCount = _friendsManager.Friends.Count;
            int discoveredCount = _discoveredPeers.Count;
            
            if (friendCount > 0 || discoveredCount > 0)
            {
                StatusMessage = $"{friendCount} ami(s), {discoveredCount} détecté(s)";
            }
            else
            {
                StatusMessage = "Recherche d'appareils sur le réseau...";
            }
        });
    }
    
    private void OnTransferProgress(object? sender, TransferInfo info)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            CurrentTransfer = info;
            IsTransferring = !info.IsComplete;
            
            OnPropertyChanged(nameof(TransferFileName));
            OnPropertyChanged(nameof(TransferSpeed));
            OnPropertyChanged(nameof(TransferProgress));
            OnPropertyChanged(nameof(TransferProgressText));
            OnPropertyChanged(nameof(TransferPercent));
        });
    }
    
    private void OnTransferCompleted(object? sender, string filePath)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsTransferring = false;
            StatusMessage = $"Reçu: {Path.GetFileName(filePath)}";
        });
    }
    
    private void OnTransferFailed(object? sender, (string FileName, string Error) args)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsTransferring = false;
            StatusMessage = $"Échec: {args.FileName} - {args.Error}";
        });
    }
    
    private async Task<bool> OnTransferRequested(FileTransferRequest request)
    {
        // Show confirmation dialog on UI thread
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new TransferRequestDialog(request);
            
            // Get the main window
            if (Application.Current?.ApplicationLifetime 
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
                && desktop.MainWindow != null)
            {
                await dialog.ShowDialog(desktop.MainWindow);
                
                if (dialog.Accepted)
                {
                    StatusMessage = $"Réception de {request.FileName} ({request.FileSizeFormatted})...";
                    IsTransferring = true;
                    return true;
                }
                else
                {
                    StatusMessage = $"Transfert refusé: {request.FileName}";
                    return false;
                }
            }
            
            return false;
        });
    }
    
    [RelayCommand]
    private async Task SelectFile()
    {
        if (SelectedPeer == null)
        {
            if (Peers.Count > 0)
            {
                SelectedPeer = Peers[0];
            }
            else
            {
                StatusMessage = "Aucun appareil disponible pour l'envoi";
                return;
            }
        }
        
        var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime 
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
            ? desktop.MainWindow : null);
        
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Sélectionner un fichier à envoyer",
            AllowMultiple = true
        });
        
        if (files.Count > 0)
        {
            var paths = new List<string>();
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }
            
            await SendPathsAsync(paths);
        }
    }
    
    [RelayCommand]
    private async Task SelectFolder()
    {
        if (SelectedPeer == null)
        {
            if (Peers.Count > 0)
            {
                SelectedPeer = Peers[0];
            }
            else
            {
                StatusMessage = "Aucun appareil disponible pour l'envoi";
                return;
            }
        }
        
        var topLevel = TopLevel.GetTopLevel(Application.Current?.ApplicationLifetime 
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
            ? desktop.MainWindow : null);
        
        if (topLevel == null) return;
        
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Sélectionner un dossier à envoyer",
            AllowMultiple = false
        });
        
        if (folders.Count > 0)
        {
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                await SendPathsAsync([path]);
            }
        }
    }
    
    [RelayCommand]
    private void SelectPeer(Peer peer)
    {
        SelectedPeer = peer;
        StatusMessage = $"Sélectionné: {peer.Name}";
    }
    
    [RelayCommand]
    private async Task AddFriend()
    {
        if (Application.Current?.ApplicationLifetime 
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
            && desktop.MainWindow != null)
        {
            var dialog = new AddFriendDialog();
            await dialog.ShowDialog(desktop.MainWindow);
            
            if (dialog.WasAdded)
            {
                var friend = _friendsManager.AddFriend(
                    dialog.FriendName,
                    dialog.IpAddress,
                    dialog.Port
                );
                
                StatusMessage = $"Ami ajouté: {friend.Name} ({friend.IpAddress}:{friend.TcpPort})";
            }
        }
    }
    
    [RelayCommand]
    private void RemoveFriend(Peer peer)
    {
        // Remove from friends if it's a friend (has star prefix)
        if (peer.Name.StartsWith("⭐"))
        {
            _friendsManager.RemoveFriend(peer.Id);
            StatusMessage = $"Ami supprimé: {peer.Name.Replace("⭐ ", "")}";
        }
    }
    
    [RelayCommand]
    private async Task ShareCode()
    {
        if (Application.Current?.ApplicationLifetime 
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
            && desktop.MainWindow != null)
        {
            var dialog = new ShareCodeDialog(_signalingClient, _transferServer.Port, Environment.MachineName);
            await dialog.ShowDialog(desktop.MainWindow);
            
            if (dialog.Code != null)
            {
                StatusMessage = $"Code de partage: {dialog.Code}";
            }
        }
    }
    
    [RelayCommand]
    private async Task EnterCode()
    {
        if (Application.Current?.ApplicationLifetime 
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop 
            && desktop.MainWindow != null)
        {
            var dialog = new EnterCodeDialog(_signalingClient);
            await dialog.ShowDialog(desktop.MainWindow);
            
            if (dialog.WasConnected && dialog.PeerInfo != null)
            {
                // Add as friend automatically
                var friend = _friendsManager.AddFriend(
                    dialog.PeerInfo.Name ?? "Ami",
                    dialog.PeerInfo.Ip!,
                    dialog.PeerInfo.Port
                );
                
                StatusMessage = $"Connecté à {friend.Name} via code!";
            }
        }
    }
    
    public async Task SendFilesAsync(List<string> filePaths) => await SendPathsAsync(filePaths);
    
    public async Task SendPathsAsync(List<string> paths)
    {
        if (SelectedPeer == null)
        {
            if (Peers.Count > 0)
            {
                SelectedPeer = Peers[0];
            }
            else
            {
                StatusMessage = "Aucun appareil disponible pour l'envoi";
                return;
            }
        }
        
        IsTransferring = true;
        StatusMessage = $"Envoi vers {SelectedPeer.Name}...";
        
        try
        {
            foreach (var path in paths)
            {
                bool isFolder = Directory.Exists(path);
                string displayName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
                
                CurrentTransfer = new TransferInfo
                {
                    FileName = isFolder ? $"📁 {displayName}" : displayName,
                    TotalBytes = isFolder ? GetFolderSize(path) : new FileInfo(path).Length
                };
                
                OnPropertyChanged(nameof(TransferFileName));
                
                bool success = await _transferClient.SendAsync(SelectedPeer, path);
                
                if (success)
                {
                    StatusMessage = $"Envoyé: {displayName}";
                }
                else
                {
                    StatusMessage = $"Refusé: {displayName}";
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur: {ex.Message}";
        }
        finally
        {
            IsTransferring = false;
        }
    }
    
    private static long GetFolderSize(string folderPath)
    {
        try
        {
            return new DirectoryInfo(folderPath)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }
    
    private static string GetLocalIPAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endpoint = socket.LocalEndPoint as IPEndPoint;
            return endpoint?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}

/// <summary>
/// Converter that returns the first character of a string in uppercase.
/// </summary>
public class FirstCharConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrEmpty(str))
        {
            return str[0].ToString().ToUpper();
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
