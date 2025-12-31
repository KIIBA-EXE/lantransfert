using System.Net.Sockets;
using System.Text;
using LanTransfer.Core.Models;

namespace LanTransfer.Core.Transfer;

/// <summary>
/// TCP client for sending files to peers.
/// Uses chunk-based writing to handle large files without loading into memory.
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
    /// Sends a file to a peer.
    /// </summary>
    /// <param name="peer">The target peer.</param>
    /// <param name="filePath">Path to the file to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if transfer was accepted and completed successfully.</returns>
    public async Task<bool> SendFileAsync(
        Peer peer, 
        string filePath, 
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("File not found", filePath);
        }
        
        var fileInfo = new FileInfo(filePath);
        string fileName = fileInfo.Name;
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
            
            // 2. Send metadata header
            var metadata = $"{fileName}|{fileSize}|{_localId}|{_localName}";
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
                FileName = fileName,
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
            
            Console.WriteLine($"[Client] Transfer complete: {fileName} -> {peer.Name}");
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
    /// Sends multiple files to a peer.
    /// </summary>
    public async Task<int> SendFilesAsync(
        Peer peer, 
        IEnumerable<string> filePaths, 
        CancellationToken cancellationToken = default)
    {
        int successCount = 0;
        
        foreach (var filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                if (await SendFileAsync(peer, filePath, cancellationToken))
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Client] Failed to send {filePath}: {ex.Message}");
            }
        }
        
        return successCount;
    }
}
