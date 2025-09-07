using System.Text.Json;
using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Service for caching scanned ROM data to avoid re-scanning on every startup
/// </summary>
public class RomCacheService
{
    private readonly string _cacheFilePath;
    private const string CacheFileName = "rom_cache.json";
    private const int CacheVersion = 1;

    public RomCacheService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "MameSelector");
        Directory.CreateDirectory(appFolder);
        _cacheFilePath = Path.Combine(appFolder, CacheFileName);
    }

    /// <summary>
    /// Saves the scanned ROM data to cache
    /// </summary>
    public async Task SaveCacheAsync(Dictionary<string, ScannedRom> scannedRoms, AppSettings settings)
    {
        try
        {
            var cacheData = new RomCacheData
            {
                Version = CacheVersion,
                CreatedAt = DateTime.UtcNow,
                RomRepositoryPath = settings.RomRepositoryPath,
                CHDRepositoryPath = settings.CHDRepositoryPath,
                MameXmlPath = settings.MameXmlPath,
                RomRepositoryHash = null, // Skip hash calculation for speed
                CHDRepositoryHash = null, // Skip hash calculation for speed
                MameXmlHash = null, // Skip hash calculation for speed
                ScannedRoms = scannedRoms
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = false, // Compact for performance
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(cacheData, options);
            await File.WriteAllTextAsync(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - caching is optional
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

            // Load ROMs with progress and real-time callbacks
            if (cacheData.ScannedRoms != null && romLoadedCallback != null)
            {
                var romList = cacheData.ScannedRoms.Values.ToList();
                var totalRoms = romList.Count;
                
                for (int i = 0; i < totalRoms; i++)
                {
                    var rom = romList[i];
                    
                    // Clear InDestination flag since destination directory state may have changed
                    rom.InDestination = false;
                    
                    romLoadedCallback(rom);
                    
                    // Report progress (20% to 100%)
                    var progressPercent = 20 + (int)((double)(i + 1) / totalRoms * 80);
                    progress?.Report(progressPercent);
                    
                    // Small delay to allow UI updates and make progress visible
                    if (i % 100 == 0) // Every 100 ROMs
                    {
                        await Task.Delay(1);
                    }
                }
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
        catch (Exception ex)
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
        catch (Exception ex)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Validates if the cached data is still valid
    /// </summary>
    private async Task<CacheValidationResult> ValidateCacheAsync(RomCacheData cacheData, AppSettings settings)
    {
        // Check version compatibility
        if (cacheData.Version != CacheVersion)
            return new CacheValidationResult(false, "Cache version mismatch");

        // Check if paths have changed
        if (cacheData.RomRepositoryPath != settings.RomRepositoryPath)
            return new CacheValidationResult(false, "ROM repository path changed");

        if (cacheData.CHDRepositoryPath != settings.CHDRepositoryPath)
            return new CacheValidationResult(false, "CHD repository path changed");

        if (cacheData.MameXmlPath != settings.MameXmlPath)
            return new CacheValidationResult(false, "MAME XML path changed");

        // Check if directories still exist
        if (!Directory.Exists(settings.RomRepositoryPath))
            return new CacheValidationResult(false, "ROM repository no longer exists");

        if (!string.IsNullOrEmpty(settings.CHDRepositoryPath) && !Directory.Exists(settings.CHDRepositoryPath))
            return new CacheValidationResult(false, "CHD repository no longer exists");

        if (!File.Exists(settings.MameXmlPath))
            return new CacheValidationResult(false, "MAME XML file no longer exists");

        // Skip hash-based validation for speed - just check basic path and age validation

        // Check cache age (invalidate after 30 days)
        if (DateTime.UtcNow - cacheData.CreatedAt > TimeSpan.FromDays(30))
            return new CacheValidationResult(false, "Cache too old");

        return new CacheValidationResult(true, "Cache is valid");
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
