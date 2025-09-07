namespace MameSelector.Forms;

partial class ProgressForm
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
        labelStatus = new Label();
        progressBar = new ProgressBar();
        SuspendLayout();
        
        // 
        // labelStatus
        // 
        labelStatus.AutoSize = true;
        labelStatus.Location = new Point(12, 15);
        labelStatus.Name = "labelStatus";
        labelStatus.Size = new Size(42, 15);
        labelStatus.TabIndex = 0;
        labelStatus.Text = "Status";
        
        // 
        // progressBar
        // 
        progressBar.Location = new Point(12, 40);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(360, 23);
        progressBar.TabIndex = 1;
        
        // 
        // ProgressForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(384, 81);
        Controls.Add(progressBar);
        Controls.Add(labelStatus);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ProgressForm";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Progress";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label labelStatus;
    private ProgressBar progressBar;
}

