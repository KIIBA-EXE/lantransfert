using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LanTransfer.Core.Models;
using LanTransfer.Desktop.ViewModels;

namespace LanTransfer.Desktop;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;
    
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
        // Set up drag-drop handlers
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
        
        // Start services when window is shown
        Opened += OnOpened;
        Closing += OnClosing;
    }
    
    private async void OnOpened(object? sender, EventArgs e)
    {
        // Check username first
        await ViewModel.CheckUsernameAsync();
        ViewModel.StartServices();
    }
    
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        ViewModel.StopServices();
    }
    
    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
            ViewModel.IsDragOver = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }
    
    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        ViewModel.IsDragOver = false;
    }
    
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        ViewModel.IsDragOver = false;
        
        if (e.Data.Contains(DataFormats.Files))
        {
            var items = e.Data.GetFiles();
            if (items != null)
            {
                var paths = new List<string>();
                foreach (var item in items)
                {
                    var path = item.TryGetLocalPath();
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Accept both files and directories
                        if (File.Exists(path) || Directory.Exists(path))
                        {
                            paths.Add(path);
                        }
                    }
                }
                
                if (paths.Count > 0)
                {
                    await ViewModel.SendPathsAsync(paths);
                }
            }
        }
    }
    
    private void OnPeerTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is Peer peer)
        {
            ViewModel.SelectedPeer = peer;
        }
    }
    
    private async void OnDropZoneTapped(object? sender, RoutedEventArgs e)
    {
        await ViewModel.SelectFileCommand.ExecuteAsync(null);
    }
    
    private async void OnProfileTapped(object? sender, RoutedEventArgs e)
    {
        await ViewModel.EditProfileCommand.ExecuteAsync(null);
    }
    
    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
