namespace MameSelector.Models;

/// <summary>
/// Represents a ROM that was found during directory scanning
/// </summary>
public class ScannedRom
{
    public string Name { get; set; } = string.Empty;
    public string? RomFilePath { get; set; }
    public long RomFileSize { get; set; }
    public List<string> ChdFiles { get; set; } = new();
    public long TotalChdSize { get; set; }
    public DateTime LastModified { get; set; }
    
    /// <summary>
    /// Whether this ROM exists in the destination directory
    /// </summary>
    public bool InDestination { get; set; }
    
    /// <summary>
    /// Whether this ROM is currently selected in the UI
    /// </summary>
    public bool IsSelected { get; set; }
    
    /// <summary>
    /// Associated MAME metadata (populated after XML matching)
    /// </summary>
    public MameGame? Metadata { get; set; }
    
    /// <summary>
    /// Whether this ROM has associated metadata from MAME XML
    /// </summary>
    public bool HasMetadata => Metadata != null;
    
    /// <summary>
    /// Whether this ROM has CHD files
    /// </summary>
    public bool HasChd => ChdFiles.Any();
    
    /// <summary>
    /// Whether this ROM has a ROM archive file
    /// </summary>
    public bool HasRomFile => !string.IsNullOrEmpty(RomFilePath);
    
    /// <summary>
    /// Total size of all files (ROM + CHD)
    /// </summary>
    public long TotalSize => RomFileSize + TotalChdSize;
    
    /// <summary>
    /// Display name with fallback to filename if no metadata
    /// </summary>
    public string DisplayName => Metadata?.Description ?? Name;
    
    /// <summary>
    /// Display manufacturer with fallback
    /// </summary>
    public string DisplayManufacturer => Metadata?.Manufacturer ?? "Unknown";
    
    /// <summary>
    /// Display year with fallback
    /// </summary>
    public string DisplayYear => Metadata?.Year ?? "Unknown";
    
    /// <summary>
    /// Whether this ROM is a clone (has metadata and is clone)
    /// </summary>
    public bool IsClone => Metadata?.IsClone ?? false;
    
    /// <summary>
    /// Whether this ROM is a BIOS file (has metadata and is BIOS)
    /// </summary>
    public bool IsBios => Metadata?.IsBios ?? false;
    
    /// <summary>
    /// Whether this ROM is a device file (has metadata and is device)
    /// </summary>
    public bool IsDevice => Metadata?.IsDevice ?? false;
}
