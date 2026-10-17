using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Dishwasher
{
    internal sealed class ConfigurationWindow : Form
    {
        private readonly DesktopSettings _settings = DesktopSettings.Load();
        private readonly ComboBox _fps = new(), _quality = new(), _language=new();
        private readonly CheckBox _fullscreen = new(), _vsync = new(), _showFps = new(), _smooth = new(), _discord = new(), _localCoop=new();
        private readonly CheckBox _vibration = new();
        private readonly Button _save = new(), _cancel = new(), _openSaves = new();
        private readonly TabControl _tabs = new();
        private readonly TabPage _gameTab = new("Game"), _cheatsTab = new("Cheats");
        private readonly TabPage _qolTab = new("Quality of life");
        private readonly QolConfigurationPanel _qol;
        private readonly CheatConfigurationPanel _cheats = new();
        private bool _previewing;
        protected override bool ShowWithoutActivation => _previewing;

        public ConfigurationWindow()
        {
            Text = "dishwasher"; ClientSize = new Size(500, 604);
            _qol = new QolConfigurationPanel(_settings);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9);
            AddLabel("Image:", 20, 24);
            Setup(_quality, 178, 20); _quality.Items.AddRange(new object[] { "Original", "Upscaled" });
            _quality.SelectedIndex = _settings.TextureQuality == "Original" ? 0 : 1;
            AddLabel("Recommended: keep Upscaled enabled.", 20, 54);
            AddLabel("FPS limit:", 20, 83);
            Setup(_fps, 178, 79);
            foreach (int fps in new[] { 60, 90, 120, 144, 165, 180, 240, 360 }) _fps.Items.Add(fps);
            if (!_fps.Items.Contains(_settings.FrameLimit)) _fps.Items.Add(_settings.FrameLimit);
            _fps.SelectedItem = _settings.FrameLimit;
            AddLabel("Resolution:", 20, 122);
            AddLabel("720p (1280 × 720)", 178, 122);
            AddLabel("Game language:",20,160); Setup(_language,178,156);
            _language.Items.AddRange(DesktopGameLanguage.Choices);
            foreach(DesktopGameLanguage.Choice choice in _language.Items)
                if(choice.Code==_settings.GameLanguage) _language.SelectedItem=choice;
            if(_language.SelectedIndex<0) _language.SelectedIndex=1;
            Place(_fullscreen, "Fullscreen", 20, 206, _settings.Fullscreen);
            Place(_vsync, "VSync", 240, 206, _settings.VSync);
            Place(_showFps, "Show FPS", 20, 239, _settings.ShowFps);
            Place(_smooth, "Smooth motion", 240, 239, _settings.SmoothMotion);
            Place(_discord, "Discord Rich Presence", 20, 272, _settings.DiscordRichPresence);
            _discord.Width = 300;
            Place(_localCoop,"Local co-op (additional controllers)",20,305,_settings.LocalCoop);
            _localCoop.Width=400;
            Place(_vibration,"Controller vibration",20,338,_settings.ControllerVibration);
            _openSaves.Text = "Open saves folder"; _openSaves.SetBounds(20, 389, 178, 34);
            _openSaves.Click += (_, _) => {
                try
                {
                    Directory.CreateDirectory(DesktopPaths.Saves);
                    Process.Start(new ProcessStartInfo(DesktopPaths.Saves) { UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "dishwasher", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            Controls.Add(_openSaves);
            while (Controls.Count > 0) _gameTab.Controls.Add(Controls[0]);
            _tabs.SetBounds(12, 12, 476, 532); _tabs.TabPages.AddRange(new[] { _gameTab, _qolTab, _cheatsTab });
            _qolTab.Controls.Add(_qol);
            _cheatsTab.Controls.Add(_cheats); Controls.Add(_tabs);
            _save.Text = "Save settings"; _save.SetBounds(278, 558, 210, 34);
            _cancel.Text = "Cancel"; _cancel.SetBounds(166, 558, 100, 34); _cancel.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[] { _save, _cancel }); AcceptButton = _save; CancelButton = _cancel;
            _save.Click += (_, _) => {
                try
                {
                    SaveChoices();
                    string message = "Settings saved. Cheats apply in-game within one second.\nRestart Dishwasher.exe for other settings.";
                    if (_cheats.LastBackup != null) message += "\n\nSave backup: " + _cheats.LastBackup;
                    MessageBox.Show(this, message, "dishwasher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "dishwasher", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
        }
        private void AddLabel(string text, int x, int y) => Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y) });
        private void Setup(ComboBox box, int x, int y) { box.SetBounds(x, y, 252, 26); box.DropDownStyle = ComboBoxStyle.DropDownList; Controls.Add(box); }
        private void Place(CheckBox box, string text, int x, int y, bool selected)
        { box.Text = text; box.SetBounds(x, y, 210, 27); box.Checked = selected; Controls.Add(box); }
        internal void SaveChoices()
        {
            _cheats.SaveChoices();
            _settings.NativeResolution = false; _settings.Width = 1280; _settings.Height = 720;
            _settings.FrameLimit = (int)_fps.SelectedItem; _settings.Fullscreen = _fullscreen.Checked;
            _settings.VSync = _vsync.Checked; _settings.ShowFps = _showFps.Checked; _settings.SmoothMotion = _smooth.Checked;
            _settings.DiscordRichPresence = _discord.Checked;
            _settings.LocalCoop=_localCoop.Checked;
            _settings.ControllerVibration=_vibration.Checked;
            _settings.TextureQuality = _quality.SelectedIndex == 0 ? "Original" : "Enhanced";
            _settings.GameLanguage=((DesktopGameLanguage.Choice)_language.SelectedItem).Code;
            _qol.SaveChoices(_settings);
            _settings.Save();
        }
        internal bool CheckControls() => _fps.Items.Contains(180) && _quality.Items.Count == 2 &&
            _quality.Items[0].ToString() == "Original" && _quality.Items[1].ToString() == "Upscaled" &&
            Text == "dishwasher" && _save.Text == "Save settings" && _openSaves.Text == "Open saves folder" &&
            _gameTab.Controls.Contains(_discord) && _gameTab.Controls.Contains(_localCoop) && _discord.Text == "Discord Rich Presence" && _language.Items.Count==DesktopGameLanguage.Choices.Length && _language.SelectedIndex>=0 &&
            _tabs.TabPages.Count == 3 && _qolTab.Text == "Quality of life" && _qolTab.Controls.Contains(_qol) && _cheatsTab.Text == "Cheats" && _cheats.Values != null &&
            _gameTab.Controls.Contains(_vibration) && _vibration.Text == "Controller vibration";
        internal void SavePreview(string path)
        {
            _previewing = true; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            Location = new Point(-10000, -10000); Opacity = 0; Show(); Application.DoEvents();
            using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path);
            _tabs.SelectedTab = _cheatsTab; Application.DoEvents();
            using var cheatsBitmap = new Bitmap(Width, Height); DrawToBitmap(cheatsBitmap, new Rectangle(0, 0, Width, Height));
            cheatsBitmap.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFileNameWithoutExtension(path) + "-cheats.png"));
            _tabs.SelectedTab = _qolTab; Application.DoEvents();
            using var qolBitmap = new Bitmap(Width, Height); DrawToBitmap(qolBitmap, new Rectangle(0, 0, Width, Height));
            qolBitmap.Save(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFileNameWithoutExtension(path) + "-qol.png"));
            Hide(); _previewing = false;
        }
    }
}
