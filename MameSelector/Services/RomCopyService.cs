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

            // Calculate total bytes to copy for accurate progress tracking
            var totalBytesToCopy = CalculateTotalBytesToCopy(romsList);
            var totalFilesToCopy = CalculateTotalFilesToCopy(romsList);
            var totalBytesCopied = 0L;
            var filesCopied = 0;

            progress?.Report(new CopyProgress 
            { 
                Phase = "Starting copy operation...", 
                Percentage = 0,
                TotalRoms = totalRoms,
                TotalFiles = totalFilesToCopy,
                TotalBytesToCopy = totalBytesToCopy
            });

            for (int i = 0; i < romsList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rom = romsList[i];
                
                // Calculate bytes already copied from previous ROMs
                var bytesCopiedBeforeThisRom = totalBytesCopied;
                
                // Calculate total bytes for this ROM
                var romBytes = GetRomTotalBytes(rom);
                
                progress?.Report(new CopyProgress 
                { 
                    Phase = $"Starting {rom.Name}...", 
                    Percentage = totalBytesToCopy > 0 ? (int)((double)totalBytesCopied / totalBytesToCopy * 100) : 0,
                    CurrentRom = rom.Name,
                    RomsCopied = i,
                    TotalRoms = totalRoms,
                    FilesCopied = filesCopied,
                    TotalFiles = totalFilesToCopy,
                    TotalBytesCopied = totalBytesCopied,
                    TotalBytesToCopy = totalBytesToCopy
                });

                try
                {
                    await CopyRomAsync(rom, destinationPath, progress, totalBytesToCopy, totalBytesCopied, cancellationToken);
                    result.SuccessfulCopies++;
                    result.CopiedRoms.Add(rom.Name);
                    
                    // Update counters
                    totalBytesCopied += romBytes;
                    filesCopied += GetRomFileCount(rom);
                }
                catch (Exception ex)
                {
                    result.FailedCopies++;
                    result.FailedRoms.Add(new FailedCopy { RomName = rom.Name, Error = ex.Message });
                    
                    // Still update counters for failed ROMs to keep progress accurate
                    totalBytesCopied += romBytes;
                    filesCopied += GetRomFileCount(rom);
                }
            }

            progress?.Report(new CopyProgress 
            { 
                Phase = "Copy operation completed", 
                Percentage = 100,
                RomsCopied = totalRoms,
                TotalRoms = totalRoms,
                FilesCopied = filesCopied,
                TotalFiles = totalFilesToCopy,
                TotalBytesCopied = totalBytesCopied,
                TotalBytesToCopy = totalBytesToCopy
            });

            return result;
        }

        /// <summary>
        /// Copies a single ROM and its associated CHD files
        /// </summary>
        private async Task CopyRomAsync(ScannedRom rom, string destinationPath, 
            IProgress<CopyProgress>? overallProgress = null,
            long totalBytesToCopy = 0,
            long totalBytesCopiedSoFar = 0,
            CancellationToken cancellationToken = default)
        {
            // Copy ROM file if it exists
            if (!string.IsNullOrEmpty(rom.RomFilePath) && File.Exists(rom.RomFilePath))
            {
                var romFileName = Path.GetFileName(rom.RomFilePath);
                var romDestinationPath = Path.Combine(destinationPath, romFileName);
                
                // Create file progress reporter for this ROM file
                var fileProgress = new Progress<(long bytesCopied, long totalBytes)>(progress =>
                {
                    if (overallProgress != null)
                    {
                        var currentBytesCopied = totalBytesCopiedSoFar + progress.bytesCopied;
                        var overallPercentage = totalBytesToCopy > 0 ? (int)((double)currentBytesCopied / totalBytesToCopy * 100) : 0;
                        
                        overallProgress.Report(new CopyProgress
                        {
                            Phase = $"Copying {rom.Name}...",
                            Percentage = overallPercentage,
                            CurrentRom = rom.Name,
                            CurrentFile = romFileName,
                            CurrentFileBytesCopied = progress.bytesCopied,
                            CurrentFileSize = progress.totalBytes,
                            TotalBytesCopied = currentBytesCopied,
                            TotalBytesToCopy = totalBytesToCopy
                        });
                    }
                });
                
                await CopyFileAsync(rom.RomFilePath, romDestinationPath, fileProgress, cancellationToken);
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
                        
                        // Create file progress reporter for this CHD file
                        var chdFileProgress = new Progress<(long bytesCopied, long totalBytes)>(progress =>
                        {
                            if (overallProgress != null)
                            {
                                var currentBytesCopied = totalBytesCopiedSoFar + progress.bytesCopied;
                                var overallPercentage = totalBytesToCopy > 0 ? (int)((double)currentBytesCopied / totalBytesToCopy * 100) : 0;
                                
                                overallProgress.Report(new CopyProgress
                                {
                                    Phase = $"Copying {rom.Name} CHD...",
                                    Percentage = overallPercentage,
                                    CurrentRom = rom.Name,
                                    CurrentFile = chdFileName,
                                    CurrentFileBytesCopied = progress.bytesCopied,
                                    CurrentFileSize = progress.totalBytes,
                                    TotalBytesCopied = currentBytesCopied,
                                    TotalBytesToCopy = totalBytesToCopy
                                });
                            }
                        });
                        
                        await CopyFileAsync(chdFile, chdDestinationPath, chdFileProgress, cancellationToken);
                    }
                }
            }
        }

        /// <summary>
        /// Copies a single file with proper error handling and progress reporting
        /// </summary>
        private async Task CopyFileAsync(string sourcePath, string destinationPath, 
            IProgress<(long bytesCopied, long totalBytes)>? fileProgress = null,
            CancellationToken cancellationToken = default)
        {
            // Create destination directory if it doesn't exist
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            var sourceFileInfo = new FileInfo(sourcePath);
            var totalBytes = sourceFileInfo.Length;
            var bytesCopied = 0L;

            // Copy the file with progress reporting
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            
            var buffer = new byte[64 * 1024]; // 64KB buffer for better performance
            int bytesRead;
            
            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destinationStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                bytesCopied += bytesRead;
                
                // Report progress every 64KB or at completion
                if (fileProgress != null && (bytesCopied % (64 * 1024) == 0 || bytesCopied == totalBytes))
                {
                    fileProgress.Report((bytesCopied, totalBytes));
                }
            }
            
            // Preserve file attributes
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

        /// <summary>
        /// Calculates the total number of bytes to copy for all ROMs
        /// </summary>
        private long CalculateTotalBytesToCopy(List<ScannedRom> roms)
        {
            long totalBytes = 0;
            foreach (var rom in roms)
            {
                totalBytes += GetRomTotalBytes(rom);
            }
            return totalBytes;
        }

        /// <summary>
        /// Calculates the total number of files to copy for all ROMs
        /// </summary>
        private int CalculateTotalFilesToCopy(List<ScannedRom> roms)
        {
            int totalFiles = 0;
            foreach (var rom in roms)
            {
                totalFiles += GetRomFileCount(rom);
            }
            return totalFiles;
        }

        /// <summary>
        /// Gets the total number of bytes for a ROM (including CHD files)
        /// </summary>
        private long GetRomTotalBytes(ScannedRom rom)
        {
            long totalBytes = 0;
            
            // Add ROM file size
            if (!string.IsNullOrEmpty(rom.RomFilePath) && File.Exists(rom.RomFilePath))
            {
                totalBytes += new FileInfo(rom.RomFilePath).Length;
            }
            
            // Add CHD file sizes
            if (rom.ChdFiles?.Any() == true)
            {
                foreach (var chdFile in rom.ChdFiles)
                {
                    if (File.Exists(chdFile))
                    {
                        totalBytes += new FileInfo(chdFile).Length;
                    }
                }
            }
            
            return totalBytes;
        }

        /// <summary>
        /// Gets the total number of files for a ROM (including CHD files)
        /// </summary>
        private int GetRomFileCount(ScannedRom rom)
        {
            int fileCount = 0;
            
            // Count ROM file
            if (!string.IsNullOrEmpty(rom.RomFilePath) && File.Exists(rom.RomFilePath))
            {
                fileCount++;
            }
            
            // Count CHD files
            if (rom.ChdFiles?.Any() == true)
            {
                foreach (var chdFile in rom.ChdFiles)
                {
                    if (File.Exists(chdFile))
                    {
                        fileCount++;
                    }
                }
            }
            
            return fileCount;
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
        public string CurrentFile { get; set; } = string.Empty;
        public long CurrentFileBytesCopied { get; set; }
        public long CurrentFileSize { get; set; }
        public long TotalBytesCopied { get; set; }
        public long TotalBytesToCopy { get; set; }
        public int FilesCopied { get; set; }
        public int TotalFiles { get; set; }
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
