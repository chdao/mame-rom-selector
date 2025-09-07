using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Service for matching scanned ROMs with MAME XML metadata
/// </summary>
public class RomMetadataService
{
    private readonly MameXmlParser _xmlParser;
    private Dictionary<string, MameGame>? _mameGamesCache;

    public RomMetadataService(MameXmlParser xmlParser)
    {
        _xmlParser = xmlParser;
    }

    /// <summary>
    /// Loads MAME XML data and matches it with scanned ROMs
    /// </summary>
    /// <param name="xmlFilePath">Path to MAME XML file</param>
    /// <param name="scannedRoms">Dictionary of scanned ROMs</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="romUpdatedCallback">Callback for each ROM updated with metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated scanned ROMs with metadata</returns>
    public async Task<Dictionary<string, ScannedRom>> LoadAndMatchMetadataAsync(
        string xmlFilePath,
        Dictionary<string, ScannedRom> scannedRoms,
        IProgress<MetadataProgress>? progress = null,
        Action<ScannedRom>? romUpdatedCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Load MAME XML data if not cached
        if (_mameGamesCache == null)
        {
            progress?.Report(new MetadataProgress { Phase = "Loading MAME XML...", Percentage = 0 });
            
            var xmlProgress = new Progress<int>(percentage =>
                progress?.Report(new MetadataProgress 
                { 
                    Phase = "Loading MAME XML...", 
                    Percentage = percentage / 2 // XML loading is 50% of the process
                }));

            var mameGames = await _xmlParser.ParseAsync(xmlFilePath, xmlProgress, cancellationToken);
            _mameGamesCache = mameGames.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);
            
            
        }

        // Match scanned ROMs with metadata
        progress?.Report(new MetadataProgress { Phase = "Matching ROM metadata...", Percentage = 50 });
        
        await Task.Run(() =>
        {
            var totalRoms = scannedRoms.Count;
            var processedRoms = 0;

            Parallel.ForEach(scannedRoms.Values, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, scannedRom =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hadMetadata = scannedRom.HasMetadata;
                
                // Try exact match first
                if (_mameGamesCache.TryGetValue(scannedRom.Name, out var metadata))
                {
                    scannedRom.Metadata = metadata;
                    
                }
                else
                {
                    
                    // Try fuzzy matching for common naming variations
                    var fuzzyMatch = FindFuzzyMatch(scannedRom.Name, _mameGamesCache);
                    if (fuzzyMatch != null)
                    {
                        scannedRom.Metadata = fuzzyMatch;
                    }
                }

                // Notify callback if metadata was added
                if (!hadMetadata && scannedRom.HasMetadata)
                {
                    romUpdatedCallback?.Invoke(scannedRom);
                }

                var processed = Interlocked.Increment(ref processedRoms);
                if (processed % 1000 == 0 || processed == totalRoms) // Update every 1000 ROMs
                {
                    var percentage = 50 + (int)((double)processed / totalRoms * 50);
                    progress?.Report(new MetadataProgress
                    {
                        Phase = $"Matching ROM metadata... ({processed}/{totalRoms})",
                        Percentage = percentage,
                        MatchedRoms = scannedRoms.Values.Count(r => r.HasMetadata)
                    });
                }
            });
        });

        var finalMatchedCount = scannedRoms.Values.Count(r => r.HasMetadata);
        progress?.Report(new MetadataProgress 
        { 
            Phase = "Metadata matching complete", 
            Percentage = 100, 
            MatchedRoms = finalMatchedCount 
        });

        return scannedRoms;
    }

    /// <summary>
    /// Attempts fuzzy matching for ROM names with common variations
    /// </summary>
    private MameGame? FindFuzzyMatch(string romName, Dictionary<string, MameGame> mameGames)
    {
        // Common ROM name variations to try
        var variations = new[]
        {
            romName.Replace("_", ""),           // Remove underscores
            romName.Replace("-", ""),           // Remove hyphens
            romName.ToLowerInvariant(),         // Lowercase
            romName.ToUpperInvariant(),         // Uppercase
            RemoveRegionCodes(romName),         // Remove region codes like (USA), [!], etc.
            RemoveVersionNumbers(romName)       // Remove version numbers like v1.1, rev1, etc.
        };

        foreach (var variation in variations.Where(v => !string.IsNullOrEmpty(v)))
        {
            if (mameGames.TryGetValue(variation, out var match))
            {
                return match;
            }
        }

        // Disable partial matching to prevent incorrect matches
        // If exact match and variations don't work, return null
        return null;
    }

    /// <summary>
    /// Removes common region codes from ROM names
    /// </summary>
    private string RemoveRegionCodes(string romName)
    {
        var patterns = new[]
        {
            @"\(USA\)", @"\(Europe\)", @"\(Japan\)", @"\(World\)",
            @"\[!\]", @"\[a\d*\]", @"\[b\d*\]", @"\[f\d*\]",
            @"\(Rev \d+\)", @"\(v\d+\.\d+\)"
        };

        var result = romName;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }

        return result;
    }

    /// <summary>
    /// Removes version numbers from ROM names
    /// </summary>
    private string RemoveVersionNumbers(string romName)
    {
        var patterns = new[]
        {
            @"v\d+\.\d+", @"rev\d+", @"r\d+", @"\d+\.\d+$"
        };

        var result = romName;
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, "", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }

        return result;
    }

    /// <summary>
    /// Gets statistics about the metadata matching
    /// </summary>
    public MetadataStats GetMatchingStats(Dictionary<string, ScannedRom> scannedRoms)
    {
        var totalRoms = scannedRoms.Count;
        var matchedRoms = scannedRoms.Values.Count(r => r.HasMetadata);
        var romsWithChd = scannedRoms.Values.Count(r => r.HasChd);
        var cloneRoms = scannedRoms.Values.Count(r => r.IsClone);

        return new MetadataStats
        {
            TotalScannedRoms = totalRoms,
            MatchedRoms = matchedRoms,
            UnmatchedRoms = totalRoms - matchedRoms,
            RomsWithChd = romsWithChd,
            CloneRoms = cloneRoms,
            MatchPercentage = totalRoms > 0 ? (double)matchedRoms / totalRoms * 100 : 0
        };
    }

    /// <summary>
    /// Clears the cached MAME XML data (useful when switching XML files)
    /// </summary>
    public void ClearCache()
    {
        _mameGamesCache = null;
    }
}

/// <summary>
/// Progress information for metadata matching
/// </summary>
public class MetadataProgress
{
    public string Phase { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public int MatchedRoms { get; set; }
}

/// <summary>
/// Statistics about metadata matching results
/// </summary>
public class MetadataStats
{
    public int TotalScannedRoms { get; set; }
    public int MatchedRoms { get; set; }
    public int UnmatchedRoms { get; set; }
    public int RomsWithChd { get; set; }
    public int CloneRoms { get; set; }
    public double MatchPercentage { get; set; }
}
