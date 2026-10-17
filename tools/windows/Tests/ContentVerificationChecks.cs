using System.Security.Cryptography;
using System.Text.Json;
using Dishwasher;

internal static class ContentVerificationChecks
{
    public static void Run(string root)
    {
        string content = Path.Combine(root, "verification");
        string install = Path.Combine(root, "installation");
        Directory.CreateDirectory(Path.Combine(content, "gfx"));
        Directory.CreateDirectory(Path.Combine(content, "sfx"));
        Directory.CreateDirectory(install);
        string png = Path.Combine(content, "gfx/test.png");
        using (var bitmap = new System.Drawing.Bitmap(2, 2)) bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        byte[] original = { 1, 2, 3 };
        File.WriteAllBytes(Path.Combine(content, "data.bin"), original);
        string manifest = Path.Combine(install, "import-manifest.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new { files = new[] {
            new { path="data.bin", sha256=Convert.ToHexString(SHA256.HashData(original)), size=3 },
            new { path="gfx/test.xnb", sha256="unused", size=1 }
        }}));
        string wave = Path.Combine(content, "sfx/test.wav");
        using (var writer = new BinaryWriter(File.Create(wave)))
        {
            writer.Write("RIFF"u8); writer.Write(38); writer.Write("WAVEfmt "u8); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(22050); writer.Write(44100);
            writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(2); writer.Write((short)100);
        }
        var cue = new { files = new[] { new { file = "test.wav" } } };
        File.WriteAllText(Path.Combine(content, "sfx/cue_map.json"), JsonSerializer.Serialize(new { cues = new { solo_dish_fg = cue, solo_dish_bg = cue } }));
        ContentVerification.VerifyAt(content, manifest, install, true);
        byte[] cleanWave = File.ReadAllBytes(wave);
        byte[] changedWave = (byte[])cleanWave.Clone(); changedWave[^1] ^= 1;
        File.WriteAllBytes(wave, changedWave);
        Reject(() => ContentVerification.VerifyAt(content, manifest, install, false), "checksum detects a changed but still valid WAV");
        File.WriteAllBytes(wave, cleanWave);
        File.Move(png, png + ".saved");
        Reject(() => ContentVerification.VerifyAt(content, manifest, install, false), "missing derived texture");
        File.Move(png + ".saved", png);
        File.WriteAllBytes(Path.Combine(content, "data.bin"), new byte[] { 3, 2, 1 });
        Reject(() => ContentVerification.VerifyAt(content, manifest, install, false), "original content checksum");
        File.WriteAllBytes(Path.Combine(content, "data.bin"), original);
        File.WriteAllText(Path.Combine(install, "installation-files.json"), JsonSerializer.Serialize(new Dictionary<string, string> { ["runtime.dll"] = Convert.ToHexString(SHA256.HashData(original)) }));
        File.WriteAllBytes(Path.Combine(install, "runtime.dll"), new byte[] { 0 });
        Reject(() => ContentVerification.VerifyAt(content, manifest, install, false), "damaged installed executable file");
        File.WriteAllBytes(Path.Combine(install, "runtime.dll"), original);
        ContentVerification.VerifyAt(content, manifest, install, false);
        Console.WriteLine("PASS content and installation integrity regression checks");
    }
    private static void Reject(Action action, string name)
    {
        try { action(); } catch (InvalidDataException) { Console.WriteLine("PASS " + name); return; }
        throw new Exception("FAIL " + name);
    }
}
