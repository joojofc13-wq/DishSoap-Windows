using System;
using System.IO;
using System.Windows.Forms;
using Dishwasher.Import;

namespace Dishwasher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                string option(string name)
                {
                    int index = Array.IndexOf(args, name);
                    if (index < 0) return null;
                    if (index + 1 == args.Length || args[index + 1].StartsWith("--"))
                        throw new ArgumentException("Missing value for " + name);
                    return args[index + 1];
                }
                DesktopPaths.Initialize(option("--data-root"));
                Log.Initialize();
                AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Exception("unhandled",
                    e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject.ToString()));
                if (option("--ffmpeg") is string ffmpeg) NativeXma.FfmpegPath = ffmpeg;
                if (option("--launcher-check") is string preview)
                {
                    if (option("--ui-culture") is string culture)
                        DesktopLocale.PreviewLanguage = System.Globalization.CultureInfo.GetCultureInfo(culture).TwoLetterISOLanguageName;
                    Application.EnableVisualStyles();
                    using var launcher = new ConfigurationWindow();
                    if (!launcher.CheckControls()) throw new InvalidDataException("Incomplete settings editor.");
                    launcher.SavePreview(preview);
                    Console.WriteLine("Launcher controls/preview PASS"); return 0;
                }
                if (Array.IndexOf(args, "--configure") >= 0)
                {
                    Application.EnableVisualStyles(); Application.Run(new ConfigurationWindow()); return 0;
                }
                if (Array.IndexOf(args, "--settings-check") >= 0)
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(DesktopSettings.Load())); return 0;
                }
                if (Array.IndexOf(args, "--help") >= 0)
                {
                    Console.WriteLine("DishSoap Windows\n--import <ZIP/package/folder>  --data-root <folder>  --ffmpeg <exe>\n" +
                        "--verify-content  --diagnose  --graphics-check  --setup  --help\n" +
                        "Without GameSource, this executable only provides setup and diagnostics.");
                    return 0;
                }
                if (Array.IndexOf(args, "--diagnose") >= 0)
                {
#if DISHWASHER_GAME
                    Console.WriteLine("GameSource: included (running still requires assets and validation).");
#else
                    Console.WriteLine("GameSource: missing: this build DOES NOT run the game.");
#endif
                    Console.WriteLine("Assets: " + (DesktopImporter.TryGetActive(out var active) ? active : "not imported"));
                    Console.WriteLine("FFmpeg XMA2/Vorbis: " + NativeXma.Probe());
                    Console.WriteLine("Data: " + DesktopPaths.Root + "\nLog: " + Log.FilePath);
                    return 0;
                }
                if (Array.IndexOf(args, "--graphics-check") >= 0)
                {
                    GraphicsProbe.TextureAuditRoot = option("--hd-content-check");
                    GraphicsProbe.Benchmark = Array.IndexOf(args, "--render-benchmark") >= 0;
                    using var probe = new GraphicsProbe();
                    probe.Run();
                    return probe.Failed == 0 ? 0 : 4;
                }
                if (option("--import") is string source)
                {
                    DesktopImporter.Import(source, p => {
                        if (p.Done == 1 || p.Done % 25 == 0) Console.WriteLine(p.Phase + " " + p.Done + "/" + p.Total + " " + p.Path);
                    });
                    return 0;
                }
                if (Array.IndexOf(args, "--verify-content") >= 0)
                {
                    ContentVerification.Verify(Array.IndexOf(args, "--record-content") >= 0);
                    return 0;
                }
                if (Array.IndexOf(args, "--setup") >= 0 || !DesktopImporter.TryGetActive(out _))
                {
                    Application.EnableVisualStyles();
                    Application.Run(new ImportWindow());
                    return 0;
                }
#if DISHWASHER_GAME
                DesktopGame.SmokeSeconds = double.TryParse(option("--smoke-seconds"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double seconds) ? seconds : 0;
                DesktopGame.CaptureDirectory = option("--capture-dir");
                return DesktopBootstrap.Run();
#else
                Console.Error.WriteLine("Assets installed. This build only contains the setup tool. " +
                    "Build Dishwasher.exe with your real GameSource; see docs/WINDOWS_BUILD.md.");
                return 3;
#endif
            }
            catch (Exception ex)
            {
                Log.Exception("fatal", ex);
                Console.Error.WriteLine("Failure: " + ex.Message + "\nLog: " + Log.FilePath);
                return 1;
            }
        }
    }
}
