using Avalonia.Controls;
using Avalonia.Interactivity;
using LanTransfer.Core.Models;

namespace LanTransfer.Desktop;

public partial class TransferRequestDialog : Window
{
    public bool Accepted { get; private set; }
    
    public TransferRequestDialog()
    {
        InitializeComponent();
    }
    
    public TransferRequestDialog(FileTransferRequest request) : this()
    {
        // Set sender info
        SenderInitial.Text = string.IsNullOrEmpty(request.Sender.Name) 
            ? "?" 
            : request.Sender.Name[0].ToString().ToUpper();
        SenderNameText.Text = request.Sender.Name;
        
        // Set file info
        FileNameText.Text = request.FileName;
        FileSizeText.Text = request.FileSizeFormatted;
    }
    
    private void OnAccept(object? sender, RoutedEventArgs e)
    {
        Accepted = true;
        Close();
    }
    
    private void OnReject(object? sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close();
    }
}
