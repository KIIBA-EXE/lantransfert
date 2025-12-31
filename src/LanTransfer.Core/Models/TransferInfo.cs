namespace LanTransfer.Core.Models;

/// <summary>
/// Represents the progress of a file transfer.
/// </summary>
public class TransferInfo
{
    /// <summary>
    /// Name of the file being transferred.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Total size of the file in bytes.
    /// </summary>
    public long TotalBytes { get; set; }
    
    /// <summary>
    /// Number of bytes transferred so far.
    /// </summary>
    public long TransferredBytes { get; set; }
    
    /// <summary>
    /// Current transfer speed in bytes per second.
    /// </summary>
    public double BytesPerSecond { get; set; }
    
    /// <summary>
    /// Whether the transfer is complete.
    /// </summary>
    public bool IsComplete => TransferredBytes >= TotalBytes;
    
    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double ProgressPercent => TotalBytes > 0 
        ? (double)TransferredBytes / TotalBytes * 100 
        : 0;
    
    /// <summary>
    /// Estimated time remaining in seconds.
    /// </summary>
    public double EstimatedSecondsRemaining => BytesPerSecond > 0 
        ? (TotalBytes - TransferredBytes) / BytesPerSecond 
        : 0;
    
    /// <summary>
    /// Human-readable transfer speed.
    /// </summary>
    public string SpeedFormatted => FormatBytes(BytesPerSecond) + "/s";
    
    /// <summary>
    /// Human-readable progress.
    /// </summary>
    public string ProgressFormatted => $"{FormatBytes(TransferredBytes)} / {FormatBytes(TotalBytes)}";
    
    private static string FormatBytes(double bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        while (bytes >= 1024 && order < sizes.Length - 1)
        {
            order++;
            bytes /= 1024;
        }
        return $"{bytes:0.##} {sizes[order]}";
    }
}
