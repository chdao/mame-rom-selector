using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MameSelector.Models;

namespace MameSelector.UI
{
    /// <summary>
    /// Virtual ListView specifically for displaying ROMs in the destination directory
    /// </summary>
    public class DestinationRomListView
    {
        private readonly ListView _listView;
        private List<ScannedRom> _destinationRoms = new();
        private int _sortColumn = -1;
        private SortOrder _sortOrder = SortOrder.None;

        public DestinationRomListView(ListView listView)
        {
            _listView = listView ?? throw new ArgumentNullException(nameof(listView));
            SetupListView();
            
            // Adjust column widths after setup
            AdjustColumnWidths();
        }

        /// <summary>
        /// Adjusts column widths when the ListView is resized
        /// </summary>
        public void AdjustColumnWidths()
        {
            if (_listView.Columns.Count >= 3)
            {
                // Keep Name and Size columns fixed, Description fills remaining space
                var totalWidth = _listView.ClientSize.Width;
                var fixedWidth = 200 + 120; // Name + Size
                var descriptionWidth = Math.Max(200, totalWidth - fixedWidth - 20); // 20px for scrollbar
                
                _listView.Columns[0].Width = 200; // Name - fixed
                _listView.Columns[1].Width = descriptionWidth; // Description - auto-resize
                _listView.Columns[2].Width = 120; // Size - fixed
            }
        }

        /// <summary>
        /// Sets up the ListView for destination ROMs
        /// </summary>
        private void SetupListView()
        {
            _listView.VirtualMode = true;
            _listView.VirtualListSize = 0;
            _listView.View = View.Details;
            _listView.FullRowSelect = true;
            _listView.GridLines = true;
            _listView.CheckBoxes = false;
            _listView.MultiSelect = true;

            // Setup columns
            _listView.Columns.Clear();
            _listView.Columns.Add("Name", 200);
            _listView.Columns.Add("Description", 400);
            _listView.Columns.Add("Size", 120);
            
            // Set column resize behavior
            _listView.Columns[0].Width = 200; // Name - fixed
            _listView.Columns[1].Width = 400; // Description - will auto-resize
            _listView.Columns[2].Width = 120; // Size - fixed

            // Wire up events
            _listView.RetrieveVirtualItem += OnRetrieveVirtualItem;
            _listView.DoubleClick += OnDoubleClick;
            _listView.CacheVirtualItems += OnCacheVirtualItems;
            _listView.ColumnClick += OnColumnClick;
        }

        /// <summary>
        /// Updates the destination ROM collection
        /// </summary>
        public void UpdateDestinationRoms(List<ScannedRom> allRoms)
        {
            _destinationRoms = allRoms.Where(rom => rom.InDestination).ToList();
            _listView.VirtualListSize = _destinationRoms.Count;
            _listView.Invalidate();
            
            // Adjust column widths after updating ROMs
            AdjustColumnWidths();
        }

        /// <summary>
        /// Clears all ROMs from the destination list
        /// </summary>
        public void ClearRoms()
        {
            _destinationRoms.Clear();
            _listView.VirtualListSize = 0;
            _listView.Invalidate();
        }

        /// <summary>
        /// Gets the currently selected ROMs
        /// </summary>
        public List<ScannedRom> GetSelectedRoms()
        {
            var selectedRoms = new List<ScannedRom>();
            foreach (int index in _listView.SelectedIndices)
            {
                if (index >= 0 && index < _destinationRoms.Count)
                {
                    selectedRoms.Add(_destinationRoms[index]);
                }
            }
            return selectedRoms;
        }


        /// <summary>
        /// Handles virtual item retrieval
        /// </summary>
        private void OnRetrieveVirtualItem(object? sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _destinationRoms.Count)
                return;

            var rom = _destinationRoms[e.ItemIndex];
            var item = new ListViewItem(rom.Name);
            
            item.SubItems.Add(rom.DisplayName);
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
        /// Handles double-click events
        /// </summary>
        private void OnDoubleClick(object? sender, EventArgs e)
        {
            if (_listView.SelectedIndices.Count > 0)
            {
                var index = _listView.SelectedIndices[0];
                if (index >= 0 && index < _destinationRoms.Count)
                {
                    var rom = _destinationRoms[index];
                    // Could add context menu or details dialog here
                }
            }
        }

        /// <summary>
        /// Handles cache virtual items event
        /// </summary>
        private void OnCacheVirtualItems(object? sender, CacheVirtualItemsEventArgs e)
        {
            // Virtual mode handles caching automatically
        }

        /// <summary>
        /// Handles column click events for sorting
        /// </summary>
        private void OnColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (e.Column == _sortColumn)
            {
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _sortColumn = e.Column;
                _sortOrder = SortOrder.Ascending;
            }

            SortRoms();
        }

        /// <summary>
        /// Sorts the ROMs based on the current sort column and order
        /// </summary>
        private void SortRoms()
        {
            if (_sortColumn < 0 || _sortOrder == SortOrder.None) return;

            _destinationRoms.Sort((rom1, rom2) =>
            {
                var comparison = _sortColumn switch
                {
                    0 => string.Compare(rom1.Name, rom2.Name, StringComparison.OrdinalIgnoreCase),
                    1 => string.Compare(rom1.DisplayName, rom2.DisplayName, StringComparison.OrdinalIgnoreCase),
                    2 => rom1.TotalSize.CompareTo(rom2.TotalSize),
                    _ => 0
                };

                return _sortOrder == SortOrder.Descending ? -comparison : comparison;
            });

            _listView.Invalidate();
        }

        /// <summary>
        /// Gets a status string for the ROM
        /// </summary>
        private static string GetRomStatusString(ScannedRom rom)
        {
            if (!rom.HasRomFile && !rom.HasChd)
                return "Missing";
            if (!rom.HasRomFile)
                return "CHD Only";
            if (!rom.HasChd)
                return "ROM Only";
            return "Complete";
        }

        /// <summary>
        /// Formats file size in human-readable format
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        /// <summary>
        /// Gets statistics about the destination ROMs
        /// </summary>
        public (int TotalRoms, int WithMetadata, int WithChd, long TotalSize) GetStats()
        {
            return (
                TotalRoms: _destinationRoms.Count,
                WithMetadata: _destinationRoms.Count(rom => rom.HasMetadata),
                WithChd: _destinationRoms.Count(rom => rom.HasChd),
                TotalSize: _destinationRoms.Sum(rom => rom.TotalSize)
            );
        }

    }
}

