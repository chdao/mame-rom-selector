namespace MameSelector.Forms;

partial class SettingsForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        groupBoxPaths = new GroupBox();
        labelMameXml = new Label();
        textBoxMameXml = new TextBox();
        buttonBrowseMameXml = new Button();
        labelRomRepository = new Label();
        textBoxRomRepository = new TextBox();
        buttonBrowseRomRepository = new Button();
        labelDestination = new Label();
        textBoxDestination = new TextBox();
        buttonBrowseDestination = new Button();
        labelCHDRepository = new Label();
        textBoxCHDRepository = new TextBox();
        buttonBrowseCHDRepository = new Button();
        groupBoxOptions = new GroupBox();
        checkBoxCopyBios = new CheckBox();
        checkBoxCopyDevice = new CheckBox();
        checkBoxCreateSubfolders = new CheckBox();
        checkBoxVerifyChecksums = new CheckBox();
        checkBoxPortableMode = new CheckBox();
        buttonOK = new Button();
        buttonCancel = new Button();
        
        groupBoxPaths.SuspendLayout();
        groupBoxOptions.SuspendLayout();
        SuspendLayout();
        
        // 
        // groupBoxPaths
        // 
        groupBoxPaths.Controls.Add(buttonBrowseCHDRepository);
        groupBoxPaths.Controls.Add(textBoxCHDRepository);
        groupBoxPaths.Controls.Add(labelCHDRepository);
        groupBoxPaths.Controls.Add(buttonBrowseDestination);
        groupBoxPaths.Controls.Add(textBoxDestination);
        groupBoxPaths.Controls.Add(labelDestination);
        groupBoxPaths.Controls.Add(buttonBrowseRomRepository);
        groupBoxPaths.Controls.Add(textBoxRomRepository);
        groupBoxPaths.Controls.Add(labelRomRepository);
        groupBoxPaths.Controls.Add(buttonBrowseMameXml);
        groupBoxPaths.Controls.Add(textBoxMameXml);
        groupBoxPaths.Controls.Add(labelMameXml);
        groupBoxPaths.Location = new Point(12, 12);
        groupBoxPaths.Name = "groupBoxPaths";
        groupBoxPaths.Size = new Size(560, 160);
        groupBoxPaths.TabIndex = 0;
        groupBoxPaths.TabStop = false;
        groupBoxPaths.Text = "File Paths";
        
        // 
        // labelMameXml
        // 
        labelMameXml.AutoSize = true;
        labelMameXml.Location = new Point(6, 25);
        labelMameXml.Name = "labelMameXml";
        labelMameXml.Size = new Size(87, 15);
        labelMameXml.TabIndex = 0;
        labelMameXml.Text = "MAME XML File:";
        
        // 
        // textBoxMameXml
        // 
        textBoxMameXml.Location = new Point(120, 22);
        textBoxMameXml.Name = "textBoxMameXml";
        textBoxMameXml.Size = new Size(350, 23);
        textBoxMameXml.TabIndex = 1;
        
        // 
        // buttonBrowseMameXml
        // 
        buttonBrowseMameXml.Location = new Point(476, 21);
        buttonBrowseMameXml.Name = "buttonBrowseMameXml";
        buttonBrowseMameXml.Size = new Size(75, 25);
        buttonBrowseMameXml.TabIndex = 2;
        buttonBrowseMameXml.Text = "Browse...";
        buttonBrowseMameXml.UseVisualStyleBackColor = true;
        buttonBrowseMameXml.Click += ButtonBrowseMameXml_Click;
        
        // 
        // labelRomRepository
        // 
        labelRomRepository.AutoSize = true;
        labelRomRepository.Location = new Point(6, 54);
        labelRomRepository.Name = "labelRomRepository";
        labelRomRepository.Size = new Size(95, 15);
        labelRomRepository.TabIndex = 3;
        labelRomRepository.Text = "ROM Repository:";
        
        // 
        // textBoxRomRepository
        // 
        textBoxRomRepository.Location = new Point(120, 51);
        textBoxRomRepository.Name = "textBoxRomRepository";
        textBoxRomRepository.Size = new Size(350, 23);
        textBoxRomRepository.TabIndex = 4;
        
        // 
        // buttonBrowseRomRepository
        // 
        buttonBrowseRomRepository.Location = new Point(476, 50);
        buttonBrowseRomRepository.Name = "buttonBrowseRomRepository";
        buttonBrowseRomRepository.Size = new Size(75, 25);
        buttonBrowseRomRepository.TabIndex = 5;
        buttonBrowseRomRepository.Text = "Browse...";
        buttonBrowseRomRepository.UseVisualStyleBackColor = true;
        buttonBrowseRomRepository.Click += ButtonBrowseRomRepository_Click;
        
        // 
        // labelDestination
        // 
        labelDestination.AutoSize = true;
        labelDestination.Location = new Point(6, 83);
        labelDestination.Name = "labelDestination";
        labelDestination.Size = new Size(71, 15);
        labelDestination.TabIndex = 6;
        labelDestination.Text = "Destination:";
        
        // 
        // textBoxDestination
        // 
        textBoxDestination.Location = new Point(120, 80);
        textBoxDestination.Name = "textBoxDestination";
        textBoxDestination.Size = new Size(350, 23);
        textBoxDestination.TabIndex = 7;
        
        // 
        // buttonBrowseDestination
        // 
        buttonBrowseDestination.Location = new Point(476, 79);
        buttonBrowseDestination.Name = "buttonBrowseDestination";
        buttonBrowseDestination.Size = new Size(75, 25);
        buttonBrowseDestination.TabIndex = 8;
        buttonBrowseDestination.Text = "Browse...";
        buttonBrowseDestination.UseVisualStyleBackColor = true;
        buttonBrowseDestination.Click += ButtonBrowseDestination_Click;
        
        // 
        // labelCHDRepository
        // 
        labelCHDRepository.AutoSize = true;
        labelCHDRepository.Location = new Point(6, 112);
        labelCHDRepository.Name = "labelCHDRepository";
        labelCHDRepository.Size = new Size(96, 15);
        labelCHDRepository.TabIndex = 9;
        labelCHDRepository.Text = "CHD Repository:";
        
        // 
        // textBoxCHDRepository
        // 
        textBoxCHDRepository.Location = new Point(120, 109);
        textBoxCHDRepository.Name = "textBoxCHDRepository";
        textBoxCHDRepository.Size = new Size(350, 23);
        textBoxCHDRepository.TabIndex = 10;
        
        // 
        // buttonBrowseCHDRepository
        // 
        buttonBrowseCHDRepository.Location = new Point(476, 108);
        buttonBrowseCHDRepository.Name = "buttonBrowseCHDRepository";
        buttonBrowseCHDRepository.Size = new Size(75, 25);
        buttonBrowseCHDRepository.TabIndex = 11;
        buttonBrowseCHDRepository.Text = "Browse...";
        buttonBrowseCHDRepository.UseVisualStyleBackColor = true;
        buttonBrowseCHDRepository.Click += ButtonBrowseCHDRepository_Click;
        
        // 
        // groupBoxOptions
        // 
        groupBoxOptions.Controls.Add(checkBoxPortableMode);
        groupBoxOptions.Controls.Add(checkBoxVerifyChecksums);
        groupBoxOptions.Controls.Add(checkBoxCreateSubfolders);
        groupBoxOptions.Controls.Add(checkBoxCopyDevice);
        groupBoxOptions.Controls.Add(checkBoxCopyBios);
        groupBoxOptions.Location = new Point(12, 178);
        groupBoxOptions.Name = "groupBoxOptions";
        groupBoxOptions.Size = new Size(560, 120);
        groupBoxOptions.TabIndex = 1;
        groupBoxOptions.TabStop = false;
        groupBoxOptions.Text = "Copy Options";
        
        // 
        // checkBoxCopyBios
        // 
        checkBoxCopyBios.AutoSize = true;
        checkBoxCopyBios.Location = new Point(15, 25);
        checkBoxCopyBios.Name = "checkBoxCopyBios";
        checkBoxCopyBios.Size = new Size(108, 19);
        checkBoxCopyBios.TabIndex = 0;
        checkBoxCopyBios.Text = "Copy BIOS files";
        checkBoxCopyBios.UseVisualStyleBackColor = true;
        
        // 
        // checkBoxCopyDevice
        // 
        checkBoxCopyDevice.AutoSize = true;
        checkBoxCopyDevice.Location = new Point(15, 50);
        checkBoxCopyDevice.Name = "checkBoxCopyDevice";
        checkBoxCopyDevice.Size = new Size(115, 19);
        checkBoxCopyDevice.TabIndex = 1;
        checkBoxCopyDevice.Text = "Copy device files";
        checkBoxCopyDevice.UseVisualStyleBackColor = true;
        
        // 
        // checkBoxCreateSubfolders
        // 
        checkBoxCreateSubfolders.AutoSize = true;
        checkBoxCreateSubfolders.Location = new Point(15, 75);
        checkBoxCreateSubfolders.Name = "checkBoxCreateSubfolders";
        checkBoxCreateSubfolders.Size = new Size(122, 19);
        checkBoxCreateSubfolders.TabIndex = 2;
        checkBoxCreateSubfolders.Text = "Create subfolders";
        checkBoxCreateSubfolders.UseVisualStyleBackColor = true;
        
        // 
        // checkBoxVerifyChecksums
        // 
        checkBoxVerifyChecksums.AutoSize = true;
        checkBoxVerifyChecksums.Location = new Point(200, 25);
        checkBoxVerifyChecksums.Name = "checkBoxVerifyChecksums";
        checkBoxVerifyChecksums.Size = new Size(125, 19);
        checkBoxVerifyChecksums.TabIndex = 3;
        checkBoxVerifyChecksums.Text = "Verify checksums";
        checkBoxVerifyChecksums.UseVisualStyleBackColor = true;
        
        // 
        // checkBoxPortableMode
        // 
        checkBoxPortableMode.AutoSize = true;
        checkBoxPortableMode.Location = new Point(200, 50);
        checkBoxPortableMode.Name = "checkBoxPortableMode";
        checkBoxPortableMode.Size = new Size(200, 19);
        checkBoxPortableMode.TabIndex = 4;
        checkBoxPortableMode.Text = "Portable mode (store cache alongside exe)";
        checkBoxPortableMode.UseVisualStyleBackColor = true;
        
        // 
        // buttonOK
        // 
        buttonOK.Location = new Point(416, 314);
        buttonOK.Name = "buttonOK";
        buttonOK.Size = new Size(75, 30);
        buttonOK.TabIndex = 2;
        buttonOK.Text = "OK";
        buttonOK.UseVisualStyleBackColor = true;
        buttonOK.Click += ButtonOK_Click;
        
        // 
        // buttonCancel
        // 
        buttonCancel.Location = new Point(497, 314);
        buttonCancel.Name = "buttonCancel";
        buttonCancel.Size = new Size(75, 30);
        buttonCancel.TabIndex = 3;
        buttonCancel.Text = "Cancel";
        buttonCancel.UseVisualStyleBackColor = true;
        buttonCancel.Click += ButtonCancel_Click;
        
        // 
        // SettingsForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(584, 356);
        Controls.Add(buttonCancel);
        Controls.Add(buttonOK);
        Controls.Add(groupBoxOptions);
        Controls.Add(groupBoxPaths);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "SettingsForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Settings";
        groupBoxPaths.ResumeLayout(false);
        groupBoxPaths.PerformLayout();
        groupBoxOptions.ResumeLayout(false);
        groupBoxOptions.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private GroupBox groupBoxPaths;
    private Label labelMameXml;
    private TextBox textBoxMameXml;
    private Button buttonBrowseMameXml;
    private Label labelRomRepository;
    private TextBox textBoxRomRepository;
    private Button buttonBrowseRomRepository;
    private Label labelDestination;
    private TextBox textBoxDestination;
    private Button buttonBrowseDestination;
    private Label labelCHDRepository;
    private TextBox textBoxCHDRepository;
    private Button buttonBrowseCHDRepository;
    private GroupBox groupBoxOptions;
    private CheckBox checkBoxCopyBios;
    private CheckBox checkBoxCopyDevice;
    private CheckBox checkBoxCreateSubfolders;
    private CheckBox checkBoxVerifyChecksums;
    private CheckBox checkBoxPortableMode;
    private Button buttonOK;
    private Button buttonCancel;
}

