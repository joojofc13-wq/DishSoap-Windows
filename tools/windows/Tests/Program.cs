using System.Security.Cryptography;
using System.Xml.Serialization;
using Dishwasher;
using Dishwasher.Import;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

static void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    Console.WriteLine("PASS: " + label);
}
static void Reject(Action action, string label)
{
    try { action(); } catch (InvalidDataException) { Console.WriteLine("PASS: " + label); return; }
    throw new Exception("FAIL: " + label);
}
if (args.Length != 1) throw new ArgumentException("Provide a new test folder.");
string root = Path.GetFullPath(args[0]);
if (Directory.Exists(root)) throw new IOException("Preserve previous tests; provide a new folder.");
DesktopPaths.Initialize(root); Log.Initialize();
Reject(() => DesktopPaths.Under(root, "../escape"), "path traversal");
Reject(() => DesktopPaths.Under(root, "C:/escape"), "absolute path");
Reject(() => DesktopPaths.Under(root, "foo:bar"), "NTFS stream path");
string config = Path.Combine(DesktopPaths.Config, "atomic.txt");
DesktopPaths.AtomicWrite(config, "old"); DesktopPaths.AtomicWrite(config, "new");
Check(File.ReadAllText(config) == "new" && File.ReadAllText(config + ".bak") == "old", "atomic replace preserves backup");
var settings = new DesktopSettings { Width = 1920, Height = 1080, NativeResolution = true, VSync = false, Volume = .65f };
Check(settings.DiscordRichPresence, "Discord Rich Presence enabled by default");
settings.DiscordRichPresence = false;
settings.Save(); var loaded = DesktopSettings.Load();
Check(loaded.Width == 1280 && loaded.Height == 720 && !loaded.NativeResolution && !loaded.VSync && loaded.Volume == .65f,
    "old native/1080p settings migrate to 720p while preserving other choices");
Check(!loaded.DiscordRichPresence, "Discord Rich Presence opt-out persists");
var regionalCulture = System.Globalization.CultureInfo.CurrentCulture;
foreach (string culture in new[] { "pt-BR", "pt-PT", "es-MX", "en-US", "ja-JP" })
{
    System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
    Check(DesktopLocale.LanguageForRegion("BR", "pt") == "en" &&
        DesktopLocale.Text("legacy-pt", "English", "legacy-es") == "English", "English UI under " + culture);
}
System.Globalization.CultureInfo.CurrentCulture = regionalCulture;
byte[] authored = { 100,100,100,255, 130,100,100,255, 70,90,120,128, 0,0,0,255, 100,40,60,0 };
byte[] neural = { 160,120,110,255, 0,255,255,255, 255,0,0,255, 10,0,20,255, 255,255,255,255 };
DesktopTextureColors.Preserve(authored, neural);
Check(neural[0] == neural[2] && neural[4] - neural[6] == 30 && neural[8] - neural[10] == -50 &&
    neural[11] == 128 && neural[12] == 0 && neural[16] == 0 && neural[19] == 0,
    "neural luminance preserves authored bloom masks, shadows and alpha");
File.WriteAllText(Path.Combine(DesktopPaths.Config, "desktop.json"), "{\"ImageMode\":\"Uniform360p\"}");
var legacy = DesktopSettings.Load(); legacy.Save();
Check(!File.ReadAllText(Path.Combine(DesktopPaths.Config, "desktop.json")).Contains("ImageMode"), "legacy quality choice removed");
File.WriteAllText(Path.Combine(DesktopPaths.Config, "desktop.json"), "{broken");
Check(DesktopSettings.Load().Width == 1280 && Directory.GetFiles(DesktopPaths.Config, "*.corrupt-*").Length == 1,
    "corrupt config preserved");
var pad = DesktopInput.Merge(default, new KeyboardState(Keys.W, Keys.D, Keys.J), new DesktopSettings());
Check(pad.IsConnected && pad.IsButtonDown(Buttons.A) && pad.ThumbSticks.Left.X > .6f && pad.ThumbSticks.Left.Y > .6f,
    "keyboard creates connected pad, diagonal normalized");
var altEnter = DesktopInput.Merge(default, new KeyboardState(Keys.LeftAlt, Keys.Enter), new DesktopSettings());
Check(!altEnter.IsButtonDown(Buttons.A) && !altEnter.IsButtonDown(Buttons.Start), "Alt+Enter does not activate menu");
Check(!pad.IsButtonDown(Buttons.DPadUp), "movement does not trigger weapon selector D-pad");
var escape = DesktopInput.Merge(default, new KeyboardState(Keys.Escape), new DesktopSettings(), false);
Check(escape.IsButtonDown(Buttons.Start) && !escape.IsButtonDown(Buttons.B), "gameplay Escape pauses without grab");
Check(DesktopPresentation.Fit(1920, 1080) == new Rectangle(0, 0, 1920, 1080) &&
    DesktopPresentation.Fit(2560, 1440) == new Rectangle(0, 0, 2560, 1440) &&
    DesktopPresentation.Fit(1024, 768) == new Rectangle(0, 96, 1024, 576), "720p canvas scales and letterboxes");
string pcm = Path.Combine(DesktopPaths.Cache, "test.wav");
Check(NativeXma.Pcm16BeToWav(new byte[] { 0x12, 0x34, 0xfe, 0xdc }, 1, 8000, pcm) == 0, "PCM BE conversion");
byte[] wav = File.ReadAllBytes(pcm);
Check(wav[44] == 0x34 && wav[45] == 0x12 && wav[46] == 0xdc && wav[47] == 0xfe, "PCM sample endian");
byte[] xma = NativeXma.WrapXma2(new byte[2048], 2, 7995, 8704);
Check(BitConverter.ToInt32(xma, 4) == xma.Length - 8 && BitConverter.ToUInt16(xma, 20) == 0x166 &&
    BitConverter.ToUInt16(xma, 36) == 34 && BitConverter.ToInt32(xma, 44) == 8704,
    "XMA2 RIFF lengths and sample metadata");
var texture = new XnbTexture { Format = 1, Width = 1, Height = 1 };
texture.Mips.Add(new XnbMip { Width = 1, Height = 1, Data = new byte[] { 128, 200, 100, 50 } });
byte[] rgba = XnbFile.DecodeTexture(texture, 0, true, true, false, true);
Check(rgba.SequenceEqual(new byte[] { 200, 100, 50, 128 }), "ARGB big-endian and straight alpha");
var bc1 = new XnbTexture { Format = 28, Width = 4, Height = 4 };
bc1.Mips.Add(new XnbMip { Width = 4, Height = 4, Data = new byte[] { 0xf8,0,7,0xe0,0x55,0,0xff,0xaa } });
var bc1Pixels = XnbFile.DecodeTexture(bc1, 0, true, true, false, true, swapColorIndexWords: true);
Check(bc1Pixels[0] == 255 && bc1Pixels[1] == 0 && bc1Pixels[16] == 0 && bc1Pixels[17] == 255 && bc1Pixels[32] == 170 && bc1Pixels[48] == 85,
    "Xbox BC1 index words preserve row order and diagonal continuity");
var bc3 = new XnbTexture { Format = 32, Width = 4, Height = 4 };
bc3.Mips.Add(new XnbMip { Width = 4, Height = 4, Data = new byte[] { 0,255,0,0,0,0,0,0, 0,0,255,255,255,255,255,255 } });
var bc3Pixels = XnbFile.DecodeTexture(bc3, 0, true, true, false, true, swapColorIndexWords: true);
Check(bc3Pixels[0] == 170 && bc3Pixels[1] == 170 && bc3Pixels[3] == 255,
    "BC3 retains four-colour palette regardless of endpoint ordering");
foreach (int fps in new[] { 30, 60, 120, 144, 180, 240 })
{
    var clock = new FixedSimulationClock(); double distance = 0;
    for (int frame = 0; frame < fps * 10; frame++) clock.Advance(1.0 / fps, () => distance += 300 * FixedSimulationClock.StepSeconds);
    Check(clock.Ticks == 600 && Math.Abs(distance - 3000) < 0.001, "10 seconds of movement remain identical at " + fps + " render FPS");
}
var pausedClock = new FixedSimulationClock(); pausedClock.Advance(10, () => { });
Check(pausedClock.Ticks == 15, "long stalls bound simulation catch-up work");
var simulationRandom = new Random(37); var expectedRandom = new Random(37);
DesktopRenderContext.Begin(1); for (int i = 0; i < 100; i++) DesktopRenderContext.RandomFor(simulationRandom).Next(); DesktopRenderContext.End();
Check(simulationRandom.Next() == expectedRandom.Next(), "extra render frames do not consume simulation randomness");
using (var empty = new DirectoryContentSource(root))
using (var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "import-manifest.json")))
{
    var manifest = ImportManifest.Load(stream);
    var report = ContentImporter.Validate(empty, manifest, null);
    Check(report.Total == 458 && report.NothingUsable && report.Missing == 458, "missing originals rejected");
}
var xml = new XmlSerializer(typeof(SyntheticSave));
string savePath = Path.Combine(DesktopPaths.Saves, "synthetic.sav");
AtomicSave.Write(savePath, xml, new SyntheticSave { Progress = 1 });
AtomicSave.Write(savePath, xml, new SyntheticSave { Progress = 2 });
using (var latest = File.OpenRead(savePath)) using (var backup = File.OpenRead(savePath + ".bak"))
    Check(((SyntheticSave)xml.Deserialize(latest)).Progress == 2 && ((SyntheticSave)xml.Deserialize(backup)).Progress == 1,
        "atomic XML save preserves previous generation");
File.WriteAllText(savePath, "broken XML");
AtomicSave.Recover(savePath, xml);
using (var recovered = File.OpenRead(savePath))
    Check(((SyntheticSave)xml.Deserialize(recovered)).Progress == 1 &&
        Directory.GetFiles(DesktopPaths.Saves, "*.corrupt-*").Length == 1, "corrupt save restored and preserved");
await DiscordPresenceChecks.Run();
Console.WriteLine("All platform checks passed. These are not campaign tests.");

public sealed class SyntheticSave { public int Progress { get; set; } }
