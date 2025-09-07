namespace MameSelector.Forms;

public partial class ProgressForm : Form
{
    public ProgressForm(string title)
    {
        InitializeComponent();
        Text = title;
        labelStatus.Text = title;
    }

    public void UpdateProgress(int percentage)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<int>(UpdateProgress), percentage);
            return;
        }

        progressBar.Value = Math.Min(Math.Max(percentage, 0), 100);
        labelStatus.Text = $"{Text} - {percentage}%";
        Application.DoEvents();
    }

    public void UpdateStatus(string status)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(UpdateStatus), status);
            return;
        }

        labelStatus.Text = status;
        Application.DoEvents();
    }
}

