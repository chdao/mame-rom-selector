using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MameSelector.UI
{
    /// <summary>
    /// Custom progress bar that displays percentage text inside the bar
    /// </summary>
    public class CustomProgressBar : ToolStripControlHost
    {
        private readonly TextProgressBar _progressBar;
        private string _text = "";

        public CustomProgressBar() : base(new TextProgressBar())
        {
            _progressBar = (TextProgressBar)Control;
            _progressBar.Style = ProgressBarStyle.Continuous;
        }

        public int Value
        {
            get => _progressBar.Value;
            set => _progressBar.Value = value;
        }

        public int Maximum
        {
            get => _progressBar.Maximum;
            set => _progressBar.Maximum = value;
        }

        public int Minimum
        {
            get => _progressBar.Minimum;
            set => _progressBar.Minimum = value;
        }

        public ProgressBarStyle Style
        {
            get => _progressBar.Style;
            set => _progressBar.Style = value;
        }

        public void SetText(string text)
        {
            _text = text;
            _progressBar.SetText(text);
        }
    }

    /// <summary>
    /// Progress bar that can display text inside it
    /// </summary>
    public class TextProgressBar : ProgressBar
    {
        private string _text = "";
        private Font _textFont;
        private Brush _textBrush;
        private System.Windows.Forms.Timer _animationTimer;
        private int _animationOffset = 0;

        public TextProgressBar()
        {
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);
            _textFont = new Font("Segoe UI", 7, FontStyle.Bold);
            _textBrush = new SolidBrush(Color.Black);
            
            // Initialize animation timer
            _animationTimer = new System.Windows.Forms.Timer();
            _animationTimer.Interval = 50; // 20 FPS
            _animationTimer.Tick += AnimationTimer_Tick;
            _animationTimer.Start();
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            _animationOffset = (_animationOffset + 2) % 40; // Animate every 2 pixels, cycle every 40 pixels
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            var g = e.Graphics;
            
            // Draw background
            g.FillRectangle(SystemBrushes.Control, rect);
            
            // Draw progress fill with green color and animation
            if (Value > 0)
            {
                var progressWidth = (int)((double)Value / Maximum * rect.Width);
                var progressRect = new Rectangle(0, 0, progressWidth, rect.Height);
                
                // Create animated green gradient brush
                var gradientRect = new Rectangle(-_animationOffset, 0, rect.Width + 40, rect.Height);
                using (var brush = new LinearGradientBrush(gradientRect, 
                    Color.FromArgb(80, 180, 80), Color.FromArgb(120, 220, 120), 
                    LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(brush, progressRect);
                }
                
                // Add animated highlight effect
                if (progressWidth > 20)
                {
                    var highlightWidth = Math.Min(20, progressWidth / 3);
                    var highlightRect = new Rectangle(progressWidth - highlightWidth, 0, highlightWidth, rect.Height);
                    using (var highlightBrush = new LinearGradientBrush(highlightRect, 
                        Color.FromArgb(180, 255, 180), Color.FromArgb(120, 220, 120), 
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(highlightBrush, highlightRect);
                    }
                }
            }
            
            // Draw border
            g.DrawRectangle(SystemPens.ControlDark, rect);
            
            // Draw percentage text in the center of the progress bar
            if (!string.IsNullOrEmpty(_text))
            {
                // Use TextRenderingHint for better text quality
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                
                var textSize = g.MeasureString(_text, _textFont);
                
                // Center the text both horizontally and vertically
                var x = (rect.Width - textSize.Width) / 2;
                var y = (rect.Height - textSize.Height) / 2;
                
                // Draw text with a slight shadow for better visibility
                g.DrawString(_text, _textFont, Brushes.White, x + 1, y + 1);
                g.DrawString(_text, _textFont, _textBrush, x, y);
            }
        }

        public void SetText(string text)
        {
            if (_text != text)
            {
                _text = text;
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Stop();
                _animationTimer?.Dispose();
                _textFont?.Dispose();
                _textBrush?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
