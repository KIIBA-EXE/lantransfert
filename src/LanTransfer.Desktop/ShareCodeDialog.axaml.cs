using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LanTransfer.Core.Signaling;

namespace LanTransfer.Desktop;

public partial class ShareCodeDialog : Window
{
    private readonly SignalingClient _signalingClient;
    private readonly int _port;
    private readonly string _name;
    
    public string? Code { get; private set; }
    
    public ShareCodeDialog()
    {
        InitializeComponent();
        _signalingClient = new SignalingClient();
        _port = 0;
        _name = "";
    }
    
    public ShareCodeDialog(SignalingClient client, int port, string name) : this()
    {
        _signalingClient = client;
        _port = port;
        _name = name;
        
        // Register when dialog opens
        Opened += OnDialogOpened;
    }
    
    private async void OnDialogOpened(object? sender, EventArgs e)
    {
        await RegisterAsync();
    }
    
    private async Task RegisterAsync()
    {
        LoadingState.IsVisible = true;
        CodeState.IsVisible = false;
        ErrorState.IsVisible = false;
        
        try
        {
            Code = await _signalingClient.RegisterAsync(_port, _name);
            
            if (Code != null)
            {
                // Format as ABC-123
                string formatted = Code.Length == 6 
                    ? $"{Code[..3]}-{Code[3..]}" 
                    : Code;
                    
                CodeText.Text = formatted;
                
                LoadingState.IsVisible = false;
                CodeState.IsVisible = true;
            }
            else
            {
                ShowError("Impossible d'obtenir un code. Vérifiez votre connexion.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Erreur: {ex.Message}");
        }
    }
    
    private void ShowError(string message)
    {
        LoadingState.IsVisible = false;
        CodeState.IsVisible = false;
        ErrorState.IsVisible = true;
        ErrorText.Text = message;
    }
    
    private async void OnCopyCode(object? sender, RoutedEventArgs e)
    {
        if (Code != null && Clipboard != null)
        {
            await Clipboard.SetTextAsync(Code);
            
            // Visual feedback
            if (sender is Button btn)
            {
                var original = btn.Content;
                btn.Content = "✓ Copié !";
                await Task.Delay(1500);
                btn.Content = original;
            }
        }
    }
    
    private void OnClose(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
