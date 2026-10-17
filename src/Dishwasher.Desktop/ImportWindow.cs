using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dishwasher.Import;

namespace Dishwasher
{
    internal sealed class ImportWindow : Form
    {
        private readonly Button _file = new Button { Text = "Select ZIP / XBLA", AutoSize = true };
        private readonly Button _folder = new Button { Text = "Select folder", AutoSize = true };
        private readonly Label _status = new Label { AutoSize = false, Width = 600, Height = 110 };
        private readonly ProgressBar _progress = new ProgressBar { Width = 600 };
        private readonly Button _close = new Button { Text = "Close", AutoSize = true };
        private bool _busy;
        public ImportWindow()
        {
            Text = "DishSoap Windows: Import your copy";
            ClientSize = new Size(650, 300); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _status.Text = "Select your legitimate copy of The Dishwasher: Dead Samurai.\n" +
                "The program converts textures, fonts, and audio while preserving saves.\n" +
                "This package does not include original game code or assets.";
            var buttons = new FlowLayoutPanel { Width = 600, Height = 40 };
            buttons.Controls.AddRange(new Control[] { _file, _folder, _close });
            layout.Controls.AddRange(new Control[] { _status, _progress, buttons }); Controls.Add(layout);
            _file.Click += async (_, _) => {
                using var picker = new OpenFileDialog { Filter = "ZIP or XBLA package|*.zip;*.stfs;*.*", Title = "Select your legitimate copy" };
                if (picker.ShowDialog(this) == DialogResult.OK) await ImportAsync(picker.FileName);
            };
            _folder.Click += async (_, _) => {
                using var picker = new FolderBrowserDialog { Description = "Root folder containing data, gfx, Resources, and sfx", UseDescriptionForTitle = true };
                if (picker.ShowDialog(this) == DialogResult.OK) await ImportAsync(picker.SelectedPath);
            };
            _close.Click += (_, _) => Close();
            FormClosing += (_, e) => { if (_busy) e.Cancel = true; };
        }
        private async Task ImportAsync(string path)
        {
            _busy = true; _file.Enabled = _folder.Enabled = _close.Enabled = false;
            var progress = new Progress<ImportProgress>(p => {
                _status.Text = p.Phase + ": " + p.Path;
                _progress.Value = p.Total == 0 ? 0 : Math.Clamp((int)(100L * p.Done / p.Total), 0, 100);
            });
            try
            {
                string root = await Task.Run(() => DesktopImporter.Import(path, p => ((IProgress<ImportProgress>)progress).Report(p)));
                _progress.Value = 100;
                _status.Text = "Assets imported to:\n" + root + "\nSee import-report.json for missing file/audio warnings.\n" +
                    "Reopen Dishwasher.exe after building the game with your GameSource.";
            }
            catch (Exception ex)
            {
                Log.Exception("import", ex);
                _status.Text = ex.Message + "\nLog: " + Log.FilePath;
            }
            finally { _busy = false; _file.Enabled = _folder.Enabled = _close.Enabled = true; }
        }
    }
}
