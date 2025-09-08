using MameSelector.Models;

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

        // Step 1: Pre-scan CHD directories to create a lookup dictionary
        Dictionary<string, ChdInfo>? chdLookup = null;
        var totalChdDirectories = 0;
        if (!string.IsNullOrEmpty(chdRepositoryPath) && Directory.Exists(chdRepositoryPath))
        {
            progress?.Report(new ScanProgress { Phase = "Scanning CHD directories...", Percentage = 0 });
            chdLookup = await ScanChdDirectoriesAsync(chdRepositoryPath, progress, cancellationToken);
            totalChdDirectories = chdLookup.Count;
            progress?.Report(new ScanProgress { Phase = $"Found {totalChdDirectories} CHD directories", Percentage = 0 });
        }
        else
        {
            progress?.Report(new ScanProgress { Phase = "CHD scanning skipped (no CHD path)", Percentage = 0 });
        }
        
        // Step 2: Scan ROM files and immediately match with CHDs
        progress?.Report(new ScanProgress { Phase = "Scanning ROM files...", Percentage = 0 });
        await ScanRomFilesAsync(romRepositoryPath, scannedRoms, chdLookup, 
            progress, metadataLookup, cancellationToken);

        // Final progress report with accurate counts
        var finalChdCount = scannedRoms.Values.Count(r => r.ChdFiles?.Count > 0);
        var totalChdFiles = scannedRoms.Values.Sum(r => r.ChdFiles?.Count ?? 0);
        
        // Debug: Log the different counts
        progress?.Report(new ScanProgress { 
            Phase = $"Scan Complete - ROMs with CHDs: {finalChdCount}, Total CHD directories: {totalChdDirectories}, Total CHD files: {totalChdFiles}", 
            Percentage = 100, 
            ItemsProcessed = scannedRoms.Count,
            ChdCount = finalChdCount,
            TotalChdDirectories = totalChdDirectories
        });
        return scannedRoms;
    }


    /// <summary>
    /// Pre-scans CHD directories to create a lookup dictionary
    /// </summary>
    private async Task<Dictionary<string, ChdInfo>> ScanChdDirectoriesAsync(
        string chdPath,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var chdLookup = new Dictionary<string, ChdInfo>(StringComparer.OrdinalIgnoreCase);
        
        // Run directory enumeration on background thread to avoid blocking UI
        var chdDirectories = await Task.Run(() => 
            Directory.EnumerateDirectories(chdPath, "*", SearchOption.TopDirectoryOnly).ToList(), 
            cancellationToken);
        
        var totalDirs = chdDirectories.Count;
        var processedDirs = 0;
        
        progress?.Report(new ScanProgress { Phase = $"Found {totalDirs} CHD directories to scan", Percentage = 0 });
        
        // Debug: Log some sample directory names
        if (totalDirs > 0)
        {
            var sampleDirs = chdDirectories.Take(5).Select(d => Path.GetFileName(d)).ToList();
            progress?.Report(new ScanProgress { Phase = $"Sample CHD dirs: {string.Join(", ", sampleDirs)}", Percentage = 0 });
        }

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
                    var chdInfo = new ChdInfo
                    {
                        ChdFiles = chdFiles.ToList(),
                        TotalSize = chdFiles.Sum(f => new FileInfo(f).Length)
                    };

                    lock (chdLookup)
                    {
                        chdLookup[dirName] = chdInfo;
                    }
                }

                var processed = Interlocked.Increment(ref processedDirs);
                if (processed % 50 == 0 || processed == totalDirs)
                {
                    var percentage = (int)((double)processed / totalDirs * 100);
                    progress?.Report(new ScanProgress 
                    { 
                        Phase = $"Scanning CHD directories... ({processed}/{totalDirs})", 
                        Percentage = percentage,
                        ItemsProcessed = processed
                    });
                }
            });
        });

        progress?.Report(new ScanProgress { Phase = $"CHD directory scan complete: {chdLookup.Count} directories with CHD files", Percentage = 100 });
        return chdLookup;
    }

    /// <summary>
    /// Scans ROM archive files in the repository
    /// </summary>
    private async Task ScanRomFilesAsync(
        string romPath, 
        Dictionary<string, ScannedRom> scannedRoms,
        Dictionary<string, ChdInfo>? chdLookup,
        IProgress<ScanProgress>? progress,
        Dictionary<string, MameGame>? metadataLookup,
        CancellationToken cancellationToken)
    {
        progress?.Report(new ScanProgress { Phase = "Listing ROM files...", Percentage = 0 });

        // Use DirectoryInfo.GetFiles() to get FileInfo objects directly - much more efficient!
        // Run this on a background thread to avoid blocking the UI
        var romFileInfos = await Task.Run(() =>
        {
            var directoryInfo = new DirectoryInfo(romPath);
            return directoryInfo.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                .Where(f => _validRomExtensions.Contains(f.Extension))
                .ToList();
        }, cancellationToken);

        progress?.Report(new ScanProgress { Phase = "Scanning ROM files...", Percentage = 0 });

        var totalFiles = romFileInfos.Count;

        // Process ROMs in background - no real-time UI updates needed since scanning is fast
        const int batchSize = 100; // Smaller batches for progress reporting
        
        // Process ROMs in background
        await Task.Run(() =>
        {
            for (int i = 0; i < romFileInfos.Count; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var batch = romFileInfos.Skip(i).Take(batchSize);
                var batchResults = new List<ScannedRom>(batchSize);
                
                // Process batch in parallel
                Parallel.ForEach(batch, new ParallelOptions 
                { 
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                }, fileInfo =>
                {
                    var romName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                    
                    var scannedRom = new ScannedRom
                    {
                        Name = romName,
                        RomFilePath = fileInfo.FullName,
                        RomFileSize = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime
                    };

                    // Immediately match CHD files if available
                    if (chdLookup != null && chdLookup.TryGetValue(romName, out var chdInfo))
                    {
                        scannedRom.ChdFiles = chdInfo.ChdFiles;
                        scannedRom.TotalChdSize = chdInfo.TotalSize;
                    }

                    // Match metadata in real-time if lookup is available
                    if (metadataLookup != null)
                    {
                        MatchMetadata(scannedRom, metadataLookup);
                    }

                    // Use thread-local collection to reduce lock contention
                    lock (batchResults)
                    {
                        batchResults.Add(scannedRom);
                    }
                });
                
                // Add all batch results to main collection in one lock operation
                lock (scannedRoms)
                {
                    foreach (var rom in batchResults)
                    {
                        scannedRoms[rom.Name] = rom;
                    }
                }
                
                // Update progress every batch
                var processed = Math.Min(i + batchSize, romFileInfos.Count);
                var percentage = (int)((double)processed / totalFiles * 100);
                progress?.Report(new ScanProgress 
                { 
                    Phase = $"Scanning ROM files... ({processed}/{totalFiles})", 
                    Percentage = percentage,
                    ItemsProcessed = processed
                });
            }
        }, cancellationToken);
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
    /// Matches metadata for a scanned ROM using exact matching only
    /// </summary>
    private void MatchMetadata(ScannedRom scannedRom, Dictionary<string, MameGame> metadataLookup)
    {
        // Only try exact match - XML should contain exact matches only
        if (metadataLookup.TryGetValue(scannedRom.Name, out var metadata))
        {
            scannedRom.Metadata = metadata;
        }
    }


    /// <summary>
    /// Scans the destination directory and marks ROMs as being in destination
    /// </summary>
    /// <param name="destinationPath">Path to destination directory</param>
    /// <param name="roms">Dictionary of ROMs to update</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of ROMs found in destination</returns>
    public Task<int> ScanDestinationAsync(string destinationPath, Dictionary<string, ScannedRom> roms, 
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(destinationPath) || !Directory.Exists(destinationPath))
        {
            return Task.FromResult(0);
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
            return Task.FromResult(destinationRoms.Count);
        }
        catch (Exception ex)
        {
            progress?.Report($"Error scanning destination: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Information about CHD files for a ROM
/// </summary>
public class ChdInfo
{
    public List<string> ChdFiles { get; set; } = new();
    public long TotalSize { get; set; }
}

/// <summary>
/// Progress information for ROM scanning
/// </summary>
public class ScanProgress
{
    public string Phase { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public int ItemsProcessed { get; set; }
    public int ChdCount { get; set; }
    public int TotalChdDirectories { get; set; }
}
