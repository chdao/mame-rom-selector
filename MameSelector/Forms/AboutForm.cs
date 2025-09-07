using System;
using System.Drawing;
using System.Windows.Forms;

namespace MameSelector.Forms
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
            LoadIcon();
        }

        private void LoadIcon()
        {
            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "mame-rom-selector.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    using var icon = new Icon(iconPath);
                    iconPictureBox.Image = icon.ToBitmap();
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
    }
}
