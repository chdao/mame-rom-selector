using MameSelector.Models;

namespace MameSelector.UI;

/// <summary>
/// Virtual list view manager for efficiently displaying large ROM collections
/// </summary>
public class VirtualRomListView
{
    private readonly ListView _listView;
    private readonly ToolTip _toolTip;
    private List<ScannedRom> _allRoms = new();
    private List<ScannedRom> _filteredRoms = new();
    private readonly HashSet<ScannedRom> _selectedRoms = new();
    private string _currentFilter = string.Empty;
    private int _sortColumn = -1;
    private SortOrder _sortOrder = SortOrder.None;

    public event EventHandler<RomSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<RomDoubleClickEventArgs>? RomDoubleClick;

    /// <summary>
    /// Gets whether invoke is required for thread-safe operations
    /// </summary>
    private bool InvokeRequired => _listView.InvokeRequired;

    public VirtualRomListView(ListView listView)
    {
        _listView = listView;
        _toolTip = new ToolTip();
        _toolTip.IsBalloon = false;
        _toolTip.ToolTipTitle = "ROM Information";
        _toolTip.ShowAlways = false;
        _toolTip.InitialDelay = 300;
        _toolTip.AutoPopDelay = 5000;
        SetupListView();
    }

    /// <summary>
    /// Gets the currently selected ROMs
    /// </summary>
    public IReadOnlyCollection<ScannedRom> SelectedRoms => _selectedRoms;

    /// <summary>
    /// Gets the ROM at the specified index
    /// </summary>
    public ScannedRom? GetRomAtIndex(int index)
    {
        if (index < 0 || index >= _filteredRoms.Count)
            return null;
        return _filteredRoms[index];
    }

    /// <summary>
    /// Gets the currently filtered ROMs
    /// </summary>
    public IReadOnlyList<ScannedRom> FilteredRoms => _filteredRoms;

    /// <summary>
    /// Gets all ROMs
    /// </summary>
    public IReadOnlyList<ScannedRom> AllRoms => _allRoms;

    /// <summary>
    /// Adjusts column widths when the ListView is resized
    /// </summary>
    public void AdjustColumnWidths()
    {
        if (_listView.Columns.Count >= 6)
        {
            // Keep all columns fixed except Description, which fills remaining space
            var totalWidth = _listView.ClientSize.Width;
            var fixedWidth = 30 + 200 + 60 + 50 + 120; // Checkbox + Name + Year + CHD + Size
            var descriptionWidth = Math.Max(200, totalWidth - fixedWidth - 20); // 20px for scrollbar
            
            _listView.Columns[0].Width = 30;  // Checkbox
            _listView.Columns[1].Width = 200; // Name
            _listView.Columns[2].Width = descriptionWidth; // Description - auto-resize
            _listView.Columns[3].Width = 60;  // Year
            _listView.Columns[4].Width = 50;  // CHD
            _listView.Columns[5].Width = 120; // Size
        }
    }

    /// <summary>
    /// Sets up the ListView for virtual mode
    /// </summary>
    private void SetupListView()
    {
        _listView.VirtualMode = true;
        _listView.VirtualListSize = 0;
        _listView.View = View.Details;
        _listView.FullRowSelect = true;
        _listView.GridLines = true;
        _listView.CheckBoxes = false;  // We'll create our own checkbox column
        _listView.MultiSelect = true;

        // Setup columns
        _listView.Columns.Clear();
        _listView.Columns.Add("☐", 30);  // Checkbox column
        _listView.Columns.Add("Name", 200);
        _listView.Columns.Add("Description", 500);
        _listView.Columns.Add("Year", 60);
        _listView.Columns.Add("CHD", 50);
        _listView.Columns.Add("Size", 120);
        
        // Set column resize behavior - only Description column should auto-resize
        _listView.Columns[0].Width = 30;  // Checkbox - fixed
        _listView.Columns[1].Width = 200; // Name - fixed
        _listView.Columns[2].Width = 500; // Description - will auto-resize
        _listView.Columns[3].Width = 60;  // Year - fixed
        _listView.Columns[4].Width = 50;  // CHD - fixed
        _listView.Columns[5].Width = 120; // Size - fixed

        // Wire up events
        _listView.RetrieveVirtualItem += OnRetrieveVirtualItem;
        _listView.DoubleClick += OnDoubleClick;
        _listView.CacheVirtualItems += OnCacheVirtualItems;
        _listView.ColumnClick += OnColumnClick;
        _listView.MouseClick += OnMouseClick;
    }

    /// <summary>
    /// Updates the ROM collection and refreshes the display
    /// </summary>
    public void UpdateRoms(IEnumerable<ScannedRom> roms)
    {
        _allRoms = roms.ToList();
        ApplyFilter(_currentFilter);
    }

    /// <summary>
    /// Adds a single ROM to the collection in real-time
    /// </summary>
    public void AddRom(ScannedRom rom)
    {
        if (InvokeRequired)
        {
            _listView.Invoke(new Action<ScannedRom>(AddRom), rom);
            return;
        }

        // Check if ROM already exists (update scenario)
        var existingIndex = _allRoms.FindIndex(r => r.Name.Equals(rom.Name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            _allRoms[existingIndex] = rom;
        }
        else
        {
            _allRoms.Add(rom);
        }

        // Check if the ROM matches current filter
        var matchesFilter = string.IsNullOrWhiteSpace(_currentFilter) ||
                           rom.Name.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase) ||
                           rom.DisplayName.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase) ||
                           rom.DisplayManufacturer.Contains(_currentFilter, StringComparison.OrdinalIgnoreCase);

        if (matchesFilter)
        {
            // Update filtered list
            var filteredIndex = _filteredRoms.FindIndex(r => r.Name.Equals(rom.Name, StringComparison.OrdinalIgnoreCase));
            if (filteredIndex >= 0)
            {
                _filteredRoms[filteredIndex] = rom;
            }
            else
            {
                _filteredRoms.Add(rom);
            }

            // Update virtual list size and refresh
            _listView.VirtualListSize = _filteredRoms.Count;
            
            // Only invalidate the new item area to avoid flickering
            if (filteredIndex < 0) // New item
            {
                _listView.Invalidate();
            }
            else // Updated item
            {
                _listView.RedrawItems(filteredIndex, filteredIndex, false);
            }
        }
    }

    /// <summary>
    /// Clears all ROMs (for starting a new scan)
    /// </summary>
    public void ClearRoms()
    {
        if (InvokeRequired)
        {
            _listView.Invoke(new Action(ClearRoms));
            return;
        }

        _allRoms.Clear();
        _filteredRoms.Clear();
        _selectedRoms.Clear();
        _listView.VirtualListSize = 0;
        _listView.Invalidate();
        OnSelectionChanged();
    }

    /// <summary>
    /// Selects all visible (filtered) ROMs
    /// </summary>
    public void SelectAll()
    {
        _selectedRoms.Clear();
        foreach (var rom in _filteredRoms)
        {
            _selectedRoms.Add(rom);
        }
        
        // Force refresh of all visible items
        if (_filteredRoms.Count > 0)
        {
            _listView.RedrawItems(0, _filteredRoms.Count - 1, false);
            _listView.Update();
        }
        
        OnSelectionChanged();
        System.Diagnostics.Debug.WriteLine($"DEBUG: Selected all ROMs - Total: {_selectedRoms.Count}");
    }

    /// <summary>
    /// Clears all ROM selections
    /// </summary>
    public void ClearSelection()
    {
        _selectedRoms.Clear();
        
        // Force refresh of all visible items
        if (_filteredRoms.Count > 0)
        {
            _listView.RedrawItems(0, _filteredRoms.Count - 1, false);
            _listView.Update();
        }
        
        OnSelectionChanged();
        System.Diagnostics.Debug.WriteLine("DEBUG: Cleared all ROM selections");
    }

    /// <summary>
    /// Selects all ROMs that are already installed in the destination directory
    /// </summary>
    public void SelectInstalledRoms()
    {
        _selectedRoms.Clear();
        foreach (var rom in _filteredRoms.Where(rom => rom.InDestination))
        {
            _selectedRoms.Add(rom);
        }
        
        // Force refresh of all visible items
        if (_filteredRoms.Count > 0)
        {
            _listView.RedrawItems(0, _filteredRoms.Count - 1, false);
            _listView.Update();
        }
        
        OnSelectionChanged();
        System.Diagnostics.Debug.WriteLine($"DEBUG: Selected {_selectedRoms.Count} installed ROMs");
    }

    /// <summary>
    /// Selects all currently highlighted/selected ROMs in the list view
    /// </summary>
    public void SelectHighlightedRoms()
    {
        _selectedRoms.Clear();
        foreach (int index in _listView.SelectedIndices)
        {
            if (index >= 0 && index < _filteredRoms.Count)
            {
                var rom = _filteredRoms[index];
                _selectedRoms.Add(rom);
            }
        }
        
        // Force refresh of all visible items
        if (_filteredRoms.Count > 0)
        {
            _listView.RedrawItems(0, _filteredRoms.Count - 1, false);
            _listView.Update();
        }
        
        OnSelectionChanged();
        System.Diagnostics.Debug.WriteLine($"DEBUG: Selected {_selectedRoms.Count} highlighted ROMs");
    }

    /// <summary>
    /// Refreshes the display to show updated ROM data
    /// </summary>
    public void RefreshDisplay()
    {
        _listView.Invalidate();
    }


    /// <summary>
    /// Applies a filter to the ROM list
    /// </summary>
    public void ApplyFilter(string filter)
    {
        _currentFilter = filter;

        if (string.IsNullOrWhiteSpace(filter))
        {
            _filteredRoms = new List<ScannedRom>(_allRoms);
        }
        else
        {
            var filterLower = filter.ToLowerInvariant();
            
            // Handle special destination filters
            if (filterLower.StartsWith("destination:"))
            {
                var destinationFilter = filterLower.Substring("destination:".Length);
                _filteredRoms = destinationFilter switch
                {
                    "installed" => _allRoms.Where(rom => rom.InDestination).ToList(),
                    "not-installed" => _allRoms.Where(rom => !rom.InDestination).ToList(),
                    _ => _allRoms.ToList()
                };
            }
            else
            {
                // Regular text filter
                _filteredRoms = _allRoms.Where(rom =>
                    rom.Name.Contains(filterLower, StringComparison.OrdinalIgnoreCase) ||
                    rom.DisplayName.Contains(filterLower, StringComparison.OrdinalIgnoreCase) ||
                    rom.DisplayManufacturer.Contains(filterLower, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
        }

        // Apply current sorting
        SortRoms();

        // Update virtual list size and refresh
        _listView.VirtualListSize = _filteredRoms.Count;
        _listView.Invalidate();
    }


    /// <summary>
    /// Toggles selection for ROMs matching a predicate
    /// </summary>
    public void ToggleSelection(Func<ScannedRom, bool> predicate, bool select)
    {
        var romsToToggle = _filteredRoms.Where(predicate).ToList();
        
        foreach (var rom in romsToToggle)
        {
            if (select)
                _selectedRoms.Add(rom);
            else
                _selectedRoms.Remove(rom);
        }

        _listView.Invalidate();
        OnSelectionChanged();
    }

    /// <summary>
    /// Gets ROM statistics for the current filter
    /// </summary>
    public RomListStats GetStats()
    {
        return new RomListStats
        {
            TotalRoms = _allRoms.Count,
            FilteredRoms = _filteredRoms.Count,
            SelectedRoms = _selectedRoms.Count,
            RomsWithMetadata = _filteredRoms.Count(r => r.HasMetadata),
            RomsWithChd = _filteredRoms.Count(r => r.HasChd),
            CloneRoms = _filteredRoms.Count(r => r.IsClone),
            TotalSelectedSize = _selectedRoms.Sum(r => r.TotalSize)
        };
    }

    /// <summary>
    /// Handles virtual item retrieval
    /// </summary>
    private void OnRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= _filteredRoms.Count)
            return;

        var rom = _filteredRoms[e.ItemIndex];
        
        // First column is the checkbox
        var checkboxText = _selectedRoms.Contains(rom) ? "✓" : "☐";
        var item = new ListViewItem(checkboxText);
        
        // Second column is the name
        item.SubItems.Add(rom.Name);
        
        // Third column is the description
        item.SubItems.Add(rom.DisplayName);
        
        // Fourth column is the year
        item.SubItems.Add(rom.DisplayYear);
        
        // Fifth column is CHD status
        item.SubItems.Add(rom.HasChd ? "✓" : "");
        
        // Sixth column is size
        item.SubItems.Add(FormatFileSize(rom.TotalSize));

        item.Tag = rom;

        // Color coding based on status
        if (!rom.HasMetadata)
        {
            item.ForeColor = Color.Gray;
        }
        else if (rom.IsClone)
        {
            item.ForeColor = Color.Blue;
        }

        e.Item = item;
    }



    /// <summary>
    /// Handles mouse click events for checkbox interaction in virtual mode
    /// </summary>
    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        var hitTest = _listView.HitTest(e.Location);
        if (hitTest.Item == null)
            return;

        var itemIndex = hitTest.Item.Index;
        if (itemIndex < 0 || itemIndex >= _filteredRoms.Count)
            return;

        var rom = _filteredRoms[itemIndex];
        
        // Check if click is in the checkbox column (first column)
        var checkboxColumnWidth = _listView.Columns[0].Width;
        
        if (e.X <= checkboxColumnWidth)
        {
            // Click in checkbox column - toggle selection
            bool wasSelected = _selectedRoms.Contains(rom);
            if (wasSelected)
            {
                _selectedRoms.Remove(rom);
            }
            else
            {
                _selectedRoms.Add(rom);
            }
            _listView.RedrawItems(itemIndex, itemIndex, false);
            _listView.Update();
            OnSelectionChanged();
        }
    }


    /// <summary>
    /// Handles double-click events
    /// </summary>
    private void OnDoubleClick(object? sender, EventArgs e)
    {
        if (_listView.SelectedIndices.Count > 0)
        {
            var index = _listView.SelectedIndices[0];
            if (index >= 0 && index < _filteredRoms.Count)
            {
                var rom = _filteredRoms[index];
                RomDoubleClick?.Invoke(this, new RomDoubleClickEventArgs(rom));
            }
        }
    }

    /// <summary>
    /// Handles virtual item caching for performance
    /// </summary>
    private void OnCacheVirtualItems(object? sender, CacheVirtualItemsEventArgs e)
    {
        // Pre-cache items for smooth scrolling
        // This is called when the ListView needs to cache items for performance
        // We don't need to do anything special here as our data is already in memory
    }

    /// <summary>
    /// Raises the SelectionChanged event
    /// </summary>
    private void OnSelectionChanged()
    {
        SelectionChanged?.Invoke(this, new RomSelectionChangedEventArgs(_selectedRoms.ToList()));
    }

    /// <summary>
    /// Handles column click for sorting
    /// </summary>
    private void OnColumnClick(object? sender, ColumnClickEventArgs e)
    {
        // Determine sort order
        if (_sortColumn == e.Column)
        {
            _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
        }
        else
        {
            _sortColumn = e.Column;
            _sortOrder = SortOrder.Ascending;
        }

        // Apply sorting
        SortRoms();
        
        // Refresh the display
        _listView.VirtualListSize = _filteredRoms.Count;
        _listView.Invalidate();
    }

    /// <summary>
    /// Sorts the filtered ROM list based on current sort column and order
    /// </summary>
    private void SortRoms()
    {
        if (_sortColumn < 0 || _sortOrder == SortOrder.None)
            return;

        _filteredRoms.Sort((rom1, rom2) =>
        {
            var comparison = _sortColumn switch
            {
                0 => _selectedRoms.Contains(rom1).CompareTo(_selectedRoms.Contains(rom2)), // Checkbox column
                1 => string.Compare(rom1.Name, rom2.Name, StringComparison.OrdinalIgnoreCase),
                2 => string.Compare(rom1.DisplayName, rom2.DisplayName, StringComparison.OrdinalIgnoreCase),
                3 => string.Compare(rom1.DisplayYear, rom2.DisplayYear, StringComparison.OrdinalIgnoreCase),
                4 => rom1.HasChd.CompareTo(rom2.HasChd),
                5 => rom1.TotalSize.CompareTo(rom2.TotalSize),
                _ => 0
            };

            return _sortOrder == SortOrder.Descending ? -comparison : comparison;
        });
        
        // Update virtual list size and refresh
        _listView.VirtualListSize = _filteredRoms.Count;
        _listView.Invalidate();
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
}

/// <summary>
/// Event arguments for ROM selection changes
/// </summary>
public class RomSelectionChangedEventArgs : EventArgs
{
    public List<ScannedRom> SelectedRoms { get; }

    public RomSelectionChangedEventArgs(List<ScannedRom> selectedRoms)
    {
        SelectedRoms = selectedRoms;
    }
}

/// <summary>
/// Event arguments for ROM double-click
/// </summary>
public class RomDoubleClickEventArgs : EventArgs
{
    public ScannedRom Rom { get; }

    public RomDoubleClickEventArgs(ScannedRom rom)
    {
        Rom = rom;
    }
}

/// <summary>
/// Statistics about the ROM list
/// </summary>
public class RomListStats
{
    public int TotalRoms { get; set; }
    public int FilteredRoms { get; set; }
    public int SelectedRoms { get; set; }
    public int RomsWithMetadata { get; set; }
    public int RomsWithChd { get; set; }
    public int CloneRoms { get; set; }
    public long TotalSelectedSize { get; set; }
}
