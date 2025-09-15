using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Service for detecting ROMset type (merged, non-merged, split)
/// </summary>
public class RomsetDetector
{
    private readonly LoggingService? _loggingService;

    public RomsetDetector(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    /// <summary>
    /// Detects the ROMset type by analyzing the ROM files and MAME XML metadata
    /// </summary>
    /// <param name="romRepositoryPath">Path to ROM repository</param>
    /// <param name="mameGames">Dictionary of MAME games from XML</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detected ROMset type</returns>
    public async Task<RomsetType> DetectRomsetTypeAsync(
        string romRepositoryPath,
        Dictionary<string, MameGame> mameGames,
        CancellationToken cancellationToken = default)
    {
        _loggingService?.LogInfo("Starting ROMset type detection...");

        try
        {
            // Get all ROM files in the repository
            var romFiles = await GetRomFilesAsync(romRepositoryPath, cancellationToken);
            
            if (!romFiles.Any())
            {
                _loggingService?.LogWarning("No ROM files found in repository");
                return RomsetType.NonMerged; // Default fallback
            }

            // Analyze ROM files against MAME metadata
            var analysis = await AnalyzeRomsetStructureAsync(romFiles, mameGames, cancellationToken);
            
            var detectedType = DetermineRomsetType(analysis);
            
            _loggingService?.LogInfo($"ROMset type detected: {detectedType}");
            _loggingService?.LogInfo($"Analysis: {analysis.TotalRoms} ROMs, {analysis.ClonesFound} clones, {analysis.ParentsFound} parents");
            
            return detectedType;
        }
        catch (Exception ex)
        {
            _loggingService?.LogError($"Error detecting ROMset type: {ex.Message}");
            return RomsetType.NonMerged; // Safe fallback
        }
    }

    /// <summary>
    /// Gets all ROM files from the repository
    /// </summary>
    private async Task<List<string>> GetRomFilesAsync(string romRepositoryPath, CancellationToken cancellationToken)
    {
        var validExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".7z", ".rar"
        };

        return await Task.Run(() =>
        {
            try
            {
                return Directory.GetFiles(romRepositoryPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(file => validExtensions.Contains(Path.GetExtension(file)))
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Cast<string>()
                    .ToList();
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"Error scanning ROM files: {ex.Message}");
                return new List<string>();
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Analyzes the ROMset structure to determine its type
    /// </summary>
    private async Task<RomsetAnalysis> AnalyzeRomsetStructureAsync(
        List<string> romFiles,
        Dictionary<string, MameGame> mameGames,
        CancellationToken cancellationToken)
    {
        var analysis = new RomsetAnalysis();

        await Task.Run(() =>
        {
            var romFileSet = new HashSet<string>(romFiles, StringComparer.OrdinalIgnoreCase);

            foreach (var game in mameGames.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if this game's ROM file exists
                bool hasRomFile = romFileSet.Contains(game.Name);
                
                if (hasRomFile)
                {
                    analysis.TotalRoms++;
                    
                    if (game.IsClone)
                    {
                        analysis.ClonesFound++;
                        
                        // Check if parent ROM exists
                        if (romFileSet.Contains(game.CloneOf))
                        {
                            analysis.ClonesWithParents++;
                        }
                        else
                        {
                            analysis.ClonesWithoutParents++;
                        }
                    }
                    else
                    {
                        analysis.ParentsFound++;
                    }
                }
            }
        }, cancellationToken);

        return analysis;
    }

    /// <summary>
    /// Determines ROMset type based on analysis results
    /// </summary>
    private RomsetType DetermineRomsetType(RomsetAnalysis analysis)
    {
        // If no clones found, it's likely non-merged
        if (analysis.ClonesFound == 0)
        {
            return RomsetType.NonMerged;
        }

        // Calculate clone-to-parent ratio
        var cloneParentRatio = analysis.ParentsFound > 0 ? (double)analysis.ClonesFound / analysis.ParentsFound : 0;
        
        // If most clones have their parents available, it's likely merged
        var clonesWithParentsRatio = analysis.ClonesFound > 0 ? (double)analysis.ClonesWithParents / analysis.ClonesFound : 0;

        // Decision logic:
        if (clonesWithParentsRatio >= 0.8) // 80% or more clones have parents
        {
            return RomsetType.Merged;
        }
        else if (clonesWithParentsRatio >= 0.3) // 30-80% clones have parents
        {
            // Could be split or partially merged
            return cloneParentRatio > 3 ? RomsetType.Split : RomsetType.Merged;
        }
        else // Less than 30% clones have parents
        {
            return RomsetType.NonMerged;
        }
    }
}

/// <summary>
/// Analysis results for ROMset structure
/// </summary>
public class RomsetAnalysis
{
    public int TotalRoms { get; set; }
    public int ClonesFound { get; set; }
    public int ParentsFound { get; set; }
    public int ClonesWithParents { get; set; }
    public int ClonesWithoutParents { get; set; }
}
