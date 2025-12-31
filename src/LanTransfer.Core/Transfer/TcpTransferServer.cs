using System.Net;
using System.Net.Sockets;
using System.Text;
using LanTransfer.Core.Models;

namespace LanTransfer.Core.Transfer;

/// <summary>
/// TCP server for receiving files from peers.
/// Uses chunk-based reading to handle large files without loading into memory.
/// </summary>
public class TcpTransferServer : IDisposable
{
    private const int BUFFER_SIZE = 8192; // 8KB chunks
    
    private readonly TcpListener _listener;
    private readonly string _downloadFolder;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private bool _disposed;
    
    /// <summary>
    /// Event raised when a file transfer request is received (before accepting).
    /// Handler should return true to accept, false to reject.
    /// </summary>
    public event Func<FileTransferRequest, Task<bool>>? TransferRequested;
    
    /// <summary>
    /// Event raised when transfer progress updates.
    /// </summary>
    public event EventHandler<TransferInfo>? TransferProgress;
    
    /// <summary>
    /// Event raised when a transfer completes successfully.
    /// </summary>
    public event EventHandler<string>? TransferCompleted;
    
    /// <summary>
    /// Event raised when a transfer fails.
    /// </summary>
    public event EventHandler<(string FileName, string Error)>? TransferFailed;
    
    /// <summary>
    /// The port the server is listening on.
    /// </summary>
    public int Port { get; }
    
    public TcpTransferServer(int port = 0, string? downloadFolder = null)
    {
        _downloadFolder = downloadFolder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "LanTransfer"
        );
        
        Directory.CreateDirectory(_downloadFolder);
        
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }
    
    /// <summary>
    /// Starts accepting incoming file transfers.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;
        
        _isRunning = true;
        _cts = new CancellationTokenSource();
        
        Task.Run(() => AcceptLoopAsync(_cts.Token));
    }
    
    /// <summary>
    /// Stops the server.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        _cts?.Cancel();
        _listener.Stop();
    }
    
    /// <summary>
    /// Main loop accepting incoming connections.
    /// </summary>
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                // Handle each connection in its own task
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Accept error: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Handles an incoming file transfer.
    /// </summary>
    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        string fileName = "unknown";
        string tempPath = string.Empty;
        
        try
        {
            using (client)
            using (var stream = client.GetStream())
            {
                // 1. Read metadata header
                var headerBuffer = new byte[4];
                await ReadExactlyAsync(stream, headerBuffer, 0, 4, ct);
                int headerLength = BitConverter.ToInt32(headerBuffer, 0);
                
                var metadataBuffer = new byte[headerLength];
                await ReadExactlyAsync(stream, metadataBuffer, 0, headerLength, ct);
                var metadata = Encoding.UTF8.GetString(metadataBuffer);
                
                var parts = metadata.Split('|');
                fileName = SanitizeFileName(parts[0]);
                long fileSize = long.Parse(parts[1]);
                string senderId = parts.Length > 2 ? parts[2] : "Unknown";
                string senderName = parts.Length > 3 ? parts[3] : "Unknown";
                
                // 2. Ask user if they want to accept
                var request = new FileTransferRequest
                {
                    Sender = new Peer { Id = senderId, Name = senderName },
                    FileName = fileName,
                    FileSize = fileSize
                };
                
                bool accepted = true;
                if (TransferRequested != null)
                {
                    accepted = await TransferRequested.Invoke(request);
                }
                
                // 3. Send acceptance response
                var response = new byte[] { accepted ? (byte)1 : (byte)0 };
                await stream.WriteAsync(response, ct);
                
                if (!accepted)
                {
                    Console.WriteLine($"[Server] Transfer rejected: {fileName}");
                    return;
                }
                
                // 4. Receive file data in chunks
                string finalPath = GetUniqueFilePath(fileName);
                tempPath = finalPath + ".tmp";
                
                var transferInfo = new TransferInfo
                {
                    FileName = fileName,
                    TotalBytes = fileSize
                };
                
                var buffer = new byte[BUFFER_SIZE];
                long totalReceived = 0;
                DateTime lastUpdate = DateTime.UtcNow;
                long lastBytes = 0;
                
                await using (var fileStream = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    BUFFER_SIZE,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    while (totalReceived < fileSize)
                    {
                        int toRead = (int)Math.Min(BUFFER_SIZE, fileSize - totalReceived);
                        int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, toRead), ct);
                        
                        if (bytesRead == 0)
                        {
                            throw new IOException("Connection closed unexpectedly");
                        }
                        
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                        totalReceived += bytesRead;
                        
                        // Update progress (throttled to every 100ms)
                        var now = DateTime.UtcNow;
                        if ((now - lastUpdate).TotalMilliseconds >= 100)
                        {
                            double seconds = (now - lastUpdate).TotalSeconds;
                            transferInfo.TransferredBytes = totalReceived;
                            transferInfo.BytesPerSecond = (totalReceived - lastBytes) / seconds;
                            
                            TransferProgress?.Invoke(this, transferInfo);
                            
                            lastUpdate = now;
                            lastBytes = totalReceived;
                        }
                    }
                }
                
                // 5. Rename temp file to final
                File.Move(tempPath, finalPath, overwrite: true);
                tempPath = string.Empty;
                
                // 6. Check if it's a folder ZIP and extract it
                string resultPath = finalPath;
                if (FolderCompressor.IsCompressedFolder(fileName))
                {
                    try
                    {
                        Console.WriteLine($"[Server] Extracting folder: {fileName}");
                        string extractedPath = FolderCompressor.ExtractZip(finalPath, _downloadFolder);
                        
                        // Delete the ZIP file after extraction
                        File.Delete(finalPath);
                        
                        resultPath = extractedPath;
                        Console.WriteLine($"[Server] Folder extracted to: {extractedPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Server] Failed to extract folder: {ex.Message}");
                        // Keep the ZIP on failure
                    }
                }
                
                // Final progress update
                transferInfo.TransferredBytes = fileSize;
                TransferProgress?.Invoke(this, transferInfo);
                
                TransferCompleted?.Invoke(this, resultPath);
                Console.WriteLine($"[Server] Transfer complete: {resultPath}");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"[Server] Transfer cancelled: {fileName}");
            TransferFailed?.Invoke(this, (fileName, "Transfer cancelled"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Transfer error: {ex.Message}");
            TransferFailed?.Invoke(this, (fileName, ex.Message));
        }
        finally
        {
            // Clean up temp file if exists
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }
    
    /// <summary>
    /// Reads exactly the specified number of bytes from the stream.
    /// </summary>
    private static async Task ReadExactlyAsync(
        NetworkStream stream, 
        byte[] buffer, 
        int offset, 
        int count, 
        CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int bytesRead = await stream.ReadAsync(
                buffer.AsMemory(offset + totalRead, count - totalRead), ct);
            
            if (bytesRead == 0)
            {
                throw new IOException("Connection closed unexpectedly");
            }
            
            totalRead += bytesRead;
        }
    }
    
    /// <summary>
    /// Sanitizes a file name to prevent path traversal attacks.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());
        
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }
    
    /// <summary>
    /// Gets a unique file path, appending numbers if file exists.
    /// </summary>
    private string GetUniqueFilePath(string fileName)
    {
        string basePath = Path.Combine(_downloadFolder, fileName);
        
        if (!File.Exists(basePath)) return basePath;
        
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        
        int counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(_downloadFolder, $"{nameWithoutExt} ({counter}){ext}");
            counter++;
        } while (File.Exists(newPath));
        
        return newPath;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        Stop();
        _cts?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
