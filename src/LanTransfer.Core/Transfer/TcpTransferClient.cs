using System.Net.Sockets;
using System.Text;
using LanTransfer.Core.Models;

namespace LanTransfer.Core.Transfer;

/// <summary>
/// TCP client for sending files and folders to peers.
/// Uses chunk-based writing to handle large files without loading into memory.
/// Folders are automatically compressed to ZIP before sending.
/// </summary>
public class TcpTransferClient
{
    private const int BUFFER_SIZE = 8192; // 8KB chunks
    private const int CONNECT_TIMEOUT_MS = 5000;
    
    private readonly string _localId;
    private readonly string _localName;
    
    /// <summary>
    /// Event raised when transfer progress updates.
    /// </summary>
    public event EventHandler<TransferInfo>? TransferProgress;
    
    public TcpTransferClient()
    {
        _localId = $"{Environment.MachineName}-{Guid.NewGuid():N}"[..16];
        _localName = Environment.MachineName;
    }
    
    /// <summary>
    /// Sends a file or folder to a peer.
    /// Folders are automatically compressed to ZIP before sending.
    /// </summary>
    /// <param name="peer">The target peer.</param>
    /// <param name="path">Path to the file or folder to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if transfer was accepted and completed successfully.</returns>
    public async Task<bool> SendAsync(
        Peer peer, 
        string path, 
        CancellationToken cancellationToken = default)
    {
        // Check if it's a folder
        if (FolderCompressor.IsFolder(path))
        {
            return await SendFolderAsync(peer, path, cancellationToken);
        }
        else
        {
            return await SendFileAsync(peer, path, cancellationToken);
        }
    }
    
    /// <summary>
    /// Sends a folder to a peer (compressed as ZIP).
    /// </summary>
    public async Task<bool> SendFolderAsync(
        Peer peer,
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }
        
        string? tempZipPath = null;
        
        try
        {
            // Compress folder to temporary ZIP
            Console.WriteLine($"[Client] Compressing folder: {folderPath}");
            tempZipPath = FolderCompressor.CompressFolder(folderPath);
            
            // Get the folder ZIP name (with .folder.zip marker)
            string zipFileName = FolderCompressor.GetFolderZipName(folderPath);
            
            // Send the ZIP with the special folder marker filename
            return await SendFileInternalAsync(peer, tempZipPath, zipFileName, cancellationToken);
        }
        finally
        {
            // Clean up temporary ZIP file
            if (!string.IsNullOrEmpty(tempZipPath) && File.Exists(tempZipPath))
            {
                try { File.Delete(tempZipPath); } catch { }
            }
        }
    }
    
    /// <summary>
    /// Sends a file to a peer.
    /// </summary>
    public async Task<bool> SendFileAsync(
        Peer peer, 
        string filePath, 
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }
        
        string fileName = Path.GetFileName(filePath);
        return await SendFileInternalAsync(peer, filePath, fileName, cancellationToken);
    }
    
    /// <summary>
    /// Internal method to send a file with a specified display name.
    /// </summary>
    private async Task<bool> SendFileInternalAsync(
        Peer peer,
        string filePath,
        string displayFileName,
        CancellationToken cancellationToken = default)
    {
        var fileInfo = new FileInfo(filePath);
        long fileSize = fileInfo.Length;
        
        TcpClient? client = null;
        
        try
        {
            // 1. Connect to peer with timeout
            client = new TcpClient();
            
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(CONNECT_TIMEOUT_MS);
            
            try
            {
                await client.ConnectAsync(peer.IpAddress, peer.TcpPort, connectCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Connection to {peer.Name} timed out");
            }
            
            using var stream = client.GetStream();
            
            // 2. Send metadata header (using display filename, not actual path)
            var metadata = $"{displayFileName}|{fileSize}|{_localId}|{_localName}";
            var metadataBytes = Encoding.UTF8.GetBytes(metadata);
            var headerBytes = BitConverter.GetBytes(metadataBytes.Length);
            
            await stream.WriteAsync(headerBytes, cancellationToken);
            await stream.WriteAsync(metadataBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            
            // 3. Wait for acceptance response
            var response = new byte[1];
            int bytesRead = await stream.ReadAsync(response, cancellationToken);
            
            if (bytesRead == 0 || response[0] == 0)
            {
                Console.WriteLine($"[Client] Transfer rejected by {peer.Name}");
                return false;
            }
            
            // 4. Send file data in chunks
            var transferInfo = new TransferInfo
            {
                FileName = displayFileName,
                TotalBytes = fileSize
            };
            
            var buffer = new byte[BUFFER_SIZE];
            long totalSent = 0;
            DateTime lastUpdate = DateTime.UtcNow;
            long lastBytes = 0;
            
            await using (var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                BUFFER_SIZE,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                while (totalSent < fileSize)
                {
                    int toRead = (int)Math.Min(BUFFER_SIZE, fileSize - totalSent);
                    bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        throw new IOException("Unexpected end of file");
                    }
                    
                    await stream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalSent += bytesRead;
                    
                    // Update progress (throttled to every 100ms)
                    var now = DateTime.UtcNow;
                    if ((now - lastUpdate).TotalMilliseconds >= 100)
                    {
                        double seconds = (now - lastUpdate).TotalSeconds;
                        transferInfo.TransferredBytes = totalSent;
                        transferInfo.BytesPerSecond = (totalSent - lastBytes) / seconds;
                        
                        TransferProgress?.Invoke(this, transferInfo);
                        
                        lastUpdate = now;
                        lastBytes = totalSent;
                    }
                }
            }
            
            await stream.FlushAsync(cancellationToken);
            
            // Final progress update
            transferInfo.TransferredBytes = fileSize;
            TransferProgress?.Invoke(this, transferInfo);
            
            Console.WriteLine($"[Client] Transfer complete: {displayFileName} -> {peer.Name}");
            return true;
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"[Client] Socket error: {ex.SocketErrorCode} - {ex.Message}");
            throw new IOException($"Connection error: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[Client] IO error: {ex.Message}");
            throw;
        }
        finally
        {
            client?.Close();
            client?.Dispose();
        }
    }
    
    /// <summary>
    /// Sends multiple files or folders to a peer.
    /// </summary>
    public async Task<int> SendMultipleAsync(
        Peer peer, 
        IEnumerable<string> paths, 
        CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        
        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                if (await SendAsync(peer, path, cancellationToken))
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Failed to send {path}: {ex.Message}");
            }
        }
        
        return successCount;
    }
    
    /// <summary>
    /// Sends multiple files to a peer (legacy method for compatibility).
    /// </summary>
    public async Task<int> SendFilesAsync(
        Peer peer, 
        IEnumerable<string> filePaths, 
        CancellationToken cancellationToken = default)
    {
        return await SendMultipleAsync(peer, filePaths, cancellationToken);
    }
}
