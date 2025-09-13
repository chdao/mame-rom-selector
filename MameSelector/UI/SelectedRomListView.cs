using MameSelector.Models;

namespace MameSelector.UI;

/// <summary>
/// Manages the selected ROMs list view display
/// </summary>
public class SelectedRomListView
{
    private readonly ListView _listView;
    private List<ScannedRom> _selectedRoms = new();

    /// <summary>
    /// Event fired when a ROM should be deselected
    /// </summary>
    public event EventHandler<ScannedRom>? RomDeselected;

    public SelectedRomListView(ListView listView)
    {
        _listView = listView;
        SetupListView();
    }

    /// <summary>
    /// Updates the selected ROMs display
    /// </summary>
    public void UpdateSelectedRoms(IEnumerable<ScannedRom> selectedRoms)
    {
        _selectedRoms = selectedRoms.ToList();
        RefreshDisplay();
    }

    /// <summary>
    /// Sets up the ListView for displaying selected ROMs
    /// </summary>
    private void SetupListView()
    {
        _listView.View = View.Details;
        _listView.FullRowSelect = true;
        _listView.GridLines = true;
        _listView.CheckBoxes = false;
        _listView.MultiSelect = true;

        // Setup columns with better sizing
        _listView.Columns.Clear();
        _listView.Columns.Add("Name", 200);
        _listView.Columns.Add("Description", 400); // Wider description column
        _listView.Columns.Add("Year", 60);
        _listView.Columns.Add("CHD", 60);
        _listView.Columns.Add("Size", 120);

        // Set column resize behavior - Description column grows
        _listView.Columns[0].Width = 200; // Name - fixed
        _listView.Columns[1].Width = 400; // Description - grows to fill space
        _listView.Columns[2].Width = 60;  // Year - fixed
        _listView.Columns[3].Width = 60;  // CHD - fixed
        _listView.Columns[4].Width = 120; // Size - fixed

        // Add double-click handler for deselecting ROMs
        _listView.DoubleClick += OnDoubleClick;
    }

    /// <summary>
    /// Refreshes the display with current selected ROMs
    /// </summary>
    private void RefreshDisplay()
    {
        _listView.Items.Clear();

        foreach (var rom in _selectedRoms)
        {
            var item = new ListViewItem(rom.Name);
            item.SubItems.Add(rom.DisplayName);
            item.SubItems.Add(rom.DisplayYear);
            item.SubItems.Add(rom.HasChd ? "✓" : "");
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
            else if (rom.InDestination)
            {
                item.ForeColor = Color.Green;
            }

            _listView.Items.Add(item);
        }
    }

    /// <summary>
    /// Formats file size for display
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Handles double-click events to deselect ROMs
    /// </summary>
    private void OnDoubleClick(object? sender, EventArgs e)
    {
        if (_listView.SelectedItems.Count > 0)
        {
            var selectedItem = _listView.SelectedItems[0];
            if (selectedItem.Tag is ScannedRom rom)
            {
                RomDeselected?.Invoke(this, rom);
            }
        }
    }
}
