using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Globalization;

namespace ColorPicker
{

    public partial class MainForm : Form
    {
        private readonly Timer getColor;
        private bool txtHexFullBlocked = false;
        private bool txtHexShortBlocked = false;
        // Sample
        private Bitmap sampleBitmap = null;
        private bool hasSampled = false;
        private const int sampleSize = 5;
        private readonly Color[,] previewColors;
        private Color _sampleColor = Color.Black;
        private Color sampleColor
        {
            get { return _sampleColor; }
            set
            {
                _sampleColor = value;
                this.btnPickColor.BackColor = _sampleColor;
            }
        }
        // Sample preview
        private const int previewSize = 80;
        private const int previewX = 10;
        private const int previewY = 40;
        // Move form dependencies
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref Point lpPoint);
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
        public static extern int BitBlt(IntPtr hDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        Bitmap screenPixel = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        private bool autoGetColour;

        public MainForm()
        {
            InitializeComponent();
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);

            chkPin.Checked = Properties.Settings.Default.StayOnTop;
            this.TopMost = chkPin.Checked;

            // Setup get color timer
            getColor = new Timer { Interval = 10 };
            getColor.Tick += new EventHandler(GetColorOutOfAppTick);

            previewColors = new Color[previewSize, previewSize];
        }

        /// <summary>
        /// Populates textboxes with correct color values.
        /// </summary>
        private void TranslateColor()
        {
            if (!hasSampled) return;
            UpdateColourText(sampleColor);
        }

        /// <summary>
        /// Gets the contrast color based on specified color.
        /// </summary>
        /// <param name="color">Color to compare to.</param>
        /// <returns>A white or dark gray color based on color.</returns>
        private Color GetContrastColor(Color color)
        {
            int yiq = ((color.R * 299) + (color.G * 587) + (color.B)) / 1000;
            if (yiq >= 131.5)
                return Color.FromArgb(255, 33, 33, 33);
            else
                return Color.White;
        }

        /// <summary>
        /// Opens the color chooser dialog.
        /// </summary>
        private void OpenColorChooserDialog()
        {
            if (this.autoGetColour) return;

            var colorDialog = new ColorDialog { Color = sampleColor, FullOpen = true };
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                sampleColor = colorDialog.Color;
                TranslateColor();
            }
        }

        protected override void OnPaint(PaintEventArgs paintEvnt)
        {
            Graphics gfx = paintEvnt.Graphics;
            Brush brush;
            Pen pen = new Pen(Color.FromArgb(255, 255, 0, 0), 2);
            int blockSize = previewSize / sampleSize;

            // Update UI for color contrast
            lbTitle.ForeColor = GetContrastColor(sampleColor);
            btnPickColor.FlatAppearance.BorderColor = GetContrastColor(sampleColor);

            // Draw magnified preview
            for (int i = 0; i < sampleSize; i++)
            {
                for (int j = 0; j < sampleSize; j++)
                {
                    if (sampleBitmap != null)
                    {
                        previewColors[i, j] = sampleBitmap.GetPixel(i, j);
                    }
                    brush = new SolidBrush(previewColors[i, j]);
                    gfx.FillRectangle(brush, new Rectangle(previewX + i * blockSize, previewY + j * blockSize, blockSize, blockSize));

                }
            }

            // Draw preview border
            if (hasSampled)
            {
                gfx.DrawRectangle(pen, previewX + blockSize * (sampleSize / 2), previewY + blockSize * (sampleSize / 2), blockSize, blockSize);
            }

            // Clean up
            if (sampleBitmap != null)
            {
                sampleBitmap.Dispose();
                sampleBitmap = null;
            }
        }

        private void chkPin_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = chkPin.Checked;
            Properties.Settings.Default.StayOnTop = chkPin.Checked;
            Properties.Settings.Default.Save();
        }

        private void btnOpenColorChooser_Click(object sender, EventArgs e)
        {
            OpenColorChooserDialog();
        }

        private void txtHexFull_TextChanged(object sender, EventArgs e)
        {
            if (this.autoGetColour) return;

            if (txtHexFull.Text.Length == 7 && txtHexFull.Text.StartsWith("#"))
            {
                sampleColor = ColorTranslator.FromHtml(txtHexFull.Text);
                TranslateColor();
            }
            else if (txtHexFull.Text.Length == 4 && txtHexFull.Text.StartsWith("#"))
            {
                sampleColor = ColorTranslator.FromHtml(txtHexFull.Text);
                txtHexFullBlocked = true;
                TranslateColor();
            }
        }

        private void txtHexShort_TextChanged(object sender, EventArgs e)
        {
            if (this.autoGetColour) return;

            try
            {
                if (txtHexShort.Text.Length == 6)
                {
                    sampleColor = ColorTranslator.FromHtml("#" + txtHexShort.Text);
                    TranslateColor();
                }
                else if (txtHexShort.Text.Length == 3)
                {
                    sampleColor = ColorTranslator.FromHtml("#" + txtHexShort.Text);
                    txtHexShortBlocked = true;
                    TranslateColor();
                }
            }
            catch
            {
                sampleColor = Color.Black;
                TranslateColor();
            }
        }

        private void txtHexFull_Leave(object sender, EventArgs e)
        {
            txtHexFullBlocked = false;
        }

        private void txtHexShort_Leave(object sender, EventArgs e)
        {
            txtHexShortBlocked = false;
        }

        private void txtHexFull_KeyPress(object sender, KeyPressEventArgs e)
        {
            var keys = new int[] { 'a', 'b', 'c', 'd', 'e', 'f', 'A', 'B', 'C', 'D', 'E', 'F', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '\b', '#' };
            if (!keys.Contains(e.KeyChar))
                e.Handled = true;
        }

        private void TextBoxSelectAll(object sender, EventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private void TextBoxCopyToClipboard(object sender, EventArgs e)
        {
            Clipboard.SetText(((TextBox)sender).Text);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void MainForm_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // Drag form to move
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Escape to close
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            } else if(keyData == Keys.S)
            {
                this.ToggleAutoRead();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void contextFloat_Click(object sender, EventArgs e)
        {
            if (this.autoGetColour) return;
            Properties.Settings.Default.UseFloat = true;
            Properties.Settings.Default.Save();
            TranslateColor();
        }

        private void contextByte_Click(object sender, EventArgs e)
        {
            if (this.autoGetColour) return;
            Properties.Settings.Default.UseFloat = false;
            Properties.Settings.Default.Save();
            TranslateColor();
        }

        public Color GetColorAt(Point location)
        {
            using (Graphics gdest = Graphics.FromImage(screenPixel))
            {
                using (Graphics gsrc = Graphics.FromHwnd(IntPtr.Zero))
                {
                    IntPtr hSrcDC = gsrc.GetHdc();
                    IntPtr hDC = gdest.GetHdc();
                    int retval = BitBlt(hDC, 0, 0, 1, 1, hSrcDC, location.X, location.Y, (int)CopyPixelOperation.SourceCopy);
                    gdest.ReleaseHdc();
                    gsrc.ReleaseHdc();
                }
            }

            return screenPixel.GetPixel(0, 0);
        }

        void GetColorOutOfAppTick(object sender, EventArgs e)
        {
            try
            {
                Point cursor = new Point();
                GetCursorPos(ref cursor);
                var newColor = GetColorAt(cursor);
                btnPickColor.BackColor = newColor;
                UpdateColourText(newColor);
            }
            finally { }
        }

        private void UpdateColourText(Color newColor)
        {
            try
            {
                string htmlColor = ColorTranslator.ToHtml(newColor);
                htmlColor = htmlColor.StartsWith("#") ? htmlColor : htmlColor.ToLower();
                if (!txtHexFullBlocked) txtHexFull.Text = htmlColor;
                if (!txtHexShortBlocked) txtHexShort.Text = htmlColor.StartsWith("#") ? htmlColor.Substring(1) : htmlColor;

                if (Properties.Settings.Default.UseFloat)
                {
                    string tmpR = (newColor.R / 255f).ToString("0.##f", CultureInfo.GetCultureInfo("en-us"));
                    string tmpG = (newColor.G / 255f).ToString("0.##f", CultureInfo.GetCultureInfo("en-us"));
                    string tmpB = (newColor.B / 255f).ToString("0.##f", CultureInfo.GetCultureInfo("en-us"));
                    txtRgbShort.Text = string.Format("{0}, {1}, {2}", tmpR, tmpG, tmpB);
                }
                else
                {
                    txtRgbShort.Text = string.Format("{0}, {1}, {2}", newColor.R, newColor.G, newColor.B);
                }
            }
            catch { }
        }

        private void btnAutoReadColour_Click(object sender, EventArgs e)
        {
            ToggleAutoRead();
        }

        private void ToggleAutoRead()
        {
            if (this.autoGetColour)   // Changing from true to false
            {   // User stopping auto get colours
                getColor.Stop();
                this.btnPickColor.BackColor = Color.Transparent;
            }
            else   // Changing from false to true;
            {   // User starting auto get colours
                getColor.Start();
            }

            System.GC.Collect();

            this.autoGetColour = !this.autoGetColour;   // Done here to ensure other methods do not run first
        }
    }
}
