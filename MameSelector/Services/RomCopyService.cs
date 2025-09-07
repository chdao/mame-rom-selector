using MameSelector.Models;
using System.IO.Compression;

namespace MameSelector.Services
{
    /// <summary>
    /// Service for copying ROM files to the destination directory
    /// </summary>
    public class RomCopyService
    {
        /// <summary>
        /// Copies selected ROMs to the destination directory
        /// </summary>
        /// <param name="selectedRoms">ROMs to copy</param>
        /// <param name="destinationPath">Destination directory path</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Copy result with statistics</returns>
        public async Task<CopyResult> CopyRomsAsync(
            IEnumerable<ScannedRom> selectedRoms, 
            string destinationPath,
            IProgress<CopyProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));

            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);

            var result = new CopyResult();
            var romsList = selectedRoms.ToList();
            var totalRoms = romsList.Count;

            progress?.Report(new CopyProgress { Phase = "Starting copy operation...", Percentage = 0 });

            for (int i = 0; i < romsList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rom = romsList[i];
                var currentProgress = (int)((double)i / totalRoms * 100);
                
                progress?.Report(new CopyProgress 
                { 
                    Phase = $"Copying {rom.Name}...", 
                    Percentage = currentProgress,
                    CurrentRom = rom.Name,
                    RomsCopied = i,
                    TotalRoms = totalRoms
                });

                try
                {
                    await CopyRomAsync(rom, destinationPath, cancellationToken);
                    result.SuccessfulCopies++;
                    result.CopiedRoms.Add(rom.Name);
                }
                catch (Exception ex)
                {
                    result.FailedCopies++;
                    result.FailedRoms.Add(new FailedCopy { RomName = rom.Name, Error = ex.Message });
                }
            }

            progress?.Report(new CopyProgress 
            { 
                Phase = "Copy operation completed", 
                Percentage = 100,
                RomsCopied = totalRoms,
                TotalRoms = totalRoms
            });

            return result;
        }

        /// <summary>
        /// Copies a single ROM and its associated CHD files
        /// </summary>
        private async Task CopyRomAsync(ScannedRom rom, string destinationPath, CancellationToken cancellationToken)
        {
            // Copy ROM file if it exists
            if (!string.IsNullOrEmpty(rom.RomFilePath) && File.Exists(rom.RomFilePath))
            {
                var romFileName = Path.GetFileName(rom.RomFilePath);
                var romDestinationPath = Path.Combine(destinationPath, romFileName);
                
                await CopyFileAsync(rom.RomFilePath, romDestinationPath, cancellationToken);
            }

            // Copy CHD files if they exist - they go in a subfolder named after the ROM
            if (rom.ChdFiles.Any())
            {
                // Create subfolder named after the ROM (without extension)
                var romName = rom.Name; // This should be the ROM name without extension
                var chdFolderPath = Path.Combine(destinationPath, romName);
                
                // Ensure the CHD folder exists
                if (!Directory.Exists(chdFolderPath))
                {
                    Directory.CreateDirectory(chdFolderPath);
                }

                // Copy each CHD file to the ROM-named subfolder
                foreach (var chdFile in rom.ChdFiles)
                {
                    if (File.Exists(chdFile))
                    {
                        var chdFileName = Path.GetFileName(chdFile);
                        var chdDestinationPath = Path.Combine(chdFolderPath, chdFileName);
                        
                        await CopyFileAsync(chdFile, chdDestinationPath, cancellationToken);
                    }
                }
            }
        }

        /// <summary>
        /// Copies a single file with proper error handling
        /// </summary>
        private async Task CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            // Create destination directory if it doesn't exist
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // Copy the file
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            
            // Preserve file attributes
            var sourceFileInfo = new FileInfo(sourcePath);
            var destinationFileInfo = new FileInfo(destinationPath);
            destinationFileInfo.CreationTime = sourceFileInfo.CreationTime;
            destinationFileInfo.LastWriteTime = sourceFileInfo.LastWriteTime;
        }

        /// <summary>
        /// Validates that all required files exist before copying
        /// </summary>
        public CopyValidationResult ValidateCopyOperation(IEnumerable<ScannedRom> selectedRoms, string destinationPath)
        {
            var result = new CopyValidationResult();
            var romsList = selectedRoms.ToList();

            // Check destination path
            if (string.IsNullOrEmpty(destinationPath))
            {
                result.Errors.Add("Destination path is not configured");
                return result;
            }

            // Check if destination is writable
            try
            {
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }
                
                // Test write access
                var testFile = Path.Combine(destinationPath, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Cannot write to destination directory: {ex.Message}");
                return result;
            }

            // Validate ROM files
            foreach (var rom in romsList)
            {
                if (!string.IsNullOrEmpty(rom.RomFilePath) && !File.Exists(rom.RomFilePath))
                {
                    result.Warnings.Add($"ROM file not found: {rom.Name} ({rom.RomFilePath})");
                }

                foreach (var chdFile in rom.ChdFiles)
                {
                    if (!File.Exists(chdFile))
                    {
                        result.Warnings.Add($"CHD file not found: {rom.Name} ({chdFile})");
                    }
                }
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }
    }

    /// <summary>
    /// Result of a ROM copy operation
    /// </summary>
    public class CopyResult
    {
        public int SuccessfulCopies { get; set; }
        public int FailedCopies { get; set; }
        public List<string> CopiedRoms { get; set; } = new();
        public List<FailedCopy> FailedRoms { get; set; } = new();
        public long TotalBytesCopied { get; set; }
    }

    /// <summary>
    /// Information about a failed copy operation
    /// </summary>
    public class FailedCopy
    {
        public string RomName { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Progress information for copy operations
    /// </summary>
    public class CopyProgress
    {
        public string Phase { get; set; } = string.Empty;
        public int Percentage { get; set; }
        public string CurrentRom { get; set; } = string.Empty;
        public int RomsCopied { get; set; }
        public int TotalRoms { get; set; }
    }

    /// <summary>
    /// Result of copy operation validation
    /// </summary>
    public class CopyValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
