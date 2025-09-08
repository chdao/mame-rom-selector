using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MameSelector.Models;

namespace MameSelector.Forms
{
    /// <summary>
    /// Form to display ROMs that have been copied to the destination directory
    /// </summary>
    public partial class DestinationForm : Form
    {
        private ListView _destinationListView;
        private ToolStrip _toolStrip;
        private ToolStripLabel _statusLabel;
        private ToolStripButton _refreshButton;
        private ToolStripButton _deleteButton;
        private ToolStripButton _openFolderButton;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _countLabel;
        private ToolStripStatusLabel _sizeLabel;

        private string _destinationPath = string.Empty;
        private List<FileInfo> _destinationFiles = new();

        public DestinationForm()
        {
            _destinationListView = new ListView();
            _toolStrip = new ToolStrip();
            _statusLabel = new ToolStripLabel();
            _refreshButton = new ToolStripButton();
            _deleteButton = new ToolStripButton();
            _openFolderButton = new ToolStripButton();
            _statusStrip = new StatusStrip();
            _countLabel = new ToolStripStatusLabel();
            _sizeLabel = new ToolStripStatusLabel();
            SetupUI();
        }

        public void SetDestinationPath(string path)
        {
            _destinationPath = path;
            Text = $"Destination ROMs - {path}";
            RefreshDestinationList();
        }

        private void SetupUI()
        {
            Text = "Destination ROMs";
            Size = new Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;
            Icon = SystemIcons.Application;

            // Create toolbar
            _toolStrip = new ToolStrip
            {
                Dock = DockStyle.Top
            };
            _refreshButton = new ToolStripButton("🔄 Refresh", null, OnRefreshClick) { ToolTipText = "Refresh destination list" };
            _deleteButton = new ToolStripButton("🗑️ Delete Selected", null, OnDeleteClick) { ToolTipText = "Delete selected ROMs", Enabled = false };
            _openFolderButton = new ToolStripButton("📁 Open Folder", null, OnOpenFolderClick) { ToolTipText = "Open destination folder in Explorer" };
            _statusLabel = new ToolStripLabel("Ready");

            _toolStrip.Items.AddRange(new ToolStripItem[] {
                _refreshButton,
                new ToolStripSeparator(),
                _deleteButton,
                new ToolStripSeparator(),
                _openFolderButton,
                new ToolStripSeparator(),
                _statusLabel
            });


            // Create ListView
            _destinationListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true,
                MultiSelect = true
            };

            _destinationListView.Columns.Add("Name", 200);
            _destinationListView.Columns.Add("Size", 100);
            _destinationListView.Columns.Add("Date Modified", 150);
            _destinationListView.Columns.Add("Type", 80);

            _destinationListView.ItemChecked += OnItemChecked;

            // Create status strip
            _statusStrip = new StatusStrip
            {
                Dock = DockStyle.Bottom
            };
            _countLabel = new ToolStripStatusLabel("0 files");
            _sizeLabel = new ToolStripStatusLabel("0 MB");
            _statusStrip.Items.AddRange(new ToolStripItem[] { _countLabel, _sizeLabel });

            // Add controls to form
            Controls.Add(_destinationListView);
            Controls.Add(_toolStrip);
            Controls.Add(_statusStrip);
            
        }

        private void RefreshDestinationList()
        {
            if (string.IsNullOrEmpty(_destinationPath) || !Directory.Exists(_destinationPath))
            {
                _destinationListView.Items.Clear();
                _destinationFiles.Clear();
                UpdateStatusLabels();
                return;
            }

            try
            {
                _statusLabel.Text = "Scanning destination...";
                _destinationListView.Items.Clear();
                _destinationFiles.Clear();

                // Get all ROM files in destination
                var extensions = new[] { ".zip", ".7z", ".rar", ".chd" };
                var files = Directory.GetFiles(_destinationPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.Name)
                    .ToList();

                _destinationFiles = files;

                foreach (var file in files)
                {
                    var item = new ListViewItem(file.Name);
                    item.SubItems.Add(FormatFileSize(file.Length));
                    item.SubItems.Add(file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    item.SubItems.Add(Path.GetExtension(file.Name).ToUpperInvariant());
                    item.Tag = file;
                    _destinationListView.Items.Add(item);
                }

                UpdateStatusLabels();
                _statusLabel.Text = "Ready";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Error scanning destination directory: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStatusLabels()
        {
            _countLabel.Text = $"{_destinationFiles.Count} files";
            var totalSize = _destinationFiles.Sum(f => f.Length);
            _sizeLabel.Text = FormatFileSize(totalSize);
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }

        private void OnRefreshClick(object? sender, EventArgs e)
        {
            RefreshDestinationList();
        }

        private void OnDeleteClick(object? sender, EventArgs e)
        {
            var checkedItems = _destinationListView.CheckedItems.Cast<ListViewItem>().ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show("Please select files to delete.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete {checkedItems.Count} selected files?",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            try
            {
                foreach (var item in checkedItems)
                {
                    if (item.Tag is FileInfo file && file.Exists)
                    {
                        file.Delete();
                    }
                }

                RefreshDestinationList();
                MessageBox.Show($"Deleted {checkedItems.Count} files.", "Delete Complete", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting files: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnOpenFolderClick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_destinationPath) || !Directory.Exists(_destinationPath))
            {
                MessageBox.Show("Destination path is not set or does not exist.", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", _destinationPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening folder: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            var checkedCount = _destinationListView.CheckedItems.Count;
            _deleteButton.Enabled = checkedCount > 0;
        }

    }
}

