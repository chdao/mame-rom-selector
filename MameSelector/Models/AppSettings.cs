namespace MameSelector.Models;

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
    public bool CreateSubfolders { get; set; } = false;
    public bool VerifyChecksums { get; set; } = false;
    
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
