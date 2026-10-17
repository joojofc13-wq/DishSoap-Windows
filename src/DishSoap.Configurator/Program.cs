using System;
using System.IO;
using System.Globalization;
using System.Windows.Forms;

namespace Dishwasher
{
    internal static class ConfigurationProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                string Option(string name)
                {
                    int i = Array.IndexOf(args, name);
                    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
                }
                DesktopPaths.Initialize(Option("--data-root"));
                Application.EnableVisualStyles();
                if (Option("--check") is string preview)
                {
                    if (Option("--data-root") == null) throw new ArgumentException("Use an isolated --data-root for diagnostic writes.");
                    if (Option("--ui-culture") is string culture) DesktopLocale.PreviewLanguage = CultureInfo.GetCultureInfo(culture).TwoLetterISOLanguageName;
                    using var test = new ConfigurationWindow();
                    if (!test.CheckControls()) throw new InvalidDataException("Configurator controls failed.");
                    test.SavePreview(preview);
                    test.SaveChoices();
                    if (!File.Exists(Path.Combine(DesktopPaths.Config, "desktop.json"))) throw new InvalidDataException("Settings were not saved.");
                    return 0;
                }
                Application.Run(new ConfigurationWindow()); return 0;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "DishSoap", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }
}
