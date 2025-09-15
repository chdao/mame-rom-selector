using MameSelector.Models;
using System.IO.Compression;

namespace MameSelector.Services;

/// <summary>
/// Service for validating merged ROM integrity and dependencies
/// </summary>
public class MergedRomValidator
{
    private readonly LoggingService? _loggingService;

    public MergedRomValidator(LoggingService? loggingService = null)
    {
        _loggingService = loggingService;
    }

    /// <summary>
    /// Validates that a clone ROM has access to all required parent files
    /// </summary>
    /// <param name="rom">Scanned ROM to validate</param>
    /// <param name="games">Dictionary of MAME games</param>
    /// <param name="scannedRoms">Dictionary of all scanned ROMs</param>
    /// <returns>Validation result</returns>
    public async Task<ValidationResult> ValidateMergedRomAsync(
        ScannedRom rom,
        Dictionary<string, MameGame> games,
        Dictionary<string, ScannedRom> scannedRoms)
    {
        var result = new ValidationResult
        {
            RomName = rom.Name,
            IsValid = true
        };

        try
        {
            // Check if this is a clone
            if (!rom.IsClone || rom.Metadata == null)
            {
                result.IsValid = true;
                result.Status = MergedRomValidationStatus.Valid;
                return result;
            }

            // Check parent availability
            var parentValidation = await ValidateParentAvailabilityAsync(rom, scannedRoms);
            if (!parentValidation.IsValid)
            {
                result.IsValid = false;
                result.Status = MergedRomValidationStatus.MissingParentFiles;
                result.ErrorMessage = parentValidation.ErrorMessage;
                result.MissingFiles = parentValidation.MissingParents;
                return result;
            }

            // Check CRC integrity if enabled
            var crcValidation = await ValidateCrcIntegrityAsync(rom, games, scannedRoms);
            if (!crcValidation.IsValid)
            {
                result.IsValid = false;
                result.Status = MergedRomValidationStatus.InvalidChecksums;
                result.ErrorMessage = crcValidation.ErrorMessage;
                result.InvalidFiles = crcValidation.InvalidFiles;
                return result;
            }

            result.Status = MergedRomValidationStatus.Valid;
            result.IsValid = true;
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Status = MergedRomValidationStatus.IncompleteGame;
            result.ErrorMessage = $"Validation error: {ex.Message}";
            _loggingService?.LogError($"Error validating ROM {rom.Name}: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Validates parent ROM availability for a clone
    /// </summary>
    private async Task<ParentValidationResult> ValidateParentAvailabilityAsync(
        ScannedRom cloneRom,
        Dictionary<string, ScannedRom> scannedRoms)
    {
        var result = new ParentValidationResult
        {
            CloneName = cloneRom.Name,
            IsValid = true
        };

        await Task.Run(() =>
        {
            // Check if clone has missing parent files
            foreach (var missingParent in cloneRom.MissingParentFiles)
            {
                if (!scannedRoms.ContainsKey(missingParent))
                {
                    result.MissingParents.Add(missingParent);
                    result.IsValid = false;
                }
            }

            if (!result.IsValid)
            {
                result.ErrorMessage = $"Missing parent ROMs: {string.Join(", ", result.MissingParents)}";
            }
        });

        return result;
    }

    /// <summary>
    /// Validates CRC integrity across parent-child relationships
    /// </summary>
    private async Task<CrcValidationResult> ValidateCrcIntegrityAsync(
        ScannedRom cloneRom,
        Dictionary<string, MameGame> games,
        Dictionary<string, ScannedRom> scannedRoms)
    {
        var result = new CrcValidationResult
        {
            RomName = cloneRom.Name,
            IsValid = true
        };

        await Task.Run(() =>
        {
            try
            {
                // Get clone's ROM files from metadata
                if (cloneRom.Metadata?.RomFiles == null)
                {
                    result.IsValid = true; // No ROM files to validate
                    return;
                }

                // Check each ROM file in the clone
                foreach (var romFile in cloneRom.Metadata.RomFiles)
                {
                    // Check if this file exists in the clone's ROM archive
                    if (!string.IsNullOrEmpty(cloneRom.RomFilePath) && File.Exists(cloneRom.RomFilePath))
                    {
                        var fileExists = CheckFileInArchive(cloneRom.RomFilePath, romFile.Name);
                        if (!fileExists)
                        {
                            // File not in clone, check if it's in parent
                            var foundInParent = CheckFileInParentArchives(romFile.Name, cloneRom, scannedRoms);
                            if (!foundInParent)
                            {
                                result.InvalidFiles.Add($"{romFile.Name} (missing from clone and parents)");
                                result.IsValid = false;
                            }
                        }
                    }
                }

                if (!result.IsValid)
                {
                    result.ErrorMessage = $"Missing ROM files: {string.Join(", ", result.InvalidFiles)}";
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"CRC validation error: {ex.Message}";
                _loggingService?.LogError($"CRC validation error for {cloneRom.Name}: {ex.Message}");
            }
        });

        return result;
    }

    /// <summary>
    /// Checks if a file exists in a ROM archive
    /// </summary>
    private bool CheckFileInArchive(string archivePath, string fileName)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            return archive.Entries.Any(entry => 
                string.Equals(entry.Name, fileName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a file exists in any parent ROM archives
    /// </summary>
    private bool CheckFileInParentArchives(
        string fileName,
        ScannedRom cloneRom,
        Dictionary<string, ScannedRom> scannedRoms)
    {
        // Check available parent ROMs
        foreach (var parentName in cloneRom.AvailableParentFiles)
        {
            if (scannedRoms.TryGetValue(parentName, out var parentRom) &&
                !string.IsNullOrEmpty(parentRom.RomFilePath) &&
                File.Exists(parentRom.RomFilePath))
            {
                if (CheckFileInArchive(parentRom.RomFilePath, fileName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Validates all ROMs in a collection for merged ROMset integrity
    /// </summary>
    /// <param name="scannedRoms">Dictionary of scanned ROMs</param>
    /// <param name="games">Dictionary of MAME games</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection validation results</returns>
    public async Task<CollectionValidationResult> ValidateCollectionAsync(
        Dictionary<string, ScannedRom> scannedRoms,
        Dictionary<string, MameGame> games,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CollectionValidationResult();
        var totalRoms = scannedRoms.Count;
        var processedRoms = 0;

        _loggingService?.LogInfo($"Starting collection validation for {totalRoms} ROMs...");

        foreach (var rom in scannedRoms.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validation = await ValidateMergedRomAsync(rom, games, scannedRoms);
            
            if (validation.IsValid)
            {
                result.ValidRoms.Add(rom.Name);
            }
            else
            {
                result.InvalidRoms.Add(rom.Name);
                result.ValidationErrors.Add(validation);
            }

            processedRoms++;
            progress?.Report((int)((double)processedRoms / totalRoms * 100));
        }

        _loggingService?.LogInfo($"Collection validation complete: {result.ValidRoms.Count} valid, {result.InvalidRoms.Count} invalid");

        return result;
    }
}

/// <summary>
/// Result of ROM validation
/// </summary>
public class ValidationResult
{
    public string RomName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public MergedRomValidationStatus Status { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> MissingFiles { get; set; } = new();
    public List<string> InvalidFiles { get; set; } = new();
}


/// <summary>
/// Result of CRC validation
/// </summary>
public class CrcValidationResult
{
    public string RomName { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> InvalidFiles { get; set; } = new();
}

/// <summary>
/// Result of collection validation
/// </summary>
public class CollectionValidationResult
{
    public List<string> ValidRoms { get; set; } = new();
    public List<string> InvalidRoms { get; set; } = new();
    public List<ValidationResult> ValidationErrors { get; set; } = new();
    
    public int TotalRoms => ValidRoms.Count + InvalidRoms.Count;
    public double ValidRatio => TotalRoms > 0 ? (double)ValidRoms.Count / TotalRoms : 0;
}
