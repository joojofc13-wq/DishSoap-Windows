using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Dishwasher
{
    internal sealed class ConfigurationWindow : Form
    {
        private readonly DesktopSettings _settings = DesktopSettings.Load();
        private readonly ComboBox _fps = new(), _quality = new();
        private readonly CheckBox _fullscreen = new(), _vsync = new(), _showFps = new(), _smooth = new();
        private readonly Button _save = new(), _cancel = new();
        private bool _previewing;
        protected override bool ShowWithoutActivation => _previewing;
        private static string Tr(string pt, string en, string es) => DesktopLocale.Text(pt, en, es);
        public ConfigurationWindow()
        {
            Text = "dishwasher"; ClientSize = new Size(450, 337);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9);
            Controls.Add(new Label { Text = "DishSoap", Font = new Font(Font.FontFamily, 13, FontStyle.Bold), AutoSize = true, Location = new Point(20, 17) });
            AddLabel(Tr("Imagem:", "Image:", "Imagen:"), 20, 66);
            Setup(_quality, 178, 62); _quality.Items.AddRange(new object[] { "Original", "Upscaled" });
            _quality.SelectedIndex = _settings.TextureQuality == "Original" ? 0 : 1;
            AddLabel(Tr("Limite de FPS:", "FPS limit:", "Limite de FPS:"), 20, 105);
            Setup(_fps, 178, 101);
            foreach (int fps in new[] { 60, 90, 120, 144, 165, 180, 240, 360 }) _fps.Items.Add(fps);
            if (!_fps.Items.Contains(_settings.FrameLimit)) _fps.Items.Add(_settings.FrameLimit);
            _fps.SelectedItem = _settings.FrameLimit;
            AddLabel(Tr("Resolução:", "Resolution:", "Resolucion:"), 20, 144);
            AddLabel("720p (1280 × 720)", 178, 144);
            Place(_fullscreen, Tr("Tela cheia", "Fullscreen", "Pantalla completa"), 20, 184, _settings.Fullscreen);
            Place(_vsync, "VSync", 240, 184, _settings.VSync);
            Place(_showFps, Tr("Mostrar FPS", "Show FPS", "Mostrar FPS"), 20, 217, _settings.ShowFps);
            Place(_smooth, Tr("Suavizar movimento", "Smooth motion", "Suavizar movimiento"), 240, 217, _settings.SmoothMotion);
            _save.Text = Tr("Salvar configuração", "Save settings", "Guardar configuracion"); _save.SetBounds(231, 281, 199, 34);
            _cancel.Text = Tr("Cancelar", "Cancel", "Cancelar"); _cancel.SetBounds(119, 281, 100, 34); _cancel.DialogResult = DialogResult.Cancel;
            Controls.AddRange(new Control[] { _save, _cancel }); AcceptButton = _save; CancelButton = _cancel;
            _save.Click += (_, _) => {
                try
                {
                    SaveChoices();
                    MessageBox.Show(this, Tr("Configuração salva. Abra Dishwasher.exe para jogar.", "Settings saved. Open Dishwasher.exe to play.", "Configuracion guardada. Abre Dishwasher.exe para jugar."), "DishSoap", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "DishSoap", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
        }
        private void AddLabel(string text, int x, int y) => Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(x, y) });
        private void Setup(ComboBox box, int x, int y) { box.SetBounds(x, y, 252, 26); box.DropDownStyle = ComboBoxStyle.DropDownList; Controls.Add(box); }
        private void Place(CheckBox box, string text, int x, int y, bool selected)
        { box.Text = text; box.SetBounds(x, y, 210, 27); box.Checked = selected; Controls.Add(box); }
        internal void SaveChoices()
        {
            _settings.NativeResolution = false; _settings.Width = 1280; _settings.Height = 720;
            _settings.FrameLimit = (int)_fps.SelectedItem; _settings.Fullscreen = _fullscreen.Checked;
            _settings.VSync = _vsync.Checked; _settings.ShowFps = _showFps.Checked; _settings.SmoothMotion = _smooth.Checked;
            _settings.TextureQuality = _quality.SelectedIndex == 0 ? "Original" : "Enhanced";
            _settings.Save();
        }
        internal bool CheckControls() => _fps.Items.Contains(180) && _quality.Items.Count == 2 &&
            _quality.Items[0].ToString() == "Original" && _quality.Items[1].ToString() == "Upscaled" &&
            Text == "dishwasher" && _save.Text == Tr("Salvar configuração", "Save settings", "Guardar configuracion");
        internal void SavePreview(string path)
        {
            _previewing = true; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            Location = new Point(-10000, -10000); Opacity = 0; Show(); Application.DoEvents();
            using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path);
            Hide(); _previewing = false;
        }
    }
}
