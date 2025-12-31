using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LanTransfer.Desktop;

public partial class AddFriendDialog : Window
{
    public bool WasAdded { get; private set; }
    public string FriendName { get; private set; } = string.Empty;
    public string IpAddress { get; private set; } = string.Empty;
    public int Port { get; private set; }
    
    public AddFriendDialog()
    {
        InitializeComponent();
    }
    
    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        WasAdded = false;
        Close();
    }
    
    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        // Validate inputs
        var name = FriendNameInput.Text?.Trim();
        var ip = IpAddressInput.Text?.Trim();
        var portStr = PortInput.Text?.Trim();
        
        // Validate name
        if (string.IsNullOrEmpty(name))
        {
            ShowError("Veuillez entrer un nom");
            return;
        }
        
        // Validate IP
        if (string.IsNullOrEmpty(ip))
        {
            ShowError("Veuillez entrer une adresse IP");
            return;
        }
        
        if (!IPAddress.TryParse(ip, out _))
        {
            // Check if it's a hostname
            if (!Uri.CheckHostName(ip).Equals(UriHostNameType.Dns))
            {
                ShowError("Adresse IP invalide");
                return;
            }
        }
        
        // Validate port
        if (string.IsNullOrEmpty(portStr) || !int.TryParse(portStr, out int port))
        {
            ShowError("Port invalide");
            return;
        }
        
        if (port < 1 || port > 65535)
        {
            ShowError("Le port doit être entre 1 et 65535");
            return;
        }
        
        // All valid
        FriendName = name;
        IpAddress = ip;
        Port = port;
        WasAdded = true;
        Close();
    }
    
    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
