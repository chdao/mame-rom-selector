namespace MameSelector.Models;

/// <summary>
/// Represents a MAME game entry from the XML file
/// </summary>
public class MameGame
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string CloneOf { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsBios { get; set; } = false;
    public bool IsDevice { get; set; } = false;
    public bool IsClone => !string.IsNullOrEmpty(CloneOf);
    public List<RomFile> RomFiles { get; set; } = new();
    public List<DiskFile> DiskFiles { get; set; } = new();
    public bool HasCHD => DiskFiles.Any();
    
    /// <summary>
    /// Gets the total size of all ROM files in bytes
    /// </summary>
    public long TotalRomSize => RomFiles.Sum(r => r.Size);
    
    /// <summary>
    /// Gets the total size of all disk files in bytes (estimated)
    /// </summary>
    public long TotalDiskSize => DiskFiles.Sum(d => d.EstimatedSize);
}

/// <summary>
/// Represents a ROM file within a MAME game
/// </summary>
public class RomFile
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string CRC { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
}

/// <summary>
/// Represents a CHD disk file within a MAME game
/// </summary>
public class DiskFile
{
    public string Name { get; set; } = string.Empty;
    public string SHA1 { get; set; } = string.Empty;
    public string MD5 { get; set; } = string.Empty;
    public long EstimatedSize { get; set; } = 700_000_000; // Default ~700MB estimate for CHD files
}

