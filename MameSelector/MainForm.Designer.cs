namespace MameSelector;

partial class MainForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        
        // Dispose cancellation token source if it exists
        if (disposing)
        {
            try
            {
                _cancellationTokenSource?.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }
        
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
        menuStrip = new MenuStrip();
        fileToolStripMenuItem = new ToolStripMenuItem();
        settingsToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator3 = new ToolStripSeparator();
        cacheInfoToolStripMenuItem = new ToolStripMenuItem();
        clearCacheToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator4 = new ToolStripSeparator();
        destinationToolStripMenuItem = new ToolStripMenuItem();
        toolStripSeparator5 = new ToolStripSeparator();
        exitToolStripMenuItem = new ToolStripMenuItem();
        helpToolStripMenuItem = new ToolStripMenuItem();
        aboutToolStripMenuItem = new ToolStripMenuItem();
        copyRomsButton = new Button();
        statusStrip = new StatusStrip();
        statusLabel = new ToolStripStatusLabel();
        progressBar = new ToolStripProgressBar();
        mainPanel = new Panel();
        mainSplitContainer = new SplitContainer();
        romsSplitContainer = new SplitContainer();
        gamesGroupBox = new GroupBox();
        gamesListView = new ListView();
        destinationGroupBox = new GroupBox();
        destinationListView = new ListView();
        destinationPanel = new Panel();
        refreshDestinationButton = new Button();
        deleteDestinationButton = new Button();
        detailsGroupBox = new GroupBox();
        detailsTextBox = new TextBox();
        searchPanel = new Panel();
        clearAllButton = new Button();
        selectAllButton = new Button();
        showInstalledButton = new Button();
        selectedButton = new Button();
        verifyButton = new Button();
        scanRomsButton = new Button();
        searchTextBox = new TextBox();
        searchLabel = new Label();
        
        menuStrip.SuspendLayout();
        statusStrip.SuspendLayout();
        mainPanel.SuspendLayout();
        romsSplitContainer.SuspendLayout();
        gamesGroupBox.SuspendLayout();
        destinationGroupBox.SuspendLayout();
        destinationPanel.SuspendLayout();
        detailsGroupBox.SuspendLayout();
        searchPanel.SuspendLayout();
        SuspendLayout();

        // 
        // menuStrip
        // 
        menuStrip.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, helpToolStripMenuItem });
        menuStrip.Location = new Point(0, 0);
        menuStrip.Name = "menuStrip";
        menuStrip.Size = new Size(2200, 24);
        menuStrip.TabIndex = 0;

        // 
        // fileToolStripMenuItem
        // 
        fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { settingsToolStripMenuItem, toolStripSeparator3, cacheInfoToolStripMenuItem, clearCacheToolStripMenuItem, toolStripSeparator4, destinationToolStripMenuItem, toolStripSeparator5, exitToolStripMenuItem });
        fileToolStripMenuItem.Name = "fileToolStripMenuItem";
        fileToolStripMenuItem.Size = new Size(37, 20);
        fileToolStripMenuItem.Text = "&File";

        // 
        // settingsToolStripMenuItem
        // 
        settingsToolStripMenuItem.Name = "settingsToolStripMenuItem";
        settingsToolStripMenuItem.Size = new Size(116, 22);
        settingsToolStripMenuItem.Text = "&Settings";
        settingsToolStripMenuItem.Click += SettingsToolStripMenuItem_Click;

        // 
        // toolStripSeparator3
        // 
        toolStripSeparator3.Name = "toolStripSeparator3";
        toolStripSeparator3.Size = new Size(113, 6);

        // 
        // cacheInfoToolStripMenuItem
        // 
        cacheInfoToolStripMenuItem.Name = "cacheInfoToolStripMenuItem";
        cacheInfoToolStripMenuItem.Size = new Size(140, 22);
        cacheInfoToolStripMenuItem.Text = "Cache &Info";
        cacheInfoToolStripMenuItem.Click += CacheInfoToolStripMenuItem_Click;

        // 
        // clearCacheToolStripMenuItem
        // 
        clearCacheToolStripMenuItem.Name = "clearCacheToolStripMenuItem";
        clearCacheToolStripMenuItem.Size = new Size(140, 22);
        clearCacheToolStripMenuItem.Text = "&Clear Cache";
        clearCacheToolStripMenuItem.Click += ClearCacheToolStripMenuItem_Click;

        // 
        // toolStripSeparator4
        // 
        toolStripSeparator4.Name = "toolStripSeparator4";
        toolStripSeparator4.Size = new Size(137, 6);

        // 
        // destinationToolStripMenuItem
        // 
        destinationToolStripMenuItem.Name = "destinationToolStripMenuItem";
        destinationToolStripMenuItem.Size = new Size(140, 22);
        destinationToolStripMenuItem.Text = "&Destination";
        destinationToolStripMenuItem.Click += DestinationToolStripMenuItem_Click;

        // 
        // exitToolStripMenuItem
        // 
        exitToolStripMenuItem.Name = "exitToolStripMenuItem";
        exitToolStripMenuItem.Size = new Size(140, 22);
        exitToolStripMenuItem.Text = "E&xit";
        exitToolStripMenuItem.Click += ExitToolStripMenuItem_Click;

        // 
        // helpToolStripMenuItem
        // 
        helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
        helpToolStripMenuItem.Name = "helpToolStripMenuItem";
        helpToolStripMenuItem.Size = new Size(44, 20);
        helpToolStripMenuItem.Text = "&Help";


        // 
        // aboutToolStripMenuItem
        // 
        aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
        aboutToolStripMenuItem.Size = new Size(160, 22);
        aboutToolStripMenuItem.Text = "&About";
        aboutToolStripMenuItem.Click += AboutToolStripMenuItem_Click;





        // 
        // statusStrip
        // 
        statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel, progressBar });
        statusStrip.Location = new Point(0, 878);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(2200, 22);
        statusStrip.TabIndex = 2;

        // 
        // statusLabel
        // 
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(39, 17);
        statusLabel.Text = "Ready";

        // 
        // progressBar
        // 
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(200, 16);
        progressBar.Visible = false;

        // 
        // mainPanel
        // 
        mainPanel.Controls.Add(mainSplitContainer);
        mainPanel.Dock = DockStyle.Fill;
        mainPanel.Location = new Point(0, 24);
        mainPanel.Name = "mainPanel";
        mainPanel.Size = new Size(2200, 929);
        mainPanel.TabIndex = 3;

        // 
        // mainSplitContainer
        // 
        mainSplitContainer.Dock = DockStyle.Fill;
        mainSplitContainer.Location = new Point(0, 0);
        mainSplitContainer.Name = "mainSplitContainer";
        mainSplitContainer.Orientation = Orientation.Horizontal;
        mainSplitContainer.Panel1.Controls.Add(romsSplitContainer);
        mainSplitContainer.Panel2.Controls.Add(detailsGroupBox);
        mainSplitContainer.Size = new Size(2200, 929);
        mainSplitContainer.SplitterDistance = 600;
        mainSplitContainer.TabIndex = 0;

        // 
        // romsSplitContainer
        // 
        romsSplitContainer.Dock = DockStyle.Fill;
        romsSplitContainer.Location = new Point(0, 0);
        romsSplitContainer.Name = "romsSplitContainer";
        romsSplitContainer.Orientation = Orientation.Vertical;
        romsSplitContainer.Panel1.Controls.Add(gamesGroupBox);
        romsSplitContainer.Panel2.Controls.Add(destinationGroupBox);
        romsSplitContainer.Size = new Size(2200, 600);
        romsSplitContainer.SplitterDistance = 1480;
        romsSplitContainer.TabIndex = 0;

        // 
        // gamesGroupBox
        // 
        gamesGroupBox.Controls.Add(gamesListView);
        gamesGroupBox.Controls.Add(searchPanel);
        gamesGroupBox.Dock = DockStyle.Fill;
        gamesGroupBox.Location = new Point(0, 0);
        gamesGroupBox.Name = "gamesGroupBox";
        gamesGroupBox.Size = new Size(2200, 829);
        gamesGroupBox.TabIndex = 0;
        gamesGroupBox.TabStop = false;
        gamesGroupBox.Text = "ROM Collection";

        // 
        // destinationGroupBox
        // 
        destinationGroupBox.Controls.Add(destinationListView);
        destinationGroupBox.Controls.Add(destinationPanel);
        destinationGroupBox.Dock = DockStyle.Fill;
        destinationGroupBox.Location = new Point(0, 0);
        destinationGroupBox.Name = "destinationGroupBox";
        destinationGroupBox.Size = new Size(996, 829);
        destinationGroupBox.TabIndex = 1;
        destinationGroupBox.TabStop = false;
        destinationGroupBox.Text = "Installed ROMs";

        // 
        // detailsGroupBox
        // 
        detailsGroupBox.Controls.Add(detailsTextBox);
        detailsGroupBox.Dock = DockStyle.Fill;
        detailsGroupBox.Location = new Point(0, 0);
        detailsGroupBox.Name = "detailsGroupBox";
        detailsGroupBox.Size = new Size(1800, 329);
        detailsGroupBox.TabIndex = 2;
        detailsGroupBox.TabStop = false;
        detailsGroupBox.Text = "ROM Details";

        // 
        // detailsTextBox
        // 
        detailsTextBox.Dock = DockStyle.Fill;
        detailsTextBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
        detailsTextBox.Location = new Point(3, 19);
        detailsTextBox.Multiline = true;
        detailsTextBox.Name = "detailsTextBox";
        detailsTextBox.ReadOnly = true;
        detailsTextBox.ScrollBars = ScrollBars.Vertical;
        detailsTextBox.Size = new Size(1794, 307);
        detailsTextBox.TabIndex = 0;
        detailsTextBox.Text = "Select a ROM to view details...";

        // 
        // destinationListView
        // 
        destinationListView.Dock = DockStyle.Fill;
        destinationListView.FullRowSelect = true;
        destinationListView.GridLines = true;
        destinationListView.Location = new Point(3, 19);
        destinationListView.Name = "destinationListView";
        destinationListView.Size = new Size(990, 807);
        destinationListView.TabIndex = 0;
        destinationListView.UseCompatibleStateImageBehavior = false;
        destinationListView.View = View.Details;
        // 
        // destinationPanel
        // 
        destinationPanel.Controls.Add(refreshDestinationButton);
        destinationPanel.Controls.Add(deleteDestinationButton);
        destinationPanel.Dock = DockStyle.Top;
        destinationPanel.Location = new Point(3, 19);
        destinationPanel.Name = "destinationPanel";
        destinationPanel.Size = new Size(990, 42);
        destinationPanel.TabIndex = 1;
        // 
        // refreshDestinationButton
        // 
        refreshDestinationButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        refreshDestinationButton.Location = new Point(912, 12);
        refreshDestinationButton.Name = "refreshDestinationButton";
        refreshDestinationButton.Size = new Size(75, 23);
        refreshDestinationButton.TabIndex = 0;
        refreshDestinationButton.Text = "Refresh";
        refreshDestinationButton.UseVisualStyleBackColor = true;
        refreshDestinationButton.Click += RefreshDestinationButton_Click;

        // 
        // deleteDestinationButton
        // 
        deleteDestinationButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        deleteDestinationButton.Location = new Point(831, 12);
        deleteDestinationButton.Name = "deleteDestinationButton";
        deleteDestinationButton.Size = new Size(75, 23);
        deleteDestinationButton.TabIndex = 1;
        deleteDestinationButton.Text = "Delete";
        deleteDestinationButton.UseVisualStyleBackColor = true;
        deleteDestinationButton.Click += DeleteDestinationButton_Click;

        // 
        // gamesListView
        // 
        gamesListView.Dock = DockStyle.Fill;
        gamesListView.Location = new Point(3, 84);
        gamesListView.Name = "gamesListView";
        gamesListView.Size = new Size(2194, 742);
        gamesListView.TabIndex = 1;
        gamesListView.UseCompatibleStateImageBehavior = false;

        // 
        // searchPanel
        // 
        searchPanel.Controls.Add(searchLabel);
        searchPanel.Controls.Add(searchTextBox);
        searchPanel.Controls.Add(showInstalledButton);
        searchPanel.Controls.Add(selectedButton);
        searchPanel.Controls.Add(selectAllButton);
        searchPanel.Controls.Add(clearAllButton);
        searchPanel.Controls.Add(verifyButton);
        searchPanel.Controls.Add(scanRomsButton);
        searchPanel.Controls.Add(copyRomsButton);
        searchPanel.Dock = DockStyle.Top;
        searchPanel.Location = new Point(3, 19);
        searchPanel.Name = "searchPanel";
        searchPanel.Size = new Size(2194, 65);
        searchPanel.TabIndex = 0;

        // 
        // clearAllButton
        // 
        clearAllButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        clearAllButton.Location = new Point(2116, 8);
        clearAllButton.Name = "clearAllButton";
        clearAllButton.Size = new Size(75, 23);
        clearAllButton.TabIndex = 9;
        clearAllButton.Text = "Clear All";
        clearAllButton.UseVisualStyleBackColor = true;
        clearAllButton.Click += ClearAllButton_Click;

        // 
        // verifyButton
        // 
        scanRomsButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        scanRomsButton.Location = new Point(6, 8);
        scanRomsButton.Name = "scanRomsButton";
        scanRomsButton.Size = new Size(85, 23);
        scanRomsButton.TabIndex = 7;
        scanRomsButton.Text = "Scan ROMs";
        scanRomsButton.UseVisualStyleBackColor = true;
        scanRomsButton.Click += ScanRomsButton_Click;

        // 
        // verifyButton
        // 
        verifyButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        verifyButton.Location = new Point(97, 8);
        verifyButton.Name = "verifyButton";
        verifyButton.Size = new Size(75, 23);
        verifyButton.TabIndex = 8;
        verifyButton.Text = "Verify";
        verifyButton.UseVisualStyleBackColor = true;
        verifyButton.Click += VerifyButton_Click;

        // 
        // copyRomsButton
        // 
        copyRomsButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        copyRomsButton.Enabled = false;
        copyRomsButton.Location = new Point(178, 8);
        copyRomsButton.Name = "copyRomsButton";
        copyRomsButton.Size = new Size(75, 23);
        copyRomsButton.TabIndex = 8;
        copyRomsButton.Text = "Copy ROMs";
        copyRomsButton.UseVisualStyleBackColor = true;

        // 
        // selectAllButton
        // 
        selectAllButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        selectAllButton.Location = new Point(2035, 8);
        selectAllButton.Name = "selectAllButton";
        selectAllButton.Size = new Size(75, 23);
        selectAllButton.TabIndex = 8;
        selectAllButton.Text = "Select All";
        selectAllButton.UseVisualStyleBackColor = true;
        selectAllButton.Click += SelectAllButton_Click;

        // 
        // showInstalledButton
        // 
        showInstalledButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        showInstalledButton.Location = new Point(1953, 8);
        showInstalledButton.Name = "showInstalledButton";
        showInstalledButton.Size = new Size(75, 23);
        showInstalledButton.TabIndex = 8;
        showInstalledButton.Text = "Installed";
        showInstalledButton.UseVisualStyleBackColor = true;
        showInstalledButton.Click += ShowInstalledButton_Click;

        // 
        // selectedButton
        // 
        selectedButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        selectedButton.Location = new Point(1867, 8);
        selectedButton.Name = "selectedButton";
        selectedButton.Size = new Size(75, 23);
        selectedButton.TabIndex = 9;
        selectedButton.Text = "Selected";
        selectedButton.UseVisualStyleBackColor = true;
        selectedButton.Click += SelectedButton_Click;


        // 
        // searchTextBox
        // 
        searchTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        searchTextBox.Location = new Point(60, 32);
        searchTextBox.Name = "searchTextBox";
        searchTextBox.PlaceholderText = "Search ROMs by name, description, or manufacturer...";
        searchTextBox.Size = new Size(2125, 23);
        searchTextBox.TabIndex = 1;
        searchTextBox.TextChanged += SearchTextBox_TextChanged;

        // 
        // searchLabel
        // 
        searchLabel.AutoSize = true;
        searchLabel.Location = new Point(9, 35);
        searchLabel.Name = "searchLabel";
        searchLabel.Size = new Size(45, 15);
        searchLabel.TabIndex = 0;
        searchLabel.Text = "Search:";

        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(2200, 1000);
        FormBorderStyle = FormBorderStyle.Sizable;
        // Icon is embedded via ApplicationIcon property in project file
        MaximizeBox = true;
        MinimizeBox = true;
        MinimumSize = new Size(1600, 800);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(mainPanel);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;
        Name = "MainForm";
        Text = "MAME ROM Selector";
        menuStrip.ResumeLayout(false);
        menuStrip.PerformLayout();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        mainPanel.ResumeLayout(false);
        romsSplitContainer.ResumeLayout(false);
        gamesGroupBox.ResumeLayout(false);
        destinationGroupBox.ResumeLayout(false);
        destinationPanel.ResumeLayout(false);
        detailsGroupBox.ResumeLayout(false);
        searchPanel.ResumeLayout(false);
        searchPanel.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private MenuStrip menuStrip;
    private ToolStripMenuItem fileToolStripMenuItem;
    private ToolStripMenuItem settingsToolStripMenuItem;
    private ToolStripSeparator toolStripSeparator3;
    private ToolStripMenuItem cacheInfoToolStripMenuItem;
    private ToolStripMenuItem clearCacheToolStripMenuItem;
    private ToolStripSeparator toolStripSeparator4;
    private ToolStripMenuItem destinationToolStripMenuItem;
    private ToolStripSeparator toolStripSeparator5;
    private ToolStripMenuItem exitToolStripMenuItem;
    private ToolStripMenuItem helpToolStripMenuItem;
    private ToolStripMenuItem aboutToolStripMenuItem;
    private Button scanRomsButton;
    private Button copyRomsButton;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel statusLabel;
    private ToolStripProgressBar progressBar;
    private Panel mainPanel;
    private SplitContainer mainSplitContainer;
    private SplitContainer romsSplitContainer;
    private GroupBox gamesGroupBox;
    private ListView gamesListView;
    private GroupBox destinationGroupBox;
    private ListView destinationListView;
    private Panel destinationPanel;
    private Button refreshDestinationButton;
    private Button deleteDestinationButton;
    private GroupBox detailsGroupBox;
    private TextBox detailsTextBox;
    private Panel searchPanel;
    private Button clearAllButton;
    private Button selectAllButton;
    private Button showInstalledButton;
    private Button selectedButton;
    private Button verifyButton;
    private TextBox searchTextBox;
    private Label searchLabel;
}