namespace MameSelector.Models;

/// <summary>
/// Verbosity levels for debug panel output
/// </summary>
public enum VerbosityLevel
{
    Minimal = 0,    // Only essential messages
    Normal = 1,     // Standard messages (default)
    Verbose = 2,    // Include debug messages
    Debug = 3       // All messages including detailed debug info
}

/// <summary>
/// Log levels for different types of messages
/// </summary>
public enum LogLevel
{
    Error = 0,      // Critical errors that should always be shown
    Warning = 1,    // Warnings that should be shown in Normal+ verbosity
    Info = 2,       // General information messages
    Debug = 3       // Detailed debug information
}

/// <summary>
/// ROMset format types supported by the application
/// </summary>
public enum RomsetType
{
    NonMerged,      // Each ROM file contains all required files for that game
    Merged,         // ROM files depend on parent ROMs (clones reference parents)
    Split           // ROM files split across parent/child relationships
}

/// <summary>
/// Application settings for the MAME ROM Selector
/// </summary>
public class AppSettings
{
    public string MameXmlPath { get; set; } = string.Empty;
    public string RomRepositoryPath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string CHDRepositoryPath { get; set; } = string.Empty;
    public bool CopyBiosFiles { get; set; } = true;
    public bool CopyDeviceFiles { get; set; } = true;
    public bool ShowBiosAndDevices { get; set; } = true; // Show BIOS and device files in ROM Collection by default
    public bool CreateSubfolders { get; set; } = false;
    public bool VerifyChecksums { get; set; } = false;
    
    // Portable mode - when true, cache and settings are stored alongside the executable
    public bool PortableMode { get; set; } = true; // Default to portable for better user experience
    
    // Debug panel verbosity level
    public VerbosityLevel ConsoleVerbosity { get; set; } = VerbosityLevel.Normal; // Default to normal verbosity
    
    // ROMset format configuration
    public RomsetType RomsetType { get; set; } = RomsetType.NonMerged; // Default to non-merged for backward compatibility
    public bool AutoDetectRomsetType { get; set; } = true; // Automatically detect ROMset type during scanning
    public bool AutoCopyDependencies { get; set; } = true; // Automatically copy parent ROMs when copying clones
    public bool ValidateMergedIntegrity { get; set; } = true; // Validate CRC integrity across parent-child relationships
    
    // Window state persistence
    public int WindowWidth { get; set; } = 1600;
    public int WindowHeight { get; set; } = 900;
    public int WindowX { get; set; } = -1; // -1 means center on screen
    public int WindowY { get; set; } = -1; // -1 means center on screen
    public int SplitterDistance { get; set; } = 720;
    
    /// <summary>
    /// Validates that all required paths are set and exist
    /// </summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(MameXmlPath))
            errors.Add("MAME XML file path is required");
        else if (!File.Exists(MameXmlPath))
            errors.Add($"MAME XML file not found: {MameXmlPath}");
            
        if (string.IsNullOrWhiteSpace(RomRepositoryPath))
            errors.Add("ROM repository path is required");
        else if (!Directory.Exists(RomRepositoryPath))
            errors.Add($"ROM repository directory not found: {RomRepositoryPath}");
            
        if (string.IsNullOrWhiteSpace(DestinationPath))
            errors.Add("Destination path is required");
            
        if (!string.IsNullOrWhiteSpace(CHDRepositoryPath) && !Directory.Exists(CHDRepositoryPath))
            errors.Add($"CHD repository directory not found: {CHDRepositoryPath}");
            
        return errors;
    }
}
