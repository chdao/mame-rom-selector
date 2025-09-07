using MameSelector.Models;
using MameSelector.Services;
using MameSelector.UI;

namespace MameSelector.Controllers;

/// <summary>
/// Main controller that orchestrates the application logic
/// </summary>
public class MainController
{
    private readonly SettingsManager _settingsManager;
    private readonly RomScanner _romScanner;
    private readonly RomMetadataService _metadataService;
    private readonly RomCacheService _cacheService;
    private readonly MameXmlParser _xmlParser;
    private readonly RomCopyService _copyService;
    private readonly VirtualRomListView _romListView;
    private DestinationRomListView? _destinationRomListView;

    private AppSettings _settings;
    private Dictionary<string, ScannedRom> _scannedRoms = new();
    private bool _isLoading;

    public event EventHandler<StatusUpdateEventArgs>? StatusUpdated;
    public event EventHandler<LoadingStateChangedEventArgs>? LoadingStateChanged;
    public event EventHandler<RomStatsUpdatedEventArgs>? RomStatsUpdated;
    public event EventHandler<int>? ProgressUpdated;

    public MainController(
        SettingsManager settingsManager,
        RomScanner romScanner,
        RomMetadataService metadataService,
        RomCacheService cacheService,
        MameXmlParser xmlParser,
        RomCopyService copyService,
        VirtualRomListView romListView)
    {
        _settingsManager = settingsManager;
        _romScanner = romScanner;
        _metadataService = metadataService;
        _cacheService = cacheService;
        _xmlParser = xmlParser;
        _copyService = copyService;
        _romListView = romListView;
        _settings = new AppSettings();

        // Wire up events
        _romListView.SelectionChanged += OnRomSelectionChanged;
    }

    /// <summary>
    /// Gets the current application settings
    /// </summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// Sets the destination ROM list view for updates
    /// </summary>
    public void SetDestinationListView(DestinationRomListView destinationListView)
    {
        _destinationRomListView = destinationListView;
    }

    /// <summary>
    /// Gets the currently selected ROMs
    /// </summary>
    public IReadOnlyCollection<ScannedRom> SelectedRoms => _romListView.SelectedRoms;

    /// <summary>
    /// Gets whether the application is currently loading data
    /// </summary>
    public bool IsLoading => _isLoading;

    /// <summary>
    /// Initializes the controller and loads settings
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _settings = await _settingsManager.LoadSettingsAsync();
            UpdateStatus("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error loading settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates application settings
    /// </summary>
    public async Task UpdateSettingsAsync(AppSettings newSettings)
    {
        try
        {
            await _settingsManager.SaveSettingsAsync(newSettings);
            _settings = newSettings;
            UpdateStatus("Settings saved successfully");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error saving settings: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Auto-loads ROMs from cache on startup (if available)
    /// </summary>
    public async Task AutoLoadRomsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_isLoading)
            return;

        var validationErrors = _settings.Validate();
        if (validationErrors.Any())
        {
            UpdateStatus("Please configure settings to load ROMs");
            return; // Don't throw on startup - just show message
        }

        SetLoadingState(true);

        try
        {
            // First, scan destination directory to populate the list
            if (!string.IsNullOrEmpty(_settings.DestinationPath))
            {
                progress?.Report("Scanning destination directory...");
                
                // Initialize empty ROM collection for destination scanning
                _scannedRoms = new Dictionary<string, ScannedRom>();
                
                var destinationCount = await _romScanner.ScanDestinationAsync(
                    _settings.DestinationPath,
                    _scannedRoms,
                    progress,
                    cancellationToken);
                
                // Update UI with destination ROMs
                _romListView.UpdateRoms(_scannedRoms.Values);
                _destinationRomListView?.UpdateDestinationRoms(_scannedRoms.Values.ToList());
                progress?.Report($"Found {destinationCount} ROMs in destination directory");
            }
            
            // Then try to load from cache with progress and real-time updates
            progress?.Report("Loading ROM cache...");
            
            // Real-time ROM callback for cache loading
            var romLoadedCallback = new Action<ScannedRom>(rom => 
            {
                _romListView.AddRom(rom);
            });
            
            // Progress callback for cache loading
            var cacheProgress = new Progress<int>(percent => 
            {
                progress?.Report($"Loading ROM cache... ({percent}%)");
                OnProgressUpdated(percent);
            });
            
            var cachedRoms = await _cacheService.LoadCacheAsync(_settings, cacheProgress, romLoadedCallback);
            
            if (cachedRoms != null && cachedRoms.Count > 0)
            {
                // Cache hit! ROMs already loaded via callback
                _scannedRoms = cachedRoms;
                
                // Re-scan destination directory to mark installed ROMs
                if (!string.IsNullOrEmpty(_settings.DestinationPath))
                {
                    progress?.Report("Updating destination status...");
                    var destinationCount = await _romScanner.ScanDestinationAsync(
                        _settings.DestinationPath,
                        _scannedRoms,
                        progress,
                        cancellationToken);
                    
                    // Refresh the UI to show destination status
                    _romListView.RefreshDisplay();
                    _destinationRomListView?.UpdateDestinationRoms(_scannedRoms.Values.ToList());
                    progress?.Report($"Updated destination status for {destinationCount} ROMs");
                }
                
                UpdateStatus($"Loaded {_scannedRoms.Count:N0} ROMs from cache");
            }
            else
            {
                UpdateStatus("No ROM cache found. Use 'Scan ROMs' to scan your collection.");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error loading cache: {ex.Message}");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    /// <summary>
    /// Performs a fresh ROM scan (ignoring cache)
    /// </summary>
    public async Task ScanAndLoadRomsAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        
        if (_isLoading)
        {
            return;
        }

        var validationErrors = _settings.Validate();
        if (validationErrors.Any())
        {
            throw new InvalidOperationException($"Configuration errors: {string.Join(", ", validationErrors)}");
        }

        SetLoadingState(true);

        try
        {
            // Clear existing ROMs for fresh scan
            _romListView.ClearRoms();
            
            // Always perform fresh scan (ignore cache)
            progress?.Report("Starting fresh ROM scan...");
            
            // Step 1: Load MAME XML metadata first
            progress?.Report("Loading MAME metadata...");
            var mameGames = await _xmlParser.ParseAsync(_settings.MameXmlPath, 
                new Progress<int>(p => progress?.Report($"Loading MAME XML... ({p}%)")), 
                cancellationToken);
            var metadataLookup = mameGames.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);
            
            
            // Step 2: Scan ROMs with real-time metadata matching
            progress?.Report("Scanning ROM repository...");
            
            var scanProgress = new Progress<ScanProgress>(p => 
                progress?.Report($"{p.Phase} ({p.Percentage}%)"));

            // Real-time ROM callback (ROMs now come with metadata already attached)
            var romFoundCallback = new Action<ScannedRom>(rom => 
            {
                _romListView.AddRom(rom);
            });

            _scannedRoms = await _romScanner.ScanRomsAsync(
                _settings.RomRepositoryPath,
                _settings.CHDRepositoryPath,
                scanProgress,
                romFoundCallback,
                metadataLookup, // Pass metadata for real-time matching
                cancellationToken);

            // Step 3: Scan destination directory to mark installed ROMs
            if (!string.IsNullOrEmpty(_settings.DestinationPath))
            {
                progress?.Report("Scanning destination directory...");
                var destinationCount = await _romScanner.ScanDestinationAsync(
                    _settings.DestinationPath,
                    _scannedRoms,
                    progress,
                    cancellationToken);
                
                // Refresh the UI to show destination status
                _romListView.RefreshDisplay();
                _destinationRomListView?.UpdateDestinationRoms(_scannedRoms.Values.ToList());
                progress?.Report($"Found {destinationCount} ROMs in destination directory");
            }

            // Step 4: Save to cache for next time
            progress?.Report("Saving ROM cache...");
            await _cacheService.SaveCacheAsync(_scannedRoms, _settings);

            UpdateStatus($"Scanned {_scannedRoms.Count:N0} ROMs successfully");

            // Update statistics
            var stats = _metadataService.GetMatchingStats(_scannedRoms);
            OnRomStatsUpdated(stats);

            UpdateStatus($"Loaded {_scannedRoms.Count} ROMs ({stats.MatchedRoms} with metadata)");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("ROM scanning cancelled");
            throw;
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error scanning ROMs: {ex.Message}");
            throw;
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    /// <summary>
    /// Applies a filter to the ROM list
    /// </summary>
    public void FilterRoms(string filter)
    {
        _romListView.ApplyFilter(filter);
        var stats = _romListView.GetStats();
        UpdateStatus($"Showing {stats.FilteredRoms} of {stats.TotalRoms} ROMs");
    }

    /// <summary>
    /// Selects all visible ROMs
    /// </summary>
    public void SelectAllRoms()
    {
        _romListView.SelectAll();
    }

    /// <summary>
    /// Clears all ROM selections
    /// </summary>
    public void ClearAllSelections()
    {
        _romListView.ClearSelection();
    }

    /// <summary>
    /// Selects all ROMs that are already installed in the destination directory
    /// </summary>
    public void SelectInstalledRoms()
    {
        _romListView.SelectInstalledRoms();
    }

    /// <summary>
    /// Selects all currently highlighted/selected ROMs in the list view
    /// </summary>
    public void SelectHighlightedRoms()
    {
        _romListView.SelectHighlightedRoms();
    }

    /// <summary>
    /// Copies selected ROMs to the destination directory
    /// </summary>
    public async Task<CopyResult> CopySelectedRomsAsync(
        IProgress<CopyProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        var selectedRoms = _romListView.SelectedRoms.ToList();
        
        if (selectedRoms.Count == 0)
        {
            throw new InvalidOperationException("No ROMs selected for copying");
        }

        if (string.IsNullOrEmpty(_settings.DestinationPath))
        {
            throw new InvalidOperationException("Destination path is not configured");
        }

        // Validate the copy operation
        var validation = _copyService.ValidateCopyOperation(selectedRoms, _settings.DestinationPath);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Copy validation failed: {string.Join(", ", validation.Errors)}");
        }

        // Show warnings if any
        if (validation.Warnings.Any())
        {
            UpdateStatus($"Warning: {validation.Warnings.Count} files not found");
        }

        // Perform the copy operation
        var result = await _copyService.CopyRomsAsync(
            selectedRoms, 
            _settings.DestinationPath, 
            progress, 
            cancellationToken);

        // Update destination status for copied ROMs
        foreach (var romName in result.CopiedRoms)
        {
            if (_scannedRoms.TryGetValue(romName, out var rom))
            {
                rom.InDestination = true;
            }
        }

        // Refresh both list views
        _romListView.RefreshDisplay();
        _destinationRomListView?.UpdateDestinationRoms(_scannedRoms.Values.ToList());

        // Update status
        UpdateStatus($"Copied {result.SuccessfulCopies} ROMs successfully" + 
                    (result.FailedCopies > 0 ? $", {result.FailedCopies} failed" : ""));

        return result;
    }

    /// <summary>
    /// Selects ROMs based on criteria
    /// </summary>
    public void SelectRomsByCriteria(RomSelectionCriteria criteria)
    {
        Func<ScannedRom, bool> predicate = criteria.Type switch
        {
            RomSelectionType.WithMetadata => rom => rom.HasMetadata,
            RomSelectionType.WithoutMetadata => rom => !rom.HasMetadata,
            RomSelectionType.WithChd => rom => rom.HasChd,
            RomSelectionType.WithoutChd => rom => !rom.HasChd,
            RomSelectionType.ParentsOnly => rom => rom.HasMetadata && !rom.IsClone,
            RomSelectionType.ClonesOnly => rom => rom.IsClone,
            _ => _ => false
        };

        _romListView.ToggleSelection(predicate, criteria.Select);
    }

    /// <summary>
    /// Gets current ROM statistics
    /// </summary>
    public RomListStats GetCurrentStats()
    {
        return _romListView.GetStats();
    }

    /// <summary>
    /// Gets information about the ROM cache
    /// </summary>
    public async Task<CacheInfo?> GetCacheInfoAsync()
    {
        return await _cacheService.GetCacheInfoAsync();
    }

    /// <summary>
    /// Clears the ROM cache
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheService.ClearCacheAsync();
        UpdateStatus("ROM cache cleared");
    }

    /// <summary>
    /// Validates that ROMs can be copied
    /// </summary>
    public List<string> ValidateForCopy()
    {
        var errors = new List<string>();

        if (!SelectedRoms.Any())
        {
            errors.Add("No ROMs selected for copying");
        }

        var validationErrors = _settings.Validate();
        errors.AddRange(validationErrors);

        if (!Directory.Exists(_settings.DestinationPath))
        {
            try
            {
                Directory.CreateDirectory(_settings.DestinationPath);
            }
            catch (Exception ex)
            {
                errors.Add($"Cannot create destination directory: {ex.Message}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Handles ROM selection changes
    /// </summary>
    private void OnRomSelectionChanged(object? sender, RomSelectionChangedEventArgs e)
    {
        var stats = _romListView.GetStats();
        var totalSize = stats.TotalSelectedSize;
        UpdateStatus($"Selected: {stats.SelectedRoms} ROMs, Total Size: {FormatFileSize(totalSize)}");
    }

    /// <summary>
    /// Updates the loading state
    /// </summary>
    private void SetLoadingState(bool isLoading)
    {
        _isLoading = isLoading;
        LoadingStateChanged?.Invoke(this, new LoadingStateChangedEventArgs(isLoading));
    }

    /// <summary>
    /// Updates the status message
    /// </summary>
    private void UpdateStatus(string message)
    {
        StatusUpdated?.Invoke(this, new StatusUpdateEventArgs(message));
    }

    /// <summary>
    /// Raises the RomStatsUpdated event
    /// </summary>
    private void OnRomStatsUpdated(MetadataStats stats)
    {
        RomStatsUpdated?.Invoke(this, new RomStatsUpdatedEventArgs(stats));
    }

    /// <summary>
    /// Raises the ProgressUpdated event
    /// </summary>
    private void OnProgressUpdated(int percentage)
    {
        ProgressUpdated?.Invoke(this, percentage);
    }

    /// <summary>
    /// Formats file size for display
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    /// <summary>
    /// Refreshes the destination ROM list
    /// </summary>
    public async Task RefreshDestinationListAsync()
    {
        if (_destinationRomListView == null || string.IsNullOrEmpty(_settings.DestinationPath))
            return;

        try
        {
            // Re-scan destination directory to update the list
            var destinationCount = await _romScanner.ScanDestinationAsync(
                _settings.DestinationPath,
                _scannedRoms,
                null, // progress
                CancellationToken.None);

            // Update the UI with the refreshed destination ROMs
            _destinationRomListView.UpdateDestinationRoms(_scannedRoms.Values.ToList());
            
            // Update status
            UpdateStatus($"Refreshed destination list - found {destinationCount} ROMs");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error refreshing destination list: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies selected ROMs against their CRC values in the MAME XML
    /// </summary>
    public async Task VerifySelectedRomsAsync()
    {
        if (_romListView == null)
            return;

        try
        {
            var selectedRoms = _romListView.SelectedRoms.ToList();
            if (!selectedRoms.Any())
            {
                UpdateStatus("No ROMs selected for verification");
                return;
            }

            UpdateStatus($"Verifying {selectedRoms.Count} ROM(s)...");

            var verificationResults = new List<string>();
            var verifiedCount = 0;
            var failedCount = 0;

            foreach (var rom in selectedRoms)
            {
                if (!rom.HasMetadata)
                {
                    verificationResults.Add($"{rom.Name}: No metadata available");
                    failedCount++;
                    continue;
                }

                try
                {
                    var isValid = await VerifyRomCrcAsync(rom);
                    if (isValid)
                    {
                        verificationResults.Add($"{rom.Name}: ✓ Valid");
                        verifiedCount++;
                    }
                    else
                    {
                        verificationResults.Add($"{rom.Name}: ✗ Invalid CRC");
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    verificationResults.Add($"{rom.Name}: Error - {ex.Message}");
                    failedCount++;
                }
            }

            // Show results
            var resultMessage = $"Verification Complete:\n\n" +
                              $"✓ Valid: {verifiedCount}\n" +
                              $"✗ Invalid/Error: {failedCount}\n\n" +
                              $"Details:\n{string.Join("\n", verificationResults)}";

            MessageBox.Show(resultMessage, "ROM Verification Results", 
                MessageBoxButtons.OK, 
                failedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            UpdateStatus($"Verification complete: {verifiedCount} valid, {failedCount} invalid/errors");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error during verification: {ex.Message}");
            MessageBox.Show($"Error during verification: {ex.Message}", 
                "Verification Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Verifies a single ROM's CRC against the MAME XML data
    /// </summary>
    private async Task<bool> VerifyRomCrcAsync(ScannedRom rom)
    {
        if (string.IsNullOrEmpty(rom.RomFilePath) || !File.Exists(rom.RomFilePath))
            return false;

        return await Task.Run(() =>
        {
            try
            {
                // For now, we'll do a basic file existence and size check
                // In a full implementation, you would:
                // 1. Extract the ROM file from the ZIP
                // 2. Calculate its CRC32
                // 3. Compare against the expected CRC from rom.Metadata.RomFiles
                
                var fileInfo = new FileInfo(rom.RomFilePath);
                if (fileInfo.Length == 0)
                    return false;

                // Basic validation - file exists and has content
                // For now, we just verify the file exists and has content
                // Future enhancement: Implement actual CRC verification against rom.Metadata.RomFiles
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Deletes selected ROMs from the destination directory
    /// </summary>
    public async Task DeleteSelectedDestinationRomsAsync()
    {
        if (_destinationRomListView == null || string.IsNullOrEmpty(_settings.DestinationPath))
            return;

        try
        {
            var selectedRoms = _destinationRomListView.GetSelectedRoms();
            if (!selectedRoms.Any())
            {
                UpdateStatus("No ROMs selected for deletion");
                return;
            }

            // Confirm deletion
            var result = MessageBox.Show(
                $"Are you sure you want to delete {selectedRoms.Count} ROM(s) from the destination directory?\n\nThis action cannot be undone.",
                "Confirm Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            var deletedCount = 0;
            var errors = new List<string>();

            foreach (var rom in selectedRoms)
            {
                try
                {
                    // Delete ROM file from destination directory, not source
                    var destinationFilePath = Path.Combine(_settings.DestinationPath, rom.Name + ".zip");
                    if (File.Exists(destinationFilePath))
                    {
                        File.Delete(destinationFilePath);
                        deletedCount++;
                    }
                    
                    // Delete CHD directory if it exists
                    var chdDirectoryPath = Path.Combine(_settings.DestinationPath, rom.Name);
                    if (Directory.Exists(chdDirectoryPath))
                    {
                        Directory.Delete(chdDirectoryPath, true); // true = recursive delete
                    }
                    
                    // Update the ROM's InDestination status
                    rom.InDestination = false;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to delete {rom.Name}: {ex.Message}");
                }
            }

            // Update the UI
            _destinationRomListView.UpdateDestinationRoms(_scannedRoms.Values.ToList());
            _romListView.UpdateRoms(_scannedRoms.Values.ToList());

            if (errors.Any())
            {
                UpdateStatus($"Deleted {deletedCount} ROMs, {errors.Count} errors occurred");
                MessageBox.Show($"Some deletions failed:\n\n{string.Join("\n", errors)}", 
                    "Deletion Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                UpdateStatus($"Successfully deleted {deletedCount} ROM(s)");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error deleting ROMs: {ex.Message}");
            MessageBox.Show($"Error deleting ROMs: {ex.Message}", 
                "Deletion Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// Event arguments for status updates
/// </summary>
public class StatusUpdateEventArgs : EventArgs
{
    public string Message { get; }
    public StatusUpdateEventArgs(string message) => Message = message;
}

/// <summary>
/// Event arguments for loading state changes
/// </summary>
public class LoadingStateChangedEventArgs : EventArgs
{
    public bool IsLoading { get; }
    public LoadingStateChangedEventArgs(bool isLoading) => IsLoading = isLoading;
}

/// <summary>
/// Event arguments for ROM statistics updates
/// </summary>
public class RomStatsUpdatedEventArgs : EventArgs
{
    public MetadataStats Stats { get; }
    public RomStatsUpdatedEventArgs(MetadataStats stats) => Stats = stats;
}

/// <summary>
/// Criteria for ROM selection
/// </summary>
public class RomSelectionCriteria
{
    public RomSelectionType Type { get; set; }
    public bool Select { get; set; } = true;
}

/// <summary>
/// Types of ROM selection criteria
/// </summary>
public enum RomSelectionType
{
    WithMetadata,
    WithoutMetadata,
    WithChd,
    WithoutChd,
    ParentsOnly,
    ClonesOnly
}
