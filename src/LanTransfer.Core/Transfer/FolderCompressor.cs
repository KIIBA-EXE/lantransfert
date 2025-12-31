using System.IO.Compression;

namespace LanTransfer.Core.Transfer;

/// <summary>
/// Helper class for folder compression and decompression.
/// </summary>
public static class FolderCompressor
{
    /// <summary>
    /// Compresses a folder to a temporary ZIP file.
    /// </summary>
    /// <param name="folderPath">Path to the folder to compress.</param>
    /// <returns>Path to the temporary ZIP file.</returns>
    public static string CompressFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }
        
        string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
        string tempZipPath = Path.Combine(Path.GetTempPath(), $"LanTransfer_{folderName}_{Guid.NewGuid():N}.zip");
        
        // Delete if exists (shouldn't happen with GUID but just in case)
        if (File.Exists(tempZipPath))
        {
            File.Delete(tempZipPath);
        }
        
        ZipFile.CreateFromDirectory(folderPath, tempZipPath, CompressionLevel.Fastest, includeBaseDirectory: true);
        
        return tempZipPath;
    }
    
    /// <summary>
    /// Extracts a ZIP file to a destination folder.
    /// </summary>
    /// <param name="zipPath">Path to the ZIP file.</param>
    /// <param name="destinationFolder">Destination folder.</param>
    /// <returns>Path to the extracted folder.</returns>
    public static string ExtractZip(string zipPath, string destinationFolder)
    {
        if (!File.Exists(zipPath))
        {
            throw new FileNotFoundException("ZIP file not found", zipPath);
        }
        
        Directory.CreateDirectory(destinationFolder);
        
        // Get the name of the root folder in the ZIP
        string? rootFolderName = null;
        using (var archive = ZipFile.OpenRead(zipPath))
        {
            var firstEntry = archive.Entries.FirstOrDefault();
            if (firstEntry != null)
            {
                var parts = firstEntry.FullName.Split('/');
                if (parts.Length > 0)
                {
                    rootFolderName = parts[0];
                }
            }
        }
        
        ZipFile.ExtractToDirectory(zipPath, destinationFolder, overwriteFiles: true);
        
        // Return the path to the extracted folder
        if (!string.IsNullOrEmpty(rootFolderName))
        {
            return Path.Combine(destinationFolder, rootFolderName);
        }
        
        return destinationFolder;
    }
    
    /// <summary>
    /// Checks if a path is a folder that should be compressed.
    /// </summary>
    public static bool IsFolder(string path)
    {
        return Directory.Exists(path);
    }
    
    /// <summary>
    /// Checks if a filename indicates it's a compressed folder (ZIP from our app).
    /// </summary>
    public static bool IsCompressedFolder(string fileName)
    {
        return fileName.EndsWith(".folder.zip", StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Gets the folder marker filename for a folder.
    /// </summary>
    public static string GetFolderZipName(string folderPath)
    {
        string folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar));
        return $"{folderName}.folder.zip";
    }
    
    /// <summary>
    /// Gets the original folder name from a folder ZIP filename.
    /// </summary>
    public static string GetOriginalFolderName(string zipFileName)
    {
        if (zipFileName.EndsWith(".folder.zip", StringComparison.OrdinalIgnoreCase))
        {
            return zipFileName[..^".folder.zip".Length];
        }
        return Path.GetFileNameWithoutExtension(zipFileName);
    }
}
