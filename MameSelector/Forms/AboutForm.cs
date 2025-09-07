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
        }

        private void LoadIcon()
        {
            try
            {
                // Try to get the icon from the main form's embedded resources
                var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                if (mainForm?.Icon != null)
                {
                    iconPictureBox.Image = mainForm.Icon.ToBitmap();
                }
                else
                {
                    // Try to load from embedded resources directly
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    var resourceName = "MameSelector.Resources.mame-rom-selector.ico";
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var icon = new Icon(stream);
                        iconPictureBox.Image = icon.ToBitmap();
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
    }
}
