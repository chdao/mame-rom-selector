using MameSelector.Models;

namespace MameSelector.Forms;

public partial class SettingsForm : Form
{
    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        InitializeComponent();
        Settings = new AppSettings
        {
            MameXmlPath = settings.MameXmlPath,
            RomRepositoryPath = settings.RomRepositoryPath,
            DestinationPath = settings.DestinationPath,
            CHDRepositoryPath = settings.CHDRepositoryPath,
            CopyBiosFiles = settings.CopyBiosFiles,
            CopyDeviceFiles = settings.CopyDeviceFiles,
            CreateSubfolders = settings.CreateSubfolders,
            VerifyChecksums = settings.VerifyChecksums
        };

        LoadSettingsToUI();
    }

    private void LoadSettingsToUI()
    {
        textBoxMameXml.Text = Settings.MameXmlPath;
        textBoxRomRepository.Text = Settings.RomRepositoryPath;
        textBoxDestination.Text = Settings.DestinationPath;
        textBoxCHDRepository.Text = Settings.CHDRepositoryPath;
        checkBoxCopyBios.Checked = Settings.CopyBiosFiles;
        checkBoxCopyDevice.Checked = Settings.CopyDeviceFiles;
        checkBoxCreateSubfolders.Checked = Settings.CreateSubfolders;
        checkBoxVerifyChecksums.Checked = Settings.VerifyChecksums;
    }

    private void SaveSettingsFromUI()
    {
        Settings.MameXmlPath = textBoxMameXml.Text.Trim();
        Settings.RomRepositoryPath = textBoxRomRepository.Text.Trim();
        Settings.DestinationPath = textBoxDestination.Text.Trim();
        Settings.CHDRepositoryPath = textBoxCHDRepository.Text.Trim();
        Settings.CopyBiosFiles = checkBoxCopyBios.Checked;
        Settings.CopyDeviceFiles = checkBoxCopyDevice.Checked;
        Settings.CreateSubfolders = checkBoxCreateSubfolders.Checked;
        Settings.VerifyChecksums = checkBoxVerifyChecksums.Checked;
    }

    private void ButtonBrowseMameXml_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select MAME XML File",
            Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrEmpty(textBoxMameXml.Text))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(textBoxMameXml.Text);
            dialog.FileName = Path.GetFileName(textBoxMameXml.Text);
        }

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBoxMameXml.Text = dialog.FileName;
        }
    }

    private void ButtonBrowseRomRepository_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select ROM Repository Directory",
            UseDescriptionForTitle = true,
            SelectedPath = textBoxRomRepository.Text
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBoxRomRepository.Text = dialog.SelectedPath;
        }
    }

    private void ButtonBrowseDestination_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Destination Directory",
            UseDescriptionForTitle = true,
            SelectedPath = textBoxDestination.Text
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBoxDestination.Text = dialog.SelectedPath;
        }
    }

    private void ButtonBrowseCHDRepository_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select CHD Repository Directory",
            UseDescriptionForTitle = true,
            SelectedPath = textBoxCHDRepository.Text
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            textBoxCHDRepository.Text = dialog.SelectedPath;
        }
    }

    private void ButtonOK_Click(object sender, EventArgs e)
    {
        SaveSettingsFromUI();

        var validationErrors = Settings.Validate();
        if (validationErrors.Any())
        {
            MessageBox.Show($"Please fix the following errors:\n\n{string.Join("\n", validationErrors)}", 
                "Validation Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void ButtonCancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}

