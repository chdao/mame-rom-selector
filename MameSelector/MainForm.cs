using MameSelector.Controllers;
using MameSelector.Forms;
using MameSelector.Models;
using MameSelector.Services;
using MameSelector.UI;

namespace MameSelector;

public partial class MainForm : Form
{
    private readonly MainController _controller;
    private readonly VirtualRomListView _romListView;
    private readonly DestinationRomListView _destinationRomListView;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isCopying = false;

    public MainForm()
    {
        InitializeComponent();
        
        // Set the application icon from embedded resources
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "MameSelector.Resources.mame-rom-selector.ico";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                this.Icon = new Icon(stream);
            }
        }
        catch (Exception ex)
        {
            // If icon loading fails, continue without icon
            System.Diagnostics.Debug.WriteLine($"Failed to load icon: {ex.Message}");
        }
        
        // Initialize services
        var settingsManager = new SettingsManager();
        var romScanner = new RomScanner();
        var xmlParser = new MameXmlParser();
        var metadataService = new RomMetadataService(xmlParser);
        var cacheService = new RomCacheService();
        var copyService = new RomCopyService();
        
        // Initialize virtual list view
        _romListView = new VirtualRomListView(gamesListView);
        _destinationRomListView = new DestinationRomListView(destinationListView);
        
        // Initialize controller
        _controller = new MainController(settingsManager, romScanner, metadataService, cacheService, xmlParser, copyService, _romListView);
        _controller.SetDestinationListView(_destinationRomListView);
        
        // Wire up events
        _controller.StatusUpdated += OnStatusUpdated;
        _controller.LoadingStateChanged += OnLoadingStateChanged;
        _controller.RomStatsUpdated += OnRomStatsUpdated;
        _controller.ProgressUpdated += OnProgressUpdated;
        
        // Wire up UI events
        copyRomsButton.Click += CopyRomsButton_Click;
        _romListView.SelectionChanged += OnRomSelectionChanged;
        gamesListView.SelectedIndexChanged += OnGamesListViewSelectionChanged;
        
        // Wire up resize events
        this.Resize += OnFormResize;
        romsSplitContainer.SplitterMoved += OnSplitterMoved;
        
        // Initialize UI
        InitializeUI();
        
        
        // Load settings asynchronously
        _ = InitializeAsync();
    }

    private void InitializeUI()
    {
        Text = "MAME ROM Selector v0.3.3";
        Size = new Size(1400, 900);
        StartPosition = FormStartPosition.CenterScreen;
        
        // Set default XML path to the one in the project root
        var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(Application.StartupPath));
        var defaultXmlPath = Path.Combine(projectRoot!, "MAME 0.280 ROMs (non-merged).xml");
        
        if (File.Exists(defaultXmlPath))
        {
            statusLabel.Text = $"Found MAME XML: {Path.GetFileName(defaultXmlPath)}";
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _controller.InitializeAsync();
            
            // Auto-set the XML path if it exists and settings are empty
            if (string.IsNullOrEmpty(_controller.Settings.MameXmlPath))
            {
                var projectRoot = Path.GetDirectoryName(Path.GetDirectoryName(Application.StartupPath));
                var defaultXmlPath = Path.Combine(projectRoot!, "MAME 0.280 ROMs (non-merged).xml");
                
                if (File.Exists(defaultXmlPath))
                {
                    var newSettings = new AppSettings
                    {
                        MameXmlPath = defaultXmlPath,
                        RomRepositoryPath = _controller.Settings.RomRepositoryPath,
                        DestinationPath = _controller.Settings.DestinationPath,
                        CHDRepositoryPath = _controller.Settings.CHDRepositoryPath,
                        CopyBiosFiles = _controller.Settings.CopyBiosFiles,
                        CopyDeviceFiles = _controller.Settings.CopyDeviceFiles,
                        CreateSubfolders = _controller.Settings.CreateSubfolders,
                        VerifyChecksums = _controller.Settings.VerifyChecksums
                    };
                    
                    await _controller.UpdateSettingsAsync(newSettings);
                }
            }

            // Auto-load ROMs from cache if available
            await AutoLoadRomsWithProgressAsync();
            
            UpdateUIFromSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing application: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateUIFromSettings()
    {
        var settings = _controller.Settings;
        var hasValidXml = !string.IsNullOrEmpty(settings.MameXmlPath) && File.Exists(settings.MameXmlPath);
        var hasValidRepo = !string.IsNullOrEmpty(settings.RomRepositoryPath) && Directory.Exists(settings.RomRepositoryPath);
        var hasValidDest = !string.IsNullOrEmpty(settings.DestinationPath);

        scanRomsButton.Enabled = hasValidXml && hasValidRepo && !_controller.IsLoading;
        copyRomsButton.Enabled = _controller.SelectedRoms.Any() && hasValidDest && !_controller.IsLoading;

        if (!hasValidXml || !hasValidRepo)
        {
            statusLabel.Text = "Please configure ROM repository and XML file in settings";
        }
        else if (!hasValidDest)
        {
            statusLabel.Text = "Please configure destination directory in settings";
        }
        else
        {
            statusLabel.Text = "Ready - Click 'Scan ROMs' to begin";
        }
    }

    #region Event Handlers


    private void SettingsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        ShowSettingsDialog();
    }

    private void SettingsToolStripButton_Click(object sender, EventArgs e)
    {
        ShowSettingsDialog();
    }

    private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Close();
    }


    private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var aboutForm = new AboutForm();
        aboutForm.ShowDialog(this);
    }

    private void SearchTextBox_TextChanged(object sender, EventArgs e)
    {
        _controller.FilterRoms(searchTextBox.Text);
    }


    private void SelectAllButton_Click(object sender, EventArgs e)
    {
        _controller.SelectAllRoms();
    }

    private void ClearAllButton_Click(object sender, EventArgs e)
    {
        _controller.ClearAllSelections();
    }


    private async void CacheInfoToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var cacheInfo = await _controller.GetCacheInfoAsync();
        
        if (cacheInfo == null)
        {
            MessageBox.Show("No ROM cache found.", "Cache Information", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var info = $"ROM Cache Information\n\n" +
                  $"Created: {cacheInfo.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC\n" +
                  $"ROM Count: {cacheInfo.RomCount:N0}\n" +
                  $"Cache Size: {cacheInfo.FormattedFileSize}\n\n" +
                  $"Paths:\n" +
                  $"ROM Repository: {cacheInfo.RomRepositoryPath}\n" +
                  $"CHD Repository: {cacheInfo.CHDRepositoryPath ?? "None"}\n" +
                  $"MAME XML: {cacheInfo.MameXmlPath}";

        MessageBox.Show(info, "Cache Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async void ClearCacheToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear the ROM cache?\n\nThis will force a full rescan on the next ROM load.",
            "Clear Cache", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            await _controller.ClearCacheAsync();
            MessageBox.Show("ROM cache cleared successfully.", "Cache Cleared", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    #endregion

    #region Controller Event Handlers

    private void OnStatusUpdated(object? sender, StatusUpdateEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<object?, StatusUpdateEventArgs>(OnStatusUpdated), sender, e);
            return;
        }

        statusLabel.Text = e.Message;
    }

    private void OnLoadingStateChanged(object? sender, LoadingStateChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<object?, LoadingStateChangedEventArgs>(OnLoadingStateChanged), sender, e);
            return;
        }

        progressBar.Visible = e.IsLoading;
        scanRomsButton.Enabled = !e.IsLoading;
        
        if (!e.IsLoading)
        {
            progressBar.Value = 0;
            UpdateUIFromSettings();
        }
    }

    private void OnRomStatsUpdated(object? sender, RomStatsUpdatedEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<object?, RomStatsUpdatedEventArgs>(OnRomStatsUpdated), sender, e);
            return;
        }

        var stats = e.Stats;
        var statsText = $"Total: {stats.TotalScannedRoms:N0} | " +
                       $"Matched: {stats.MatchedRoms:N0} ({stats.MatchPercentage:F1}%) | " +
                       $"CHD: {stats.RomsWithChd:N0} | " +
                       $"Clones: {stats.CloneRoms:N0}";
        
        // Update a stats label if you add one to the status bar
        statusLabel.Text = statsText;
    }

    private void OnProgressUpdated(object? sender, int percentage)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<object?, int>(OnProgressUpdated), sender, percentage);
            return;
        }

        progressBar.Value = Math.Min(Math.Max(percentage, 0), 100);
    }

    private void OnRomSelectionChanged(object? sender, RomSelectionChangedEventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<object?, RomSelectionChangedEventArgs>(OnRomSelectionChanged), sender, e);
            return;
        }

        copyRomsButton.Enabled = e.SelectedRoms.Any() && !_controller.IsLoading;
        
        var totalSize = e.SelectedRoms.Sum(r => r.TotalSize);
        var sizeText = FormatFileSize(totalSize);
        statusLabel.Text = $"Selected: {e.SelectedRoms.Count:N0} ROMs ({sizeText})";
    }

    private void OnGamesListViewSelectionChanged(object? sender, EventArgs e)
    {
        UpdateDetailsPanel();
    }

    private void UpdateDetailsPanel()
    {
        var selectedIndex = gamesListView.SelectedIndices.Count > 0 ? gamesListView.SelectedIndices[0] : -1;
        
        if (selectedIndex < 0)
        {
            detailsTextBox.Text = "Select a ROM to view details...";
            return;
        }

        var rom = _romListView.GetRomAtIndex(selectedIndex);
        if (rom == null)
        {
            detailsTextBox.Text = "No ROM data available";
            return;
        }

        var details = new List<string>();
        
        // Basic info - one line per item
        details.Add($"ROM: {rom.Name}");
        details.Add($"Description: {rom.DisplayName}");
        details.Add($"Manufacturer: {rom.DisplayManufacturer}");
        details.Add($"Year: {rom.DisplayYear}");
        details.Add($"Status: {(rom.InDestination ? "Installed" : "Not Installed")}");
        details.Add("");
        
        // File info - simplified
        if (rom.ChdFiles.Count > 0)
        {
            details.Add($"CHD Files: {rom.ChdFiles.Count}");
            details.Add($"Total Size: {FormatFileSize(rom.TotalSize)}");
            details.Add("");
            details.Add("CHD Files:");
            foreach (var chdFile in rom.ChdFiles.Take(8))
            {
                details.Add($"  {Path.GetFileName(chdFile)}");
            }
            if (rom.ChdFiles.Count > 8)
            {
                details.Add($"  ... and {rom.ChdFiles.Count - 8} more");
            }
        }
        else
        {
            details.Add($"Size: {FormatFileSize(rom.TotalSize)}");
        }
        
        // Metadata info - one line per item
        details.Add("");
        if (rom.HasMetadata && rom.Metadata != null)
        {
            details.Add($"Clone Of: {(rom.IsClone ? rom.Metadata.CloneOf ?? "Unknown" : "Parent ROM")}");
            details.Add($"Category: {rom.Metadata.Category ?? "Unknown"}");
        }
        else
        {
            details.Add("MAME Metadata: Not available");
        }

        detailsTextBox.Text = string.Join(Environment.NewLine, details);
    }

    #endregion

    #region Helper Methods

    private async Task AutoLoadRomsWithProgressAsync()
    {
        if (_controller.IsLoading)
            return;

        // Check if we should show progress (only if cache exists and has ROMs)
        var cacheInfo = await _controller.GetCacheInfoAsync();
        if (cacheInfo == null || cacheInfo.RomCount == 0)
        {
            // No cache or empty cache - just try to load without progress
            await _controller.AutoLoadRomsAsync();
            return;
        }

        try
        {
            // Show and reset progress bar
            progressBar.Visible = true;
            progressBar.Value = 0;

            var progress = new Progress<string>(message =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<string>(msg => statusLabel.Text = msg), message);
                }
                else
                {
                    statusLabel.Text = message;
                }
            });

            await _controller.AutoLoadRomsAsync(progress);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading ROM cache: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Cache loading failed";
        }
        finally
        {
            // Hide progress bar when done
            progressBar.Visible = false;
            progressBar.Value = 0;
        }
    }

    private async Task ScanRomsAsync()
    {
        
        if (_controller.IsLoading)
        {
            return;
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            
            // Show and reset progress bar
            progressBar.Visible = true;
            progressBar.Value = 0;

            var progress = new Progress<string>(message =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<string>(msg => statusLabel.Text = msg), message);
                }
                else
                {
                    statusLabel.Text = message;
                }
            });

            await _controller.ScanAndLoadRomsAsync(progress, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            statusLabel.Text = "ROM scanning cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error scanning ROMs: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "ROM scanning failed";
        }
        finally
        {
            // Hide progress bar when done
            progressBar.Visible = false;
            progressBar.Value = 0;
        }
    }

    private void ShowSettingsDialog()
    {
        using var settingsForm = new SettingsForm(_controller.Settings);
        if (settingsForm.ShowDialog() == DialogResult.OK)
        {
            _ = _controller.UpdateSettingsAsync(settingsForm.Settings);
            UpdateUIFromSettings();
        }
    }

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

    private void DestinationToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        ShowDestinationWindow();
    }

    private void ShowDestinationWindow()
    {
        var destinationForm = new DestinationForm();
        if (!string.IsNullOrEmpty(_controller.Settings.DestinationPath))
        {
            destinationForm.SetDestinationPath(_controller.Settings.DestinationPath);
        }
        
        
        destinationForm.Show();
    }

    private void ShowInstalledButton_Click(object? sender, EventArgs e)
    {
        _controller.SelectInstalledRoms();
    }

    private void SelectedButton_Click(object? sender, EventArgs e)
    {
        _controller.SelectHighlightedRoms();
    }

    private async void VerifyButton_Click(object? sender, EventArgs e)
    {
        await _controller.VerifySelectedRomsAsync();
    }

    private async void ScanRomsButton_Click(object? sender, EventArgs e)
    {
        await ScanRomsAsync();
    }

    private async void CopyRomsButton_Click(object? sender, EventArgs e)
    {
        if (_isCopying)
        {
            MessageBox.Show("Copy operation is already in progress. Please wait for it to complete.", 
                "Copy In Progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        
        await CopyRomsAsync();
    }

    private async void RefreshDestinationButton_Click(object? sender, EventArgs e)
    {
        await _controller.RefreshDestinationListAsync();
    }

    private async void DeleteDestinationButton_Click(object? sender, EventArgs e)
    {
        await _controller.DeleteSelectedDestinationRomsAsync();
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        // Adjust column widths when the form is resized
        _romListView.AdjustColumnWidths();
        _destinationRomListView.AdjustColumnWidths();
    }

    private void OnSplitterMoved(object? sender, SplitterEventArgs e)
    {
        // Adjust column widths when the splitter is moved
        _romListView.AdjustColumnWidths();
        _destinationRomListView.AdjustColumnWidths();
    }



    private async Task CopyRomsAsync()
    {
        _isCopying = true;
        try
        {
            var selectedRoms = _controller.SelectedRoms.ToList();
            if (selectedRoms.Count == 0)
            {
                MessageBox.Show("Please select ROMs to copy.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Warn if more than 100 ROMs are selected
            if (selectedRoms.Count > 100)
            {
                var warningResult = MessageBox.Show(
                    $"You have selected {selectedRoms.Count:N0} ROMs to copy. This may take a very long time.\n\n" +
                    $"Are you sure you want to continue?",
                    "Large Selection Warning",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                
                if (warningResult != DialogResult.Yes)
                {
                    return;
                }
            }

            // Show progress bar
            progressBar.Visible = true;
            progressBar.Value = 0;

            // Create progress reporter
            var progress = new Progress<CopyProgress>(copyProgress =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action<CopyProgress>(p => UpdateCopyProgress(p)), copyProgress);
                }
                else
                {
                    UpdateCopyProgress(copyProgress);
                }
            });

            // Perform copy operation
            var result = await _controller.CopySelectedRomsAsync(progress);

            // Hide progress bar
            progressBar.Visible = false;
            progressBar.Value = 0;

            // Show result
            var message = $"Copy operation completed!\n\n" +
                         $"Successfully copied: {result.SuccessfulCopies} ROMs\n" +
                         $"Failed: {result.FailedCopies} ROMs";

            if (result.FailedCopies > 0)
            {
                message += $"\n\nFailed ROMs:\n" + string.Join("\n", result.FailedRoms.Select(f => $"- {f.RomName}: {f.Error}"));
            }

            MessageBox.Show(message, "Copy Complete", MessageBoxButtons.OK, 
                result.FailedCopies > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            progressBar.Visible = false;
            progressBar.Value = 0;
            MessageBox.Show($"Error copying ROMs: {ex.Message}", "Copy Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isCopying = false;
        }
    }

    private void UpdateCopyProgress(CopyProgress progress)
    {
        statusLabel.Text = progress.Phase;
        progressBar.Value = Math.Min(Math.Max(progress.Percentage, 0), 100);
    }

    #endregion

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        try
        {
            _cancellationTokenSource?.Cancel();
        }
        catch
        {
            // Ignore cancellation errors during shutdown
        }
        
        try
        {
            _cancellationTokenSource?.Dispose();
        }
        catch
        {
            // Ignore disposal errors during shutdown
        }
        
        base.OnFormClosing(e);
    }

}
