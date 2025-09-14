using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MameSelector.Forms
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
            LoadIcon();
            SetVersion();
        }

        private void SetVersion()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var versionString = $"{version.Major}.{version.Minor}.{version.Build}";
            versionLabel.Text = $"Version {versionString}";
        }

        private void LoadIcon()
        {
            try
            {
                // Try to load from embedded resources directly to get the highest resolution
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "MameSelector.Resources.mame-rom-selector.ico";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    // Try to get the largest available icon size (128x128, 64x64, 48x48, 32x32)
                    var sizes = new[] { 128, 64, 48, 32, 16 };
                    Icon? bestIcon = null;
                    
                    foreach (var size in sizes)
                    {
                        try
                        {
                            stream.Position = 0; // Reset stream position
                            var testIcon = new Icon(stream, new Size(size, size));
                            bestIcon = testIcon;
                            break; // Use the first successful size
                        }
                        catch
                        {
                            // Try next size
                            continue;
                        }
                    }
                    
                    if (bestIcon != null)
                    {
                        iconPictureBox.Image = bestIcon.ToBitmap();
                        bestIcon.Dispose();
                    }
                }
                else
                {
                    // Fallback: Try to get the icon from the main form
                    var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                    if (mainForm?.Icon != null)
                    {
                        // Try to get a larger size from the main form's icon
                        try
                        {
                            using var largeIcon = new Icon(mainForm.Icon, new Size(64, 64));
                            iconPictureBox.Image = largeIcon.ToBitmap();
                        }
                        catch
                        {
                            // Fallback to default size
                            iconPictureBox.Image = mainForm.Icon.ToBitmap();
                        }
                    }
                }
            }
            catch
            {
                // If icon loading fails, continue without icon
            }
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void projectLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/chdao/mame-rom-selector",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open the project link: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
