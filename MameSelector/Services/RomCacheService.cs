using System.Text.Json;
using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Service for caching scanned ROM data to avoid re-scanning on every startup
/// </summary>
public class RomCacheService
{
    private string _cacheFilePath;
    private const string CacheFileName = "rom_cache.json";
    private const int CacheVersion = 1;

    public RomCacheService()
    {
        // Default to portable mode - will be updated when settings are loaded
        _cacheFilePath = GetCacheFilePath(true);
    }

    /// <summary>
    /// Updates the cache file path based on portable mode setting
    /// </summary>
    public void UpdateCachePath(bool portableMode)
    {
        _cacheFilePath = GetCacheFilePath(portableMode);
    }

    /// <summary>
    /// Gets the cache file path based on portable mode
    /// </summary>
    private string GetCacheFilePath(bool portableMode)
    {
        if (portableMode)
        {
            // Store alongside the executable
            var exeDir = AppContext.BaseDirectory;
            return Path.Combine(exeDir, CacheFileName);
        }
        else
        {
            // Store in AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MameSelector");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, CacheFileName);
        }
    }

    /// <summary>
    /// Saves the scanned ROM data to cache
    /// </summary>
    public async Task SaveCacheAsync(Dictionary<string, ScannedRom> scannedRoms, AppSettings settings, int totalChdDirectories = 0, IProgress<string>? progress = null)
    {
        try
        {
            progress?.Report("Preparing cache data... (85%)");
            
            var cacheData = new RomCacheData
            {
                Version = CacheVersion,
                CreatedAt = DateTime.UtcNow,
                RomRepositoryPath = settings.RomRepositoryPath,
                CHDRepositoryPath = settings.CHDRepositoryPath,
                MameXmlPath = settings.MameXmlPath,
                RomRepositoryHash = string.Empty, // Skip hash calculation for speed
                CHDRepositoryHash = null, // Skip hash calculation for speed
                MameXmlHash = null, // Skip hash calculation for speed
                ScannedRoms = scannedRoms,
                TotalChdDirectories = totalChdDirectories
            };

            progress?.Report("Serializing cache data... (90%)");
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = false, // Compact for performance
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Run serialization in background thread to avoid blocking UI
            var json = await Task.Run(() => JsonSerializer.Serialize(cacheData, options));
            
            progress?.Report("Writing cache file... (95%)");
            await File.WriteAllTextAsync(_cacheFilePath, json);
            
            progress?.Report("Cache saved successfully (100%)");
        }
        catch (Exception)
        {
            // Log error but don't throw - caching is optional
        }
    }

    /// <summary>
    /// Gets the total CHD directory count from the cache
    /// </summary>
    public async Task<int> GetTotalChdDirectoriesFromCacheAsync()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
            {
                Console.WriteLine($"DEBUG: Cache file does not exist: {_cacheFilePath}");
                return 0;
            }

            var json = await File.ReadAllTextAsync(_cacheFilePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var cacheData = JsonSerializer.Deserialize<RomCacheData>(json, options);
            var result = cacheData?.TotalChdDirectories ?? 0;
            Console.WriteLine($"DEBUG: Retrieved TotalChdDirectories from cache: {result}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error retrieving TotalChdDirectories from cache: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Loads cached ROM data if it's still valid
    /// </summary>
    public async Task<Dictionary<string, ScannedRom>?> LoadCacheAsync(AppSettings settings, IProgress<int>? progress = null, Action<ScannedRom>? romLoadedCallback = null)
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
                return null;

            var json = await File.ReadAllTextAsync(_cacheFilePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var cacheData = JsonSerializer.Deserialize<RomCacheData>(json, options);
            if (cacheData == null)
                return null;

            // Validate cache
            progress?.Report(10);
            var validationResult = await ValidateCacheAsync(cacheData, settings);
            if (!validationResult.IsValid)
            {
                return null;
            }

            progress?.Report(20);

            // Load ROMs efficiently in background thread
            if (cacheData.ScannedRoms != null && romLoadedCallback != null)
            {
                var romList = cacheData.ScannedRoms.Values.ToList();
                var totalRoms = romList.Count;
                
                // Process ROMs in background thread to avoid UI blocking
                await Task.Run(() =>
                {
                    for (int i = 0; i < totalRoms; i++)
                    {
                        var rom = romList[i];
                        rom.InDestination = false;
                        
                        // Batch UI updates - only call callback every 100 ROMs
                        if (i % 100 == 0 || i == totalRoms - 1)
                        {
                            // Report progress (20% to 100%)
                            var progressPercent = 20 + (int)((double)(i + 1) / totalRoms * 80);
                            progress?.Report(progressPercent);
                        }
                    }
                });
                
                // Single batch update at the end - much more efficient
                progress?.Report(100);
            }

            // Clear InDestination flags for all ROMs since destination directory state may have changed
            if (cacheData.ScannedRoms != null)
            {
                foreach (var rom in cacheData.ScannedRoms.Values)
                {
                    rom.InDestination = false;
                }
            }

            return cacheData.ScannedRoms;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets cache information for display purposes
    /// </summary>
    public async Task<CacheInfo?> GetCacheInfoAsync()
    {
        try
        {
            if (!File.Exists(_cacheFilePath))
                return null;

            var fileInfo = new FileInfo(_cacheFilePath);
            var json = await File.ReadAllTextAsync(_cacheFilePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var cacheData = JsonSerializer.Deserialize<RomCacheData>(json, options);
            if (cacheData == null)
                return null;

            return new CacheInfo
            {
                CreatedAt = cacheData.CreatedAt,
                RomCount = cacheData.ScannedRoms?.Count ?? 0,
                FileSizeBytes = fileInfo.Length,
                RomRepositoryPath = cacheData.RomRepositoryPath,
                CHDRepositoryPath = cacheData.CHDRepositoryPath,
                MameXmlPath = cacheData.MameXmlPath
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clears the ROM cache
    /// </summary>
    public Task ClearCacheAsync()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
            }
            return Task.CompletedTask;
        }
        catch (Exception)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Validates if the cached data is still valid
    /// </summary>
    private Task<CacheValidationResult> ValidateCacheAsync(RomCacheData cacheData, AppSettings settings)
    {
        // Check version compatibility
        if (cacheData.Version != CacheVersion)
            return Task.FromResult(new CacheValidationResult(false, "Cache version mismatch"));

        // Check if paths have changed
        if (cacheData.RomRepositoryPath != settings.RomRepositoryPath)
            return Task.FromResult(new CacheValidationResult(false, "ROM repository path changed"));

        if (cacheData.CHDRepositoryPath != settings.CHDRepositoryPath)
            return Task.FromResult(new CacheValidationResult(false, "CHD repository path changed"));

        if (cacheData.MameXmlPath != settings.MameXmlPath)
            return Task.FromResult(new CacheValidationResult(false, "MAME XML path changed"));

        // Check if directories still exist
        if (!Directory.Exists(settings.RomRepositoryPath))
            return Task.FromResult(new CacheValidationResult(false, "ROM repository no longer exists"));

        if (!string.IsNullOrEmpty(settings.CHDRepositoryPath) && !Directory.Exists(settings.CHDRepositoryPath))
            return Task.FromResult(new CacheValidationResult(false, "CHD repository no longer exists"));

        if (!File.Exists(settings.MameXmlPath))
            return Task.FromResult(new CacheValidationResult(false, "MAME XML file no longer exists"));

        // Skip hash-based validation for speed - just check basic path and age validation

        // Check cache age (invalidate after 30 days)
        if (DateTime.UtcNow - cacheData.CreatedAt > TimeSpan.FromDays(30))
            return Task.FromResult(new CacheValidationResult(false, "Cache too old"));

        return Task.FromResult(new CacheValidationResult(true, "Cache is valid"));
    }

    /// <summary>
    /// Gets a hash representing the contents of a directory (file names and sizes)
    /// </summary>
    private async Task<string> GetDirectoryHashAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return string.Empty;

        return await Task.Run(() =>
        {
            try
            {
                var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Select(f => new FileInfo(f))
                    .Where(fi => fi.Exists)
                    .OrderBy(fi => fi.Name)
                    .Select(fi => $"{fi.Name}:{fi.Length}:{fi.LastWriteTimeUtc.Ticks}")
                    .ToList();

                var combined = string.Join("|", files);
                return GetStringHash(combined);
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    /// <summary>
    /// Gets a hash for a single file
    /// </summary>
    private string GetFileHash(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return string.Empty;

            var fileInfo = new FileInfo(filePath);
            return GetStringHash($"{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}");
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a hash for a string
    /// </summary>
    private string GetStringHash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }
}

/// <summary>
/// Data structure for ROM cache
/// </summary>
public class RomCacheData
{
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RomRepositoryPath { get; set; } = string.Empty;
    public string? CHDRepositoryPath { get; set; }
    public string MameXmlPath { get; set; } = string.Empty;
    public string RomRepositoryHash { get; set; } = string.Empty;
    public string? CHDRepositoryHash { get; set; }
    public string? MameXmlHash { get; set; }
    public Dictionary<string, ScannedRom> ScannedRoms { get; set; } = new();
    public int TotalChdDirectories { get; set; } // Store total CHD directories found during scan
}

/// <summary>
/// Information about the cache
/// </summary>
public class CacheInfo
{
    public DateTime CreatedAt { get; set; }
    public int RomCount { get; set; }
    public long FileSizeBytes { get; set; }
    public string RomRepositoryPath { get; set; } = string.Empty;
    public string? CHDRepositoryPath { get; set; }
    public string MameXmlPath { get; set; } = string.Empty;

    public string FormattedFileSize
    {
        get
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = FileSizeBytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}

/// <summary>
/// Result of cache validation
/// </summary>
public class CacheValidationResult
{
    public bool IsValid { get; }
    public string Reason { get; }

    public CacheValidationResult(bool isValid, string reason)
    {
        IsValid = isValid;
        Reason = reason;
    }
}
