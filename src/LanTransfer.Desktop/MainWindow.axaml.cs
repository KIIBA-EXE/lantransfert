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
    
    private void OnOpened(object? sender, EventArgs e)
    {
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
            var files = e.Data.GetFiles();
            if (files != null)
            {
                var paths = new List<string>();
                foreach (var file in files)
                {
                    var path = file.TryGetLocalPath();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        paths.Add(path);
                    }
                }
                
                if (paths.Count > 0)
                {
                    await ViewModel.SendFilesAsync(paths);
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
}
