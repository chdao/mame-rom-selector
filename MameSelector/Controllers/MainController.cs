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
    private MainForm? _mainForm;

    private AppSettings _settings;
    private Dictionary<string, ScannedRom> _scannedRoms = new();
    private bool _isLoading;
    private int _totalChdDirectories = 0;

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
    /// Sets the MainForm reference for status updates
    /// </summary>
    public void SetMainForm(MainForm mainForm)
    {
        _mainForm = mainForm;
    }

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
            
            // Update cache service path based on portable mode setting
            _cacheService.UpdateCachePath(_settings.PortableMode);
            
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
            var oldSettings = _settings;
            var portableModeChanged = oldSettings.PortableMode != newSettings.PortableMode;
            var showBiosAndDevicesChanged = oldSettings.ShowBiosAndDevices != newSettings.ShowBiosAndDevices;
            
            _settings = newSettings;
            
            // Update cache service path if portable mode changed
            if (portableModeChanged)
            {
                _cacheService.UpdateCachePath(_settings.PortableMode);
                
                // If switching to portable mode, try to migrate cache from AppData
                if (_settings.PortableMode)
                {
                    await MigrateCacheToPortableAsync();
                }
                // If switching from portable mode, try to migrate cache to AppData
                else
                {
                    await MigrateCacheToAppDataAsync();
                }
            }
            
            // Refresh filter if show BIOS and devices setting changed
            if (showBiosAndDevicesChanged)
            {
                RefreshFilter();
            }
            
            // Save settings
            await _settingsManager.SaveSettingsAsync(_settings);
            UpdateStatus("Settings saved successfully");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error saving settings: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Migrates cache from AppData to portable location
    /// </summary>
    private Task MigrateCacheToPortableAsync()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDataCachePath = Path.Combine(appDataPath, "MameSelector", "rom_cache.json");
            
            if (File.Exists(appDataCachePath))
            {
                var exeDir = AppContext.BaseDirectory;
                var portableCachePath = Path.Combine(exeDir, "rom_cache.json");
                
                // Copy cache to portable location
                File.Copy(appDataCachePath, portableCachePath, true);
                UpdateStatus("Cache migrated to portable location");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Warning: Could not migrate cache to portable location: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Migrates cache from portable location to AppData
    /// </summary>
    private Task MigrateCacheToAppDataAsync()
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var portableCachePath = Path.Combine(exeDir, "rom_cache.json");
            
            if (File.Exists(portableCachePath))
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var appDataFolder = Path.Combine(appDataPath, "MameSelector");
                Directory.CreateDirectory(appDataFolder);
                var appDataCachePath = Path.Combine(appDataFolder, "rom_cache.json");
                
                // Copy cache to AppData location
                File.Copy(portableCachePath, appDataCachePath, true);
                UpdateStatus("Cache migrated to AppData location");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Warning: Could not migrate cache to AppData location: {ex.Message}");
        }
        return Task.CompletedTask;
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
            
            // Set scanning state to prevent UI flickering during cache loading
            _romListView.SetScanningState(true);
            
            // Progress callback for cache loading
            var cacheProgress = new Progress<int>(percent => 
            {
                progress?.Report($"Loading ROM cache... ({percent}%)");
                OnProgressUpdated(percent);
            });
            
            // No individual ROM callbacks - process everything in background
            var cachedRoms = await _cacheService.LoadCacheAsync(_settings, cacheProgress, null);
            
            if (cachedRoms != null && cachedRoms.Count > 0)
            {
                // Cache hit! Load all ROMs at once
                _scannedRoms = cachedRoms;
                
                // Update status bar counts from cached ROMs
                _mainForm?.UpdateRomCount(cachedRoms.Count);
                var chdCount = cachedRoms.Values.Count(r => r.ChdFiles?.Count > 0);
                
                // Get the stored total CHD directory count from cache, or fall back to counting ROMs with CHDs
                var totalChdDirectories = await _cacheService.GetTotalChdDirectoriesFromCacheAsync();
                _mainForm?.LogConsole($"Cache load: Retrieved TotalChdDirectories from cache: {totalChdDirectories}");
                
                if (totalChdDirectories == 0)
                {
                    totalChdDirectories = cachedRoms.Values.Count(r => r.ChdFiles?.Count > 0);
                    _mainForm?.LogConsole($"Cache load: Fallback to counting ROMs with CHDs: {totalChdDirectories}");
                }
                _mainForm?.UpdateChdCount(totalChdDirectories);
                
                // Debug: Log CHD count calculation
                _mainForm?.LogConsole($"Cache load: Total ROMs: {cachedRoms.Count}, CHDs: {chdCount}");
                
                
                // Bulk update the UI with all cached ROMs at once
                _romListView.UpdateRoms(cachedRoms.Values);
                
                // Clear scanning state after bulk update
                _romListView.SetScanningState(false);
                
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
                    
                    // Update installed count in status bar
                    _mainForm?.UpdateInstalledCount(destinationCount);
                    
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
            // Re-enable UI updates when cache loading completes
            _romListView.SetScanningState(false);
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
            _scannedRoms = new Dictionary<string, ScannedRom>();
            
            // Set scanning state to disable UI updates during scan
            _romListView.SetScanningState(true);
            
            // Always perform fresh scan (ignore cache)
            progress?.Report("Starting fresh ROM scan...");
            
            // Step 1: Load MAME XML metadata first (run on background thread)
            progress?.Report("Loading MAME metadata...");
            _mainForm?.LogConsole("Loading MAME XML metadata...");
            var mameGames = await Task.Run(() => _xmlParser.ParseAsync(_settings.MameXmlPath, 
                new Progress<int>(p => progress?.Report($"Loading MAME XML... ({p}%)")), 
                cancellationToken), cancellationToken);
            var metadataLookup = mameGames.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);
            
            
            // Step 2: Scan ROMs with real-time metadata matching
            progress?.Report("Scanning ROM repository...");
            _mainForm?.LogConsole("Starting ROM repository scan...");
            
            var scanProgress = new Progress<ScanProgress>(p => 
            {
                progress?.Report($"{p.Phase} ({p.Percentage}%)");
                
                // Log important messages to console
                if (p.Phase.Contains("Unmatched CHD directories") || 
                    p.Phase.Contains("Sample unmatched CHD dirs") ||
                    p.Phase.Contains("CHD Debug:") ||
                    p.Phase.Contains("All CHD directories have corresponding ROMs"))
                {
                    _mainForm?.LogConsole(p.Phase);
                }
                
                // Update counts based on scanning phase
                if (p.Phase.Contains("ROM files") || p.Phase.Contains("Scanning ROMs and CHDs"))
                {
                    // Update ROM count from ItemsProcessed (which represents ROM count during scanning)
                    _mainForm?.UpdateRomCount(p.ItemsProcessed);
                    
                    // Don't update CHD count during scanning - it will be updated at the end with the correct total
                }
                else if (p.Phase.Contains("Scan Complete"))
                {
                    // Final update with all counts
                    _mainForm?.UpdateRomCount(p.ItemsProcessed);
                    Console.WriteLine($"DEBUG: MainController calling UpdateChdCount with TotalChdDirectories: {p.TotalChdDirectories}");
                    Console.WriteLine($"DEBUG: Scan Complete phase: '{p.Phase}'");
                    _mainForm?.UpdateChdCount(p.TotalChdDirectories); // Use total CHD directories, not ROMs with CHDs
                    _totalChdDirectories = p.TotalChdDirectories; // Store for cache saving
                }
            });

            // Silent ROM callback for scanning - no UI updates during scanning
            var romFoundCallback = new Action<ScannedRom>(rom => 
            {
                _romListView.AddRomSilent(rom);
            });

            _scannedRoms = await Task.Run(() => _romScanner.ScanRomsAsync(
                _settings.RomRepositoryPath,
                _settings.CHDRepositoryPath,
                scanProgress,
                romFoundCallback, // Use silent callback like cache loading
                metadataLookup,
                cancellationToken), cancellationToken);
            
            _mainForm?.LogConsole($"ROM repository scan completed. Found {_scannedRoms.Count} ROMs");
            
            // Clear scanning state and trigger final UI update
            _romListView.SetScanningState(false);
            
            // Ensure all ROMs are visible in the UI (this can take time with large collections)
            progress?.Report($"Updating UI with {_scannedRoms.Count:N0} ROMs... (70%)");
            _romListView.UpdateRoms(_scannedRoms.Values);
            
            // Update status bar counts from scanned ROMs
            _mainForm?.UpdateRomCount(_scannedRoms.Count);
            // Note: CHD count is already updated in the scanProgress callback above
            
            progress?.Report($"UI update complete (75%)");

            // Step 3: Scan destination directory to mark installed ROMs
            if (!string.IsNullOrEmpty(_settings.DestinationPath))
            {
                progress?.Report("Scanning destination directory... (75%)");
                var destinationCount = await _romScanner.ScanDestinationAsync(
                    _settings.DestinationPath,
                    _scannedRoms,
                    progress,
                    cancellationToken);
                
                // Refresh the UI to show destination status
                _romListView.RefreshDisplay();
                _destinationRomListView?.UpdateDestinationRoms(_scannedRoms.Values.ToList());
                
                // Update installed count in status bar
                _mainForm?.UpdateInstalledCount(destinationCount);
                
                progress?.Report($"Found {destinationCount} ROMs in destination directory (80%)");
            }
            else
            {
                progress?.Report("Skipping destination scan (no destination path) (75%)");
            }

            // Step 4: Save to cache for next time
            progress?.Report("Saving ROM cache... (85%)");
            await _cacheService.SaveCacheAsync(_scannedRoms, _settings, _totalChdDirectories, progress);

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
            // Re-enable UI updates when scanning completes
            _romListView.SetScanningState(false);
            SetLoadingState(false);
        }
    }

    /// <summary>
    /// Applies a filter to the ROM list
    /// </summary>
    public void FilterRoms(string filter)
    {
        _romListView.ApplyFilter(filter, _settings.ShowBiosAndDevices);
        var stats = _romListView.GetStats();
        UpdateStatus($"Showing {stats.FilteredRoms} of {stats.TotalRoms} ROMs");
    }

    /// <summary>
    /// Refreshes the current filter with updated settings
    /// </summary>
    public void RefreshFilter()
    {
        FilterRoms(_romListView.GetCurrentFilter());
    }

    /// <summary>
    /// Exports the current ROM selection to a file
    /// </summary>
    public async Task ExportSelectionAsync(IEnumerable<ScannedRom> selectedRoms, string filePath)
    {
        try
        {
            var selection = new RomSelection
            {
                Name = $"ROM Selection - {DateTime.Now:yyyy-MM-dd HH:mm}",
                Description = $"Exported selection containing {selectedRoms.Count()} ROMs",
                RomNames = selectedRoms.Select(r => r.Name).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(selection, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to export selection: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Imports a ROM selection from a file and selects the matching ROMs
    /// </summary>
    public async Task<List<ScannedRom>> ImportSelectionAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var selection = System.Text.Json.JsonSerializer.Deserialize<RomSelection>(json);

            if (selection == null)
                throw new InvalidOperationException("Invalid selection file format");

            var importedRoms = new List<ScannedRom>();
            var allRoms = _romListView.GetAllRoms();

            // Find matching ROMs and select them
            foreach (var romName in selection.RomNames)
            {
                var matchingRom = allRoms.FirstOrDefault(r => 
                    string.Equals(r.Name, romName, StringComparison.OrdinalIgnoreCase));
                
                if (matchingRom != null)
                {
                    matchingRom.IsSelected = true;
                    importedRoms.Add(matchingRom);
                }
            }

            // Update the UI to reflect the new selections
            _romListView.RefreshSelection();

            return importedRoms;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to import selection: {ex.Message}", ex);
        }
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
        
        // Update selected count in status bar
        _mainForm?.UpdateSelectedCount(stats.SelectedRoms);
        
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
        Console.WriteLine("DEBUG: VerifySelectedRomsAsync called");
        
        if (_romListView == null)
        {
            Console.WriteLine("DEBUG: _romListView is null, returning");
            return;
        }

        try
        {
            var selectedRoms = _romListView.SelectedRoms.ToList();
            Console.WriteLine($"DEBUG: Found {selectedRoms.Count} selected ROMs");
            
            if (!selectedRoms.Any())
            {
                Console.WriteLine("DEBUG: No ROMs selected, showing message");
                UpdateStatus("No ROMs selected for verification");
                return;
            }

            Console.WriteLine($"DEBUG: Starting verification of {selectedRoms.Count} ROM(s)");
            UpdateStatus($"Verifying {selectedRoms.Count} ROM(s)...");
            
            // Show progress bar
            _mainForm?.StartProgress($"Verifying {selectedRoms.Count} ROM(s)...");

            var verificationResults = new List<string>();
            var verifiedCount = 0;
            var failedCount = 0;

            for (int i = 0; i < selectedRoms.Count; i++)
            {
                var rom = selectedRoms[i];
                var progress = (int)((double)(i + 1) / selectedRoms.Count * 100);
                _mainForm?.UpdateProgress($"Verifying {rom.Name}... ({i + 1}/{selectedRoms.Count})", progress);
                Console.WriteLine($"DEBUG: Verifying ROM: {rom.Name}");
                Console.WriteLine($"DEBUG: ROM file path: {rom.RomFilePath}");
                Console.WriteLine($"DEBUG: ROM has metadata: {rom.HasMetadata}");
                
                if (!rom.HasMetadata)
                {
                    Console.WriteLine($"DEBUG: ROM {rom.Name} has no metadata, skipping");
                    verificationResults.Add($"{rom.Name}: No metadata available");
                    failedCount++;
                    continue;
                }

                try
                {
                    Console.WriteLine($"DEBUG: Calling VerifyRomCrcAsync for {rom.Name}");
                    var isValid = await VerifyRomCrcAsync(rom);
                    Console.WriteLine($"DEBUG: Verification result for {rom.Name}: {isValid}");
                    
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
                    Console.WriteLine($"DEBUG: Exception during verification of {rom.Name}: {ex.Message}");
                    verificationResults.Add($"{rom.Name}: Error - {ex.Message}");
                    failedCount++;
                }
            }

            // Show results
            Console.WriteLine($"DEBUG: Verification complete - Valid: {verifiedCount}, Failed: {failedCount}");
            
            // Hide progress bar and show final status
            _mainForm?.CompleteProgress("Verification complete");
            
            Console.WriteLine($"DEBUG: Showing results dialog");
            
            // Create a more manageable results message
            var summary = $"Verification Complete:\n\n✓ Valid: {verifiedCount}\n✗ Invalid/Error: {failedCount}\n\n";
            
            // For large lists, show only summary and first few results
            string detailsText;
            if (verificationResults.Count <= 10)
            {
                detailsText = "Details:\n" + string.Join("\n", verificationResults);
            }
            else
            {
                var firstFew = verificationResults.Take(5);
                detailsText = $"Details (showing first 5 of {verificationResults.Count}):\n" + 
                             string.Join("\n", firstFew) + 
                             $"\n\n... and {verificationResults.Count - 5} more results";
            }
            
            var resultMessage = summary + detailsText;

            MessageBox.Show(resultMessage, "ROM Verification Results", 
                MessageBoxButtons.OK, 
                failedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            UpdateStatus($"Verification complete: {verifiedCount} valid, {failedCount} invalid/errors");
        }
        catch (Exception ex)
        {
            // Hide progress bar on error
            _mainForm?.CompleteProgress("Verification failed");
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
        Console.WriteLine($"DEBUG: VerifyRomCrcAsync called for {rom.Name}");
        
        if (string.IsNullOrEmpty(rom.RomFilePath) || !File.Exists(rom.RomFilePath))
        {
            Console.WriteLine($"DEBUG: ROM file path is null/empty or doesn't exist: {rom.RomFilePath}");
            return false;
        }

        if (!rom.HasMetadata)
        {
            Console.WriteLine($"DEBUG: ROM {rom.Name} has no metadata");
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                Console.WriteLine($"DEBUG: Processing ROM file: {rom.RomFilePath}");
                
                // Check if it's a ZIP file
                if (Path.GetExtension(rom.RomFilePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"DEBUG: ROM {rom.Name} is a ZIP file, calling VerifyZipRomCrc");
                    var result = VerifyZipRomCrc(rom);
                    Console.WriteLine($"DEBUG: VerifyZipRomCrc result for {rom.Name}: {result}");
                    return result;
                }
                else
                {
                    Console.WriteLine($"DEBUG: ROM {rom.Name} is not a ZIP file, verifying single file");
                    // For non-ZIP files, verify the single file
                    var expectedRomFile = rom.Metadata.RomFiles.FirstOrDefault();
                    if (expectedRomFile == null || string.IsNullOrEmpty(expectedRomFile.CRC))
                    {
                        Console.WriteLine($"DEBUG: No expected ROM file or CRC for {rom.Name}");
                        return false;
                    }

                    Console.WriteLine($"DEBUG: Expected CRC for {rom.Name}: {expectedRomFile.CRC}");
                    var actualCrc = CalculateFileCrc32(rom.RomFilePath);
                    Console.WriteLine($"DEBUG: Actual CRC for {rom.Name}: {actualCrc}");
                    
                    var matches = string.Equals(actualCrc, expectedRomFile.CRC, StringComparison.OrdinalIgnoreCase);
                    Console.WriteLine($"DEBUG: CRC match for {rom.Name}: {matches}");
                    return matches;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception in VerifyRomCrcAsync for {rom.Name}: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Verifies CRC of files inside a ZIP ROM archive
    /// </summary>
    private bool VerifyZipRomCrc(ScannedRom rom)
    {
        Console.WriteLine($"DEBUG: VerifyZipRomCrc called for {rom.Name}");
        
        try
        {
            Console.WriteLine($"DEBUG: Opening ZIP archive: {rom.RomFilePath}");
            using var archive = System.IO.Compression.ZipFile.OpenRead(rom.RomFilePath);
            
            // Create a lookup dictionary for expected CRCs by filename
            var expectedCrcs = rom.Metadata.RomFiles.ToDictionary(
                rf => rf.Name, 
                rf => rf.CRC, 
                StringComparer.OrdinalIgnoreCase
            );
            
            Console.WriteLine($"DEBUG: Expected CRCs for {rom.Name}: {expectedCrcs.Count} files");
            foreach (var kvp in expectedCrcs)
            {
                Console.WriteLine($"DEBUG: Expected {kvp.Key}: {kvp.Value}");
            }

            // Verify each file in the ZIP
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) // Skip directory entries
                {
                    Console.WriteLine($"DEBUG: Skipping directory entry: {entry.FullName}");
                    continue;
                }

                Console.WriteLine($"DEBUG: Processing ZIP entry: {entry.Name}");

                // Check if we have expected CRC for this file
                if (!expectedCrcs.TryGetValue(entry.Name, out var expectedCrc) || string.IsNullOrEmpty(expectedCrc))
                {
                    Console.WriteLine($"DEBUG: No expected CRC for {entry.Name}, skipping");
                    continue;
                }

                Console.WriteLine($"DEBUG: Expected CRC for {entry.Name}: {expectedCrc}");

                // Calculate CRC of the file inside the ZIP
                using var entryStream = entry.Open();
                var actualCrc = CalculateStreamCrc32(entryStream);
                Console.WriteLine($"DEBUG: Actual CRC for {entry.Name}: {actualCrc}");
                
                // Compare with expected CRC
                var matches = string.Equals(actualCrc, expectedCrc, StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"DEBUG: CRC match for {entry.Name}: {matches}");
                
                if (!matches)
                {
                    Console.WriteLine($"DEBUG: CRC mismatch for {entry.Name}, returning false");
                    return false; // At least one file doesn't match
                }
            }

            Console.WriteLine($"DEBUG: All files in {rom.Name} match, returning true");
            return true; // All files match
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Exception in VerifyZipRomCrc for {rom.Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Calculates CRC32 of a stream
    /// </summary>
    private string CalculateStreamCrc32(Stream stream)
    {
        var buffer = new byte[8192];
        uint crc = 0xFFFFFFFF;
        int bytesRead;
        
        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                crc = Crc32Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            }
        }
        
        return (crc ^ 0xFFFFFFFF).ToString("X8");
    }

    /// <summary>
    /// Calculates CRC32 of a file
    /// </summary>
    private string CalculateFileCrc32(string filePath)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        
        var buffer = new byte[8192];
        uint crc = 0xFFFFFFFF;
        int bytesRead;
        
        while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                crc = Crc32Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            }
        }
        
        return (crc ^ 0xFFFFFFFF).ToString("X8");
    }

    /// <summary>
    /// CRC32 lookup table
    /// </summary>
    private static readonly uint[] Crc32Table = {
        0x00000000, 0x77073096, 0xEE0E612C, 0x990951BA, 0x076DC419, 0x706AF48F, 0xE963A535, 0x9E6495A3,
        0x0EDB8832, 0x79DCB8A4, 0xE0D5E91E, 0x97D2D988, 0x09B64C2B, 0x7EB17CBD, 0xE7B82D07, 0x90BF1D91,
        0x1DB71064, 0x6AB020F2, 0xF3B97148, 0x84BE41DE, 0x1ADAD47D, 0x6DDDE4EB, 0xF4D4B551, 0x83D385C7,
        0x136C9856, 0x646BA8C0, 0xFD62F97A, 0x8A65C9EC, 0x14015C4F, 0x63066CD9, 0xFA0F3D63, 0x8D080DF5,
        0x3B6E20C8, 0x4C69105E, 0xD56041E4, 0xA2677172, 0x3C03E4D1, 0x4B04D447, 0xD20D85FD, 0xA50AB56B,
        0x35B5A8FA, 0x42B2986C, 0xDBBBC9D6, 0xACBCF940, 0x32D86CE3, 0x45DF5C75, 0xDCD60DCF, 0xABD13D59,
        0x26D930AC, 0x51DE003A, 0xC8D75180, 0xBFD06116, 0x21B4F4B5, 0x56B3C423, 0xCFBA9599, 0xB8BDA50F,
        0x2802B89E, 0x5F058808, 0xC60CD9B2, 0xB10BE924, 0x2F6F7C87, 0x58684C11, 0xC1611DAB, 0xB6662D3D,
        0x76DC4190, 0x01DB7106, 0x98D220BC, 0xEFD5102A, 0x71B18589, 0x06B6B51F, 0x9FBFE4A5, 0xE8B8D433,
        0x7807C9A2, 0x0F00F934, 0x9609A88E, 0xE10E9818, 0x7F6A0DBB, 0x086D3D2D, 0x91646C97, 0xE6635C01,
        0x6B6B51F4, 0x1C6C6162, 0x856530D8, 0xF262004E, 0x6C0695ED, 0x1B01A57B, 0x8208F4C1, 0xF50FC457,
        0x65B0D9C6, 0x12B7E950, 0x8BBEB8EA, 0xFCB9887C, 0x62DD1DDF, 0x15DA2D49, 0x8CD37CF3, 0xFBD44C65,
        0x4DB26158, 0x3AB551CE, 0xA3BC0074, 0xD4BB30E2, 0x4ADFA541, 0x3DD895D7, 0xA4D1C46D, 0xD3D6F4FB,
        0x4369E96A, 0x346ED9FC, 0xAD678846, 0xDA60B8D0, 0x44042D73, 0x33031DE5, 0xAA0A4C5F, 0xDD0D7CC9,
        0x5005713C, 0x270241AA, 0xBE0B1010, 0xC90C2086, 0x5768B525, 0x206F85B3, 0xB966D409, 0xCE61E49F,
        0x5EDEF90E, 0x29D9C998, 0xB0D09822, 0xC7D7A8B4, 0x59B33D17, 0x2EB40D81, 0xB7BD5C3B, 0xC0BA6CAD,
        0xEDB88320, 0x9ABFB3B6, 0x03B6E20C, 0x74B1D29A, 0xEAD54739, 0x9DD277AF, 0x04DB2615, 0x73DC1683,
        0xE3630B12, 0x94643B84, 0x0D6D6A3E, 0x7A6A5AA8, 0xE40ECF0B, 0x9309FF9D, 0x0A00AE27, 0x7D079EB1,
        0xF00F9344, 0x8708A3D2, 0x1E01F268, 0x6906C2FE, 0xF762575D, 0x806567CB, 0x196C3671, 0x6E6B06E7,
        0xFED41B76, 0x89D32BE0, 0x10DA7A5A, 0x67DD4ACC, 0xF9B9DF6F, 0x8EBEEFF9, 0x17B7BE43, 0x60B08ED5,
        0xD6D6A3E8, 0xA1D1937E, 0x38D8C2C4, 0x4FDFF252, 0xD1BB67F1, 0xA6BC5767, 0x3FB506DD, 0x48B2364B,
        0xD80D2BDA, 0xAF0A1B4C, 0x36034AF6, 0x41047A60, 0xDF60EFC3, 0xA867DF55, 0x316E8EEF, 0x4669BE79,
        0xCB61B38C, 0xBC66831A, 0x256FD2A0, 0x5268E236, 0xCC0C7795, 0xBB0B4703, 0x220216B9, 0x5505262F,
        0xC5BA3BBE, 0xB2BD0B28, 0x2BB45A92, 0x5CB36A04, 0xC2D7FFA7, 0xB5D0CF31, 0x2CD99E8B, 0x5BDEAE1D,
        0x9B64C2B0, 0xEC63F226, 0x756AA39C, 0x026D930A, 0x9C0906A9, 0xEB0E363F, 0x72076785, 0x05005713,
        0x95BF4A82, 0xE2B87A14, 0x7BB12BAE, 0x0CB61B38, 0x92D28E9B, 0xE5D5BE0D, 0x7CDCEFB7, 0x0BDBDF21,
        0x86D3D2D4, 0xF1D4E242, 0x68DDB3F8, 0x1FDA836E, 0x81BE16CD, 0xF6B9265B, 0x6FB077E1, 0x18B74777,
        0x88085AE6, 0xFF0F6A70, 0x66063BCA, 0x11010B5C, 0x8F659EFF, 0xF862AE69, 0x616BFFD3, 0x166CCF45,
        0xA00AE278, 0xD70DD2EE, 0x4E048354, 0x3903B3C2, 0xA7672661, 0xD06016F7, 0x4969474D, 0x3E6E77DB,
        0xAED16A4A, 0xD9D65ADC, 0x40DF0B66, 0x37D83BF0, 0xA9BCAE53, 0xDEBB9EC5, 0x47B2CF7F, 0x30B5FFE9,
        0xBDBDF21C, 0xCABAC28A, 0x53B39330, 0x24B4A3A6, 0xBAD03605, 0xCDD70693, 0x54DE5729, 0x23D967BF,
        0xB3667A2E, 0xC4614AB8, 0x5D681B02, 0x2A6F2B94, 0xB40BBE37, 0xC30C8EA1, 0x5A05DF1B, 0x2D02EF8D
    };

    /// <summary>
    /// Deletes selected ROMs from the destination directory
    /// </summary>
    public Task DeleteSelectedDestinationRomsAsync()
    {
        if (_destinationRomListView == null || string.IsNullOrEmpty(_settings.DestinationPath))
            return Task.CompletedTask;

        try
        {
            var selectedRoms = _destinationRomListView.GetSelectedRoms();
            if (!selectedRoms.Any())
            {
                UpdateStatus("No ROMs selected for deletion");
                return Task.CompletedTask;
            }

            // Confirm deletion
            var result = MessageBox.Show(
                $"Are you sure you want to delete {selectedRoms.Count} ROM(s) from the destination directory?\n\nThis action cannot be undone.",
                "Confirm Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return Task.CompletedTask;

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
        return Task.CompletedTask;
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
