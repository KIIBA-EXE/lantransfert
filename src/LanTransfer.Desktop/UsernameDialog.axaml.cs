using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LanTransfer.Desktop;

public partial class UsernameDialog : Window
{
    public bool WasSaved { get; private set; }
    public string Username { get; private set; } = "";
    
    public UsernameDialog()
    {
        InitializeComponent();
    }
    
    public UsernameDialog(string? currentUsername) : this()
    {
        if (!string.IsNullOrEmpty(currentUsername))
        {
            UsernameInput.Text = currentUsername;
        }
    }
    
    private void OnContinue(object? sender, RoutedEventArgs e)
    {
        var username = UsernameInput.Text?.Trim();
        
        if (string.IsNullOrEmpty(username))
        {
            ErrorText.Text = "Veuillez entrer un pseudo";
            ErrorText.IsVisible = true;
            return;
        }
        
        if (username.Length < 2)
        {
            ErrorText.Text = "Minimum 2 caractères";
            ErrorText.IsVisible = true;
            return;
        }
        
        Username = username;
        WasSaved = true;
        Close();
    }
}
