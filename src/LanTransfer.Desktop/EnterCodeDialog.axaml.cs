using Avalonia.Controls;
using Avalonia.Interactivity;
using LanTransfer.Core.Signaling;

namespace LanTransfer.Desktop;

public partial class EnterCodeDialog : Window
{
    private readonly SignalingClient _signalingClient;
    
    public bool WasConnected { get; private set; }
    public PeerInfo? PeerInfo { get; private set; }
    public string? Code { get; private set; }
    
    public EnterCodeDialog()
    {
        InitializeComponent();
        _signalingClient = new SignalingClient();
    }
    
    public EnterCodeDialog(SignalingClient client) : this()
    {
        _signalingClient = client;
    }
    
    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        WasConnected = false;
        Close();
    }
    
    private async void OnConnect(object? sender, RoutedEventArgs e)
    {
        var code = CodeInput.Text?.Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
        
        if (string.IsNullOrEmpty(code) || code.Length < 6)
        {
            ShowError("Veuillez entrer un code valide (6 caractères)");
            return;
        }
        
        // Show loading
        ConnectButton.Content = "⏳ Recherche...";
        ConnectButton.IsEnabled = false;
        ErrorText.IsVisible = false;
        
        try
        {
            var peer = await _signalingClient.LookupAsync(code);
            
            if (peer != null && !string.IsNullOrEmpty(peer.Ip))
            {
                PeerInfo = peer;
                Code = code;
                WasConnected = true;
                
                SuccessText.Text = $"✓ Trouvé: {peer.Name} ({peer.Ip})";
                SuccessText.IsVisible = true;
                
                await Task.Delay(1000);
                Close();
            }
            else
            {
                ShowError("Code non trouvé. Vérifiez le code ou demandez un nouveau.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Erreur: {ex.Message}");
        }
        finally
        {
            ConnectButton.Content = "🔗 Connecter";
            ConnectButton.IsEnabled = true;
        }
    }
    
    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
        SuccessText.IsVisible = false;
    }
}
