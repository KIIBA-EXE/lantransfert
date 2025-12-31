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
using LanTransfer.Core.Models;
using LanTransfer.Core.Transfer;

namespace LanTransfer.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly UdpDiscoveryService _discoveryService;
    private readonly TcpTransferServer _transferServer;
    private readonly TcpTransferClient _transferClient;
    
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
        
        // Wire up events
        _discoveryService.PeersChanged += OnPeersChanged;
        _transferServer.TransferProgress += OnTransferProgress;
        _transferServer.TransferCompleted += OnTransferCompleted;
        _transferServer.TransferFailed += OnTransferFailed;
        _transferServer.TransferRequested += OnTransferRequested;
        _transferClient.TransferProgress += OnTransferProgress;
        
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
        _discoveryService.Dispose();
        _transferServer.Dispose();
    }
    
    private void OnPeersChanged(object? sender, List<Peer> peers)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Peers.Clear();
            foreach (var peer in peers)
            {
                Peers.Add(peer);
            }
            
            OnPropertyChanged(nameof(PeerCount));
            OnPropertyChanged(nameof(PeerCountText));
            OnPropertyChanged(nameof(HasPeers));
            OnPropertyChanged(nameof(StatusColor));
            
            StatusMessage = HasPeers 
                ? $"{PeerCount} appareil(s) détecté(s)" 
                : "Recherche d'appareils sur le réseau...";
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
        // Auto-accept for now (could show dialog later)
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusMessage = $"Réception de {request.FileName} ({request.FileSizeFormatted})...";
            IsTransferring = true;
        });
        
        return true;
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
            
            await SendFilesAsync(paths);
        }
    }
    
    [RelayCommand]
    private void SelectPeer(Peer peer)
    {
        SelectedPeer = peer;
        StatusMessage = $"Sélectionné: {peer.Name}";
    }
    
    public async Task SendFilesAsync(List<string> filePaths)
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
            foreach (var path in filePaths)
            {
                CurrentTransfer = new TransferInfo
                {
                    FileName = Path.GetFileName(path),
                    TotalBytes = new FileInfo(path).Length
                };
                
                OnPropertyChanged(nameof(TransferFileName));
                
                bool success = await _transferClient.SendFileAsync(SelectedPeer, path);
                
                if (success)
                {
                    StatusMessage = $"Envoyé: {Path.GetFileName(path)}";
                }
                else
                {
                    StatusMessage = $"Refusé: {Path.GetFileName(path)}";
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
