namespace LanTransfer.Core.Models;

/// <summary>
/// Represents a request to receive a file.
/// </summary>
public class FileTransferRequest
{
    /// <summary>
    /// The peer sending the file.
    /// </summary>
    public Peer Sender { get; set; } = new();
    
    /// <summary>
    /// Name of the file being sent.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// Human-readable file size.
    /// </summary>
    public string FileSizeFormatted
    {
        get
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double bytes = FileSize;
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }
            return $"{bytes:0.##} {sizes[order]}";
        }
    }
}
