namespace MameSelector.Forms
{
    partial class AboutForm
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
            iconPictureBox = new PictureBox();
            titleLabel = new Label();
            versionLabel = new Label();
            descriptionLabel = new Label();
            featuresLabel = new Label();
            okButton = new Button();
            ((System.ComponentModel.ISupportInitialize)iconPictureBox).BeginInit();
            SuspendLayout();
            // 
            // iconPictureBox
            // 
            iconPictureBox.Location = new Point(20, 20);
            iconPictureBox.Name = "iconPictureBox";
            iconPictureBox.Size = new Size(64, 64);
            iconPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            iconPictureBox.TabIndex = 0;
            iconPictureBox.TabStop = false;
            // 
            // titleLabel
            // 
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            titleLabel.Location = new Point(100, 20);
            titleLabel.Name = "titleLabel";
            titleLabel.Size = new Size(200, 25);
            titleLabel.TabIndex = 1;
            titleLabel.Text = "MAME ROM Selector";
            // 
            // versionLabel
            // 
            versionLabel.AutoSize = true;
            versionLabel.Font = new Font("Segoe UI", 10F);
            versionLabel.Location = new Point(100, 50);
            versionLabel.Name = "versionLabel";
            versionLabel.Size = new Size(60, 19);
            versionLabel.TabIndex = 2;
            versionLabel.Text = "Version 0.3";
            // 
            // descriptionLabel
            // 
            descriptionLabel.AutoSize = true;
            descriptionLabel.Location = new Point(20, 100);
            descriptionLabel.MaximumSize = new Size(400, 0);
            descriptionLabel.Name = "descriptionLabel";
            descriptionLabel.Size = new Size(380, 30);
            descriptionLabel.TabIndex = 3;
            descriptionLabel.Text = "A tool for selecting and copying MAME ROMs from a full romset with support for CHD files and comprehensive metadata matching.";
            // 
            // featuresLabel
            // 
            featuresLabel.AutoSize = true;
            featuresLabel.Location = new Point(20, 150);
            featuresLabel.MaximumSize = new Size(400, 0);
            featuresLabel.Name = "featuresLabel";
            featuresLabel.Size = new Size(380, 75);
            featuresLabel.TabIndex = 4;
            featuresLabel.Text = "Features:\r\n• Scans actual ROM files first\r\n• Matches with MAME XML metadata\r\n• Optimized for large collections\r\n• Virtual list view for performance\r\n• Professional icon and clean UI";
            // 
            // okButton
            // 
            okButton.Location = new Point(325, 250);
            okButton.Name = "okButton";
            okButton.Size = new Size(75, 23);
            okButton.TabIndex = 5;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += okButton_Click;
            // 
            // AboutForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(420, 290);
            Controls.Add(okButton);
            Controls.Add(featuresLabel);
            Controls.Add(descriptionLabel);
            Controls.Add(versionLabel);
            Controls.Add(titleLabel);
            Controls.Add(iconPictureBox);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "About MAME ROM Selector";
            ((System.ComponentModel.ISupportInitialize)iconPictureBox).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox iconPictureBox;
        private Label titleLabel;
        private Label versionLabel;
        private Label descriptionLabel;
        private Label featuresLabel;
        private Button okButton;
    }
}
