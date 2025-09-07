using MameSelector.Models;
using System.IO.Compression;

namespace MameSelector.Services;

/// <summary>
/// Service for scanning ROM directories and detecting available ROM files
/// </summary>
public class RomScanner
{
    private readonly HashSet<string> _validRomExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar"
    };

    private readonly HashSet<string> _validChdExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".chd"
    };

    /// <summary>
    /// Scans the ROM repository for available ROM files
    /// </summary>
    /// <param name="romRepositoryPath">Path to ROM repository</param>
    /// <param name="chdRepositoryPath">Optional path to CHD repository</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="romFoundCallback">Callback for each ROM found (for real-time updates)</param>
    /// <param name="metadataLookup">Optional metadata lookup dictionary for real-time matching</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of ROM name to ScannedRom</returns>
    public async Task<Dictionary<string, ScannedRom>> ScanRomsAsync(
        string romRepositoryPath, 
        string? chdRepositoryPath = null,
        IProgress<ScanProgress>? progress = null,
        Action<ScannedRom>? romFoundCallback = null,
        Dictionary<string, MameGame>? metadataLookup = null,
        CancellationToken cancellationToken = default)
    {
        var scannedRoms = new Dictionary<string, ScannedRom>(StringComparer.OrdinalIgnoreCase);
        
        if (!Directory.Exists(romRepositoryPath))
            throw new DirectoryNotFoundException($"ROM repository not found: {romRepositoryPath}");

        // Scan ROM files
        await ScanRomFilesAsync(romRepositoryPath, scannedRoms, progress, romFoundCallback, metadataLookup, cancellationToken);
        
        // Scan CHD files if path provided
        if (!string.IsNullOrEmpty(chdRepositoryPath) && Directory.Exists(chdRepositoryPath))
        {
            await ScanChdFilesAsync(chdRepositoryPath, scannedRoms, progress, romFoundCallback, metadataLookup, cancellationToken);
        }

        progress?.Report(new ScanProgress { Phase = "Scan Complete", Percentage = 100, ItemsProcessed = scannedRoms.Count });
        return scannedRoms;
    }

    /// <summary>
    /// Scans ROM archive files in the repository
    /// </summary>
    private async Task ScanRomFilesAsync(
        string romPath, 
        Dictionary<string, ScannedRom> scannedRoms,
        IProgress<ScanProgress>? progress,
        Action<ScannedRom>? romFoundCallback,
        Dictionary<string, MameGame>? metadataLookup,
        CancellationToken cancellationToken)
    {
        progress?.Report(new ScanProgress { Phase = "Scanning ROM files...", Percentage = 0 });

        var romFiles = Directory.EnumerateFiles(romPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => _validRomExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var totalFiles = romFiles.Count;
        var processedFiles = 0;

        await Task.Run(() =>
        {
            Parallel.ForEach(romFiles, new ParallelOptions 
            { 
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            }, romFile =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var romName = Path.GetFileNameWithoutExtension(romFile);
                var fileInfo = new FileInfo(romFile);
                
                // Debug output for every ROM found
                Console.WriteLine($"ROM SCANNER DEBUG: Found ROM file: {romName} at {romFile}");
                
                var scannedRom = new ScannedRom
                {
                    Name = romName,
                    RomFilePath = romFile,
                    RomFileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime
                };

                // Try to get internal file list for ZIP files (optional, for detailed info)
                if (Path.GetExtension(romFile).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        scannedRom.InternalFiles = GetZipFileList(romFile);
                    }
                    catch
                    {
                        // If we can't read the ZIP, just continue - the ROM might still be valid
                        scannedRom.InternalFiles = new List<string>();
                    }
                }

                // Match metadata in real-time if lookup is available
                if (metadataLookup != null)
                {
                    MatchMetadata(scannedRom, metadataLookup);
                    
                    // Debug output for metadata matching result
                    Console.WriteLine($"ROM SCANNER DEBUG: {romName} metadata match result: {(scannedRom.HasMetadata ? "SUCCESS" : "FAILED")}");
                    if (scannedRom.HasMetadata)
                    {
                        Console.WriteLine($"  Description: '{scannedRom.Metadata?.Description}'");
                        Console.WriteLine($"  Year: '{scannedRom.Metadata?.Year}'");
                    }
                }

                lock (scannedRoms)
                {
                    scannedRoms[romName] = scannedRom;
                }

                // Notify callback for real-time updates
                romFoundCallback?.Invoke(scannedRom);

                var processed = Interlocked.Increment(ref processedFiles);
                if (processed % 100 == 0 || processed == totalFiles) // Update progress every 100 files
                {
                    var percentage = (int)((double)processed / totalFiles * 50); // ROM scanning is 50% of total
                    progress?.Report(new ScanProgress 
                    { 
                        Phase = $"Scanning ROM files... ({processed}/{totalFiles})", 
                        Percentage = percentage,
                        ItemsProcessed = processed
                    });
                }
            });
        });
    }

    /// <summary>
    /// Scans CHD files in the repository
    /// </summary>
    private async Task ScanChdFilesAsync(
        string chdPath,
        Dictionary<string, ScannedRom> scannedRoms,
        IProgress<ScanProgress>? progress,
        Action<ScannedRom>? romFoundCallback,
        Dictionary<string, MameGame>? metadataLookup,
        CancellationToken cancellationToken)
    {
        progress?.Report(new ScanProgress { Phase = "Scanning CHD files...", Percentage = 50 });

        var chdDirectories = Directory.EnumerateDirectories(chdPath, "*", SearchOption.TopDirectoryOnly).ToList();
        var totalDirs = chdDirectories.Count;
        var processedDirs = 0;

        await Task.Run(() =>
        {
            Parallel.ForEach(chdDirectories, new ParallelOptions 
            { 
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount / 2 // CHD scanning is I/O intensive
            }, chdDir =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dirName = Path.GetFileName(chdDir);
                var chdFiles = Directory.GetFiles(chdDir, "*.chd", SearchOption.AllDirectories);

                if (chdFiles.Length > 0)
                {
                    lock (scannedRoms)
                    {
                        if (scannedRoms.TryGetValue(dirName, out var existingRom))
                        {
                            // Add CHD info to existing ROM
                            existingRom.ChdFiles = chdFiles.ToList();
                            existingRom.TotalChdSize = chdFiles.Sum(f => new FileInfo(f).Length);

                            // Match metadata in real-time if lookup is available
                            if (metadataLookup != null)
                            {
                                MatchMetadata(existingRom, metadataLookup);
                            }

                            // Notify callback for ROM update (CHD added)
                            romFoundCallback?.Invoke(existingRom);
                        }
                        // If no existing ROM file found, we don't create CHD-only entries
                        // CHDs without corresponding ROM files are ignored
                    }
                }

                var processed = Interlocked.Increment(ref processedDirs);
                if (processed % 50 == 0 || processed == totalDirs) // Update progress every 50 directories
                {
                    var percentage = 50 + (int)((double)processed / totalDirs * 50); // CHD scanning is remaining 50%
                    progress?.Report(new ScanProgress 
                    { 
                        Phase = $"Scanning CHD files... ({processed}/{totalDirs})", 
                        Percentage = percentage,
                        ItemsProcessed = processed
                    });
                }
            });
        });
    }

    /// <summary>
    /// Gets the list of files inside a ZIP archive
    /// </summary>
    private List<string> GetZipFileList(string zipPath)
    {
        var files = new List<string>();
        
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            files.AddRange(archive.Entries.Select(entry => entry.Name));
        }
        catch
        {
            // Return empty list if ZIP can't be read
        }
        
        return files;
    }

    /// <summary>
    /// Quick scan to get ROM count without full details (for progress estimation)
    /// </summary>
    public async Task<int> GetRomCountAsync(string romRepositoryPath, string? chdRepositoryPath = null)
    {
        var tasks = new List<Task<int>>();

        // Count ROM files
        if (Directory.Exists(romRepositoryPath))
        {
            tasks.Add(Task.Run(() => Directory.EnumerateFiles(romRepositoryPath, "*.*", SearchOption.TopDirectoryOnly)
                .Count(f => _validRomExtensions.Contains(Path.GetExtension(f)))));
        }

        // Count CHD directories
        if (!string.IsNullOrEmpty(chdRepositoryPath) && Directory.Exists(chdRepositoryPath))
        {
            tasks.Add(Task.Run(() => Directory.EnumerateDirectories(chdRepositoryPath, "*", SearchOption.TopDirectoryOnly)
                .Count(d => Directory.GetFiles(d, "*.chd", SearchOption.AllDirectories).Length > 0)));
        }

        var counts = await Task.WhenAll(tasks);
        return counts.Sum();
    }

    /// <summary>
    /// Matches metadata for a scanned ROM using fuzzy matching logic
    /// </summary>
    private void MatchMetadata(ScannedRom scannedRom, Dictionary<string, MameGame> metadataLookup)
    {
        // Try exact match first
        if (metadataLookup.TryGetValue(scannedRom.Name, out var metadata))
        {
            scannedRom.Metadata = metadata;
            return;
        }

        // Try fuzzy matching for common naming variations
        var fuzzyMatch = FindFuzzyMatch(scannedRom.Name, metadataLookup);
        if (fuzzyMatch != null)
        {
            scannedRom.Metadata = fuzzyMatch;
        }
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
    /// Scans the destination directory and marks ROMs as being in destination
    /// </summary>
    /// <param name="destinationPath">Path to destination directory</param>
    /// <param name="roms">Dictionary of ROMs to update</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of ROMs found in destination</returns>
    public async Task<int> ScanDestinationAsync(string destinationPath, Dictionary<string, ScannedRom> roms, 
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(destinationPath) || !Directory.Exists(destinationPath))
        {
            return 0;
        }

        progress?.Report("Scanning destination directory...");

        var destinationRoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allExtensions = _validRomExtensions.Concat(_validChdExtensions).ToHashSet();

        try
        {
            var files = Directory.GetFiles(destinationPath, "*.*", SearchOption.AllDirectories)
                .Where(f => allExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = Path.GetFileNameWithoutExtension(file);
                var extension = Path.GetExtension(file).ToLowerInvariant();

                if (_validRomExtensions.Contains(extension))
                {
                    // ROM file - add the ROM name
                    destinationRoms.Add(fileName);
                }
                else if (_validChdExtensions.Contains(extension))
                {
                    // CHD file - determine ROM name from folder structure
                    var directoryName = Path.GetDirectoryName(file);
                    if (!string.IsNullOrEmpty(directoryName))
                    {
                        var folderName = Path.GetFileName(directoryName);
                        // If CHD is in a subfolder, use the folder name as the ROM name
                        if (directoryName != destinationPath)
                        {
                            destinationRoms.Add(folderName);
                        }
                        else
                        {
                            // CHD is in root directory, use filename
                            destinationRoms.Add(fileName);
                        }
                    }
                }

                if (destinationRoms.Count % 100 == 0)
                {
                    progress?.Report($"Found {destinationRoms.Count} ROMs in destination...");
                }
            }

            // First, clear all InDestination flags (ROMs may have been removed)
            foreach (var rom in roms.Values)
            {
                rom.InDestination = false;
            }

            // Then mark ROMs as being in destination
            int markedCount = 0;
            foreach (var romName in destinationRoms)
            {
                if (roms.TryGetValue(romName, out var rom))
                {
                    rom.InDestination = true;
                    markedCount++;
                }
            }

            progress?.Report($"Marked {markedCount} ROMs as being in destination");
            return destinationRoms.Count;
        }
        catch (Exception ex)
        {
            progress?.Report($"Error scanning destination: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Progress information for ROM scanning
/// </summary>
public class ScanProgress
{
    public string Phase { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public int ItemsProcessed { get; set; }
}
