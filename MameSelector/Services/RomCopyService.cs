using MameSelector.Models;
using System.IO.Compression;

namespace MameSelector.Services
{
    /// <summary>
    /// Service for copying ROM files to the destination directory
    /// </summary>
    public class RomCopyService
    {
        private readonly LoggingService? _loggingService;

        public RomCopyService(LoggingService? loggingService = null)
        {
            _loggingService = loggingService;
        }

        /// <summary>
        /// Reconstructs a merged ROM ZIP file with only the files needed for the selected ROM
        /// </summary>
        /// <param name="selectedRom">The ROM to reconstruct</param>
        /// <param name="parentRoms">Available parent ROMs</param>
        /// <param name="destinationPath">Destination directory path</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if successful, false otherwise</returns>
        private async Task<bool> ReconstructMergedRomAsync(
            ScannedRom selectedRom,
            Dictionary<string, ScannedRom> parentRoms,
            string destinationPath,
            IProgress<CopyProgress>? progress,
            CancellationToken cancellationToken)
        {
            try
            {
                _loggingService?.LogInfo($"Reconstructing merged ROM: {selectedRom.Name}");

                // Get the ROM files needed for this specific ROM
                var requiredFiles = await GetRequiredRomFilesAsync(selectedRom, parentRoms, cancellationToken);
                
                if (!requiredFiles.Any())
                {
                    _loggingService?.LogWarning($"No ROM files found for {selectedRom.Name}");
                    return false;
                }

                // Create the destination ZIP file
                var destinationZipPath = Path.Combine(destinationPath, $"{selectedRom.Name}.zip");
                
                // Remove existing file if it exists
                if (File.Exists(destinationZipPath))
                {
                    File.Delete(destinationZipPath);
                }

                // Create the reconstructed ZIP
                using (var archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create))
                {
                    foreach (var fileInfo in requiredFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        try
                        {
                            // Add the file to the ZIP archive
                            var entry = archive.CreateEntry(fileInfo.FileName, CompressionLevel.Optimal);
                            
                            using (var entryStream = entry.Open())
                            using (var sourceArchive = ZipFile.OpenRead(fileInfo.SourceZipPath))
                            using (var sourceStream = sourceArchive.GetEntry(fileInfo.FileName)?.Open())
                            {
                                if (sourceStream != null)
                                {
                                    await sourceStream.CopyToAsync(entryStream, cancellationToken);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService?.LogError($"Error adding file {fileInfo.FileName} to reconstructed ZIP: {ex.Message}");
                            return false;
                        }
                    }
                }

                _loggingService?.LogInfo($"Successfully reconstructed {selectedRom.Name}.zip with {requiredFiles.Count} files");
                return true;
            }
            catch (Exception ex)
            {
                _loggingService?.LogError($"Error reconstructing merged ROM {selectedRom.Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the ROM files required for a specific ROM from merged ROMsets
        /// </summary>
        /// <param name="selectedRom">The ROM to get files for</param>
        /// <param name="parentRoms">Available parent ROMs</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of required ROM files</returns>
        private async Task<List<RequiredRomFile>> GetRequiredRomFilesAsync(
            ScannedRom selectedRom,
            Dictionary<string, ScannedRom> parentRoms,
            CancellationToken cancellationToken)
        {
            var requiredFiles = new List<RequiredRomFile>();

            await Task.Run(() =>
            {
                try
                {
                    // Get ROM files from the selected ROM's metadata
                    if (selectedRom.Metadata?.RomFiles == null)
                    {
                        return;
                    }

                    foreach (var romFile in selectedRom.Metadata.RomFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Try to find this file in the selected ROM's archive first
                        var foundInSelected = false;
                        if (!string.IsNullOrEmpty(selectedRom.RomFilePath) && File.Exists(selectedRom.RomFilePath))
                        {
                            using (var archive = ZipFile.OpenRead(selectedRom.RomFilePath))
                            {
                                var entry = archive.GetEntry(romFile.Name);
                                if (entry != null)
                                {
                                    requiredFiles.Add(new RequiredRomFile
                                    {
                                        FileName = romFile.Name,
                                        SourceZipPath = selectedRom.RomFilePath!,
                                        SourceRomName = selectedRom.Name
                                    });
                                    foundInSelected = true;
                                }
                            }
                        }

                        // If not found in selected ROM, look in parent ROMs
                        if (!foundInSelected)
                        {
                            foreach (var parentName in selectedRom.AvailableParentFiles)
                            {
                                if (parentRoms.TryGetValue(parentName, out var parentRom) &&
                                    !string.IsNullOrEmpty(parentRom.RomFilePath) &&
                                    File.Exists(parentRom.RomFilePath))
                                {
                                    using (var archive = ZipFile.OpenRead(parentRom.RomFilePath))
                                    {
                                        var entry = archive.GetEntry(romFile.Name);
                                        if (entry != null)
                                        {
                                            requiredFiles.Add(new RequiredRomFile
                                            {
                                                FileName = romFile.Name,
                                                SourceZipPath = parentRom.RomFilePath!,
                                                SourceRomName = parentName
                                            });
                                            foundInSelected = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (!foundInSelected)
                        {
                            _loggingService?.LogWarning($"Required file {romFile.Name} not found for ROM {selectedRom.Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _loggingService?.LogError($"Error getting required ROM files for {selectedRom.Name}: {ex.Message}");
                }
            }, cancellationToken);

            return requiredFiles;
        }

        /// <summary>
        /// Copies selected ROMs with merged ROMset dependency support
        /// </summary>
        /// <param name="selectedRoms">ROMs to copy</param>
        /// <param name="destinationPath">Destination directory path</param>
        /// <param name="romsetType">Type of ROMset being copied</param>
        /// <param name="autoCopyDependencies">Whether to automatically copy parent ROMs</param>
        /// <param name="parentResolver">Parent-child dependency resolver</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="romCopiedCallback">Callback for each ROM copied</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Copy result with statistics</returns>
        public async Task<CopyResult> CopyMergedRomsAsync(
            IEnumerable<ScannedRom> selectedRoms,
            string destinationPath,
            RomsetType romsetType,
            bool autoCopyDependencies,
            ParentRomResolver? parentResolver,
            IProgress<CopyProgress>? progress = null,
            Func<string, Task>? romCopiedCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));

            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);

            var romsList = selectedRoms.ToList();
            var totalRoms = romsList.Count;

            // For merged ROMsets, expand the list to include dependencies
            var romsToCopy = new List<ScannedRom>(romsList);
            if (romsetType == RomsetType.Merged && autoCopyDependencies && parentResolver != null)
            {
                romsToCopy = await ExpandWithDependenciesAsync(romsList, parentResolver, cancellationToken);
                _loggingService?.LogInfo($"Expanded ROM list from {totalRoms} to {romsToCopy.Count} ROMs (including dependencies)");
            }

            // Log the ROMs being copied
            var romNames = string.Join(", ", romsToCopy.Select(r => r.Name));
            _loggingService?.LogInfo($"Starting merged ROM copy operation for {romsToCopy.Count} ROM(s): {romNames}");

            var result = new CopyResult();
            var totalBytesToCopy = CalculateTotalBytesToCopy(romsToCopy);
            var totalFilesToCopy = CalculateTotalFilesToCopy(romsToCopy);
            var totalBytesCopied = 0L;
            var filesCopied = 0;

            // Initial progress report
            progress?.Report(new CopyProgress
            {
                Phase = "Starting merged ROM copy operation...",
                Percentage = 0,
                RomsCopied = 0,
                TotalRoms = romsToCopy.Count,
                FilesCopied = 0,
                TotalFiles = totalFilesToCopy,
                TotalBytesCopied = 0,
                TotalBytesToCopy = totalBytesToCopy
            });

            for (int i = 0; i < romsToCopy.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rom = romsToCopy[i];
                
                try
                {
                    // For merged ROMsets, reconstruct the ZIP file instead of copying
                    bool copySuccess;
                    if (romsetType == RomsetType.Merged && rom.IsClone)
                    {
                        copySuccess = await ReconstructMergedRomAsync(rom, romsToCopy.ToDictionary(r => r.Name, r => r), destinationPath, progress, cancellationToken);
                    }
                    else
                    {
                        await CopyRomAsync(rom, destinationPath, progress, totalBytesToCopy, totalBytesCopied, cancellationToken);
                        copySuccess = true;
                    }
                    
                    if (copySuccess)
                    {
                        result.SuccessfulCopies++;
                        filesCopied += (rom.HasRomFile ? 1 : 0) + rom.ChdFiles.Count;
                        totalBytesCopied += rom.TotalSize;
                    }
                    else
                    {
                        result.FailedCopies++;
                        result.FailedRoms.Add(new FailedCopy { RomName = rom.Name, Error = "Failed to reconstruct merged ROM" });
                    }

                    // Report progress after each ROM
                    var phaseMessage = copySuccess ? 
                        (romsetType == RomsetType.Merged && rom.IsClone ? $"Reconstructed {rom.Name}" : $"Completed {rom.Name}") :
                        $"Failed {rom.Name}";
                    
                    progress?.Report(new CopyProgress
                    {
                        Phase = phaseMessage,
                        Percentage = totalBytesToCopy > 0 ? (int)((double)totalBytesCopied / totalBytesToCopy * 100) : 0,
                        RomsCopied = i + 1,
                        TotalRoms = romsToCopy.Count,
                        FilesCopied = filesCopied,
                        TotalFiles = totalFilesToCopy,
                        TotalBytesCopied = totalBytesCopied,
                        TotalBytesToCopy = totalBytesToCopy
                    });

                    // Call the callback to notify that this ROM was copied
                    if (romCopiedCallback != null)
                    {
                        await romCopiedCallback(rom.Name);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCopies++;
                    result.FailedRoms.Add(new FailedCopy { RomName = rom.Name, Error = ex.Message });
                    
                    progress?.Report(new CopyProgress
                    {
                        Phase = $"Failed {rom.Name}",
                        Percentage = totalBytesToCopy > 0 ? (int)((double)totalBytesCopied / totalBytesToCopy * 100) : 0,
                        RomsCopied = i + 1,
                        TotalRoms = romsToCopy.Count,
                        FilesCopied = filesCopied,
                        TotalFiles = totalFilesToCopy,
                        TotalBytesCopied = totalBytesCopied,
                        TotalBytesToCopy = totalBytesToCopy
                    });
                }
            }

            progress?.Report(new CopyProgress
            {
                Phase = "Merged ROM copy operation completed",
                Percentage = 100,
                RomsCopied = romsToCopy.Count,
                TotalRoms = romsToCopy.Count,
                FilesCopied = filesCopied,
                TotalFiles = totalFilesToCopy,
                TotalBytesCopied = totalBytesCopied,
                TotalBytesToCopy = totalBytesToCopy
            });

            return result;
        }

        /// <summary>
        /// Expands the ROM list to include all required dependencies
        /// </summary>
        private async Task<List<ScannedRom>> ExpandWithDependenciesAsync(
            List<ScannedRom> selectedRoms,
            ParentRomResolver parentResolver,
            CancellationToken cancellationToken)
        {
            var expandedRoms = new List<ScannedRom>();
            var processedRoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                foreach (var rom in selectedRoms)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Add the ROM itself
                    if (!processedRoms.Contains(rom.Name))
                    {
                        expandedRoms.Add(rom);
                        processedRoms.Add(rom.Name);
                    }

                    // If this is a clone, add its parents
                    if (rom.IsClone)
                    {
                        foreach (var parentName in rom.AvailableParentFiles)
                        {
                            if (!processedRoms.Contains(parentName))
                            {
                                // Create a placeholder ScannedRom for the parent
                                var parentRom = new ScannedRom
                                {
                                    Name = parentName,
                                    IsSelected = false // Don't mark parents as selected
                                };
                                expandedRoms.Add(parentRom);
                                processedRoms.Add(parentName);
                            }
                        }
                    }
                }
            }, cancellationToken);

            return expandedRoms;
        }
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
            Func<string, Task>? romCopiedCallback = null,
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

            // Log the ROMs being copied
            var romNames = string.Join(", ", selectedRoms.Select(r => r.Name));
            _loggingService?.LogInfo($"Starting copy operation for {totalRoms} ROM(s): {romNames}");

            progress?.Report(new CopyProgress 
            { 
                Phase = "Starting copy operation...", 
                Percentage = 0,
                RomsCopied = 0,
                TotalRoms = totalRoms,
                FilesCopied = 0,
                TotalFiles = totalFilesToCopy,
                TotalBytesCopied = 0,
                TotalBytesToCopy = totalBytesToCopy
            });

            for (int i = 0; i < romsList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rom = romsList[i];
                
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
                    
                    // Update counters after successful copy
                    totalBytesCopied += romBytes;
                    filesCopied += GetRomFileCount(rom);
                    
                    // Report progress after copying this ROM
                    progress?.Report(new CopyProgress 
                    { 
                        Phase = $"Completed {rom.Name}", 
                        Percentage = totalBytesToCopy > 0 ? (int)((double)totalBytesCopied / totalBytesToCopy * 100) : 0,
                        CurrentRom = rom.Name,
                        RomsCopied = i + 1,
                        TotalRoms = totalRoms,
                        FilesCopied = filesCopied,
                        TotalFiles = totalFilesToCopy,
                        TotalBytesCopied = totalBytesCopied,
                        TotalBytesToCopy = totalBytesToCopy
                    });
                    
                    // Call the callback to notify that this ROM was copied
                    if (romCopiedCallback != null)
                    {
                        await romCopiedCallback(rom.Name);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCopies++;
                    result.FailedRoms.Add(new FailedCopy { RomName = rom.Name, Error = ex.Message });
                    
                    // Still update counters for failed ROMs to keep progress accurate
                    totalBytesCopied += romBytes;
                    filesCopied += GetRomFileCount(rom);
                    
                    // Report progress even for failed ROMs
                    progress?.Report(new CopyProgress 
                    { 
                        Phase = $"Failed {rom.Name}", 
                        Percentage = totalBytesToCopy > 0 ? (int)((double)totalBytesCopied / totalBytesToCopy * 100) : 0,
                        CurrentRom = rom.Name,
                        RomsCopied = i + 1,
                        TotalRoms = totalRoms,
                        FilesCopied = filesCopied,
                        TotalFiles = totalFilesToCopy,
                        TotalBytesCopied = totalBytesCopied,
                        TotalBytesToCopy = totalBytesToCopy
                    });
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
            var romBytesCopiedSoFar = totalBytesCopiedSoFar;
            
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
                        // Calculate the actual bytes copied so far:
                        // - Previous ROMs: totalBytesCopiedSoFar
                        // - Current ROM file: progress.bytesCopied (out of progress.totalBytes)
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
                
                // Update the bytes copied so far to include the ROM file
                romBytesCopiedSoFar += rom.RomFileSize;
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

                var chdBytesCopiedSoFar = 0L; // Track bytes copied for CHD files in this ROM

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
                                // Calculate the actual bytes copied so far:
                                // - Previous ROMs: totalBytesCopiedSoFar
                                // - Current ROM file: rom.RomFileSize (if it exists)
                                // - Previous CHD files in this ROM: chdBytesCopiedSoFar
                                // - Current CHD file: progress.bytesCopied (out of progress.totalBytes)
                                var currentBytesCopied = romBytesCopiedSoFar + chdBytesCopiedSoFar + progress.bytesCopied;
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
                        
                        // Update the CHD bytes counter after copying this CHD file
                        var chdFileInfo = new FileInfo(chdFile);
                        chdBytesCopiedSoFar += chdFileInfo.Length;
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

    /// <summary>
    /// Represents a ROM file required for reconstruction
    /// </summary>
    public class RequiredRomFile
    {
        public string FileName { get; set; } = string.Empty;
        public string SourceZipPath { get; set; } = string.Empty;
        public string SourceRomName { get; set; } = string.Empty;
    }
}
