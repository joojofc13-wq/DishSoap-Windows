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
        private readonly Button _file = new Button { Text = "Selecionar ZIP / XBLA", AutoSize = true };
        private readonly Button _folder = new Button { Text = "Selecionar pasta", AutoSize = true };
        private readonly Label _status = new Label { AutoSize = false, Width = 600, Height = 110 };
        private readonly ProgressBar _progress = new ProgressBar { Width = 600 };
        private readonly Button _close = new Button { Text = "Fechar", AutoSize = true };
        private bool _busy;
        public ImportWindow()
        {
            Text = "DishSoap Windows — Importar sua cópia";
            ClientSize = new Size(650, 300); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown, WrapContents = false };
            _status.Text = "Selecione sua cópia legítima de The Dishwasher: Dead Samurai.\n" +
                "O programa converte texturas, fontes e áudio e preserva os saves.\n" +
                "Esta instalação não inclui o código nem os recursos originais do jogo.";
            var buttons = new FlowLayoutPanel { Width = 600, Height = 40 };
            buttons.Controls.AddRange(new Control[] { _file, _folder, _close });
            layout.Controls.AddRange(new Control[] { _status, _progress, buttons }); Controls.Add(layout);
            _file.Click += async (_, _) => {
                using var picker = new OpenFileDialog { Filter = "ZIP ou pacote XBLA|*.zip;*.stfs;*.*", Title = "Selecione sua cópia legítima" };
                if (picker.ShowDialog(this) == DialogResult.OK) await ImportAsync(picker.FileName);
            };
            _folder.Click += async (_, _) => {
                using var picker = new FolderBrowserDialog { Description = "Raiz com data, gfx, Resources e sfx", UseDescriptionForTitle = true };
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
                _status.Text = "Recursos importados em:\n" + root + "\nVeja import-report.json para avisos de arquivos/áudio ausentes.\n" +
                    "Reabra Dishwasher.exe após compilar o jogo com seu GameSource.";
            }
            catch (Exception ex)
            {
                Log.Exception("importação", ex);
                _status.Text = ex.Message + "\nLog: " + Log.FilePath;
            }
            finally { _busy = false; _file.Enabled = _folder.Enabled = _close.Enabled = true; }
        }
    }
}
