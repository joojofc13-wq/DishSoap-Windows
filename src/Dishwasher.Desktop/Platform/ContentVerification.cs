using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Dishwasher.Import;

namespace Dishwasher
{
    // Runs without a graphics device or audio playback. The derived-file
    // baseline detects later changes; it is not proof of original ownership.
    public static class ContentVerification
    {
        public static void Verify(bool record)
        {
            if (!DesktopImporter.TryGetActive(out string root)) throw new InvalidDataException("No complete imported content is active.");
            VerifyAt(root, Path.Combine(AppContext.BaseDirectory, "import-manifest.json"), AppContext.BaseDirectory, record);
        }
        internal static void VerifyAt(string root, string manifestPath, string installRoot, bool record)
        {
            string installationManifest = Path.Combine(installRoot, "installation-files.json");
            if (File.Exists(installationManifest))
            {
                var installed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(installationManifest));
                foreach (var entry in installed)
                {
                    string path = DesktopPaths.Under(installRoot, entry.Key);
                    if (!File.Exists(path)) throw new InvalidDataException("Missing installed file: " + entry.Key);
                    using var input = File.OpenRead(path);
                    if (!Convert.ToHexString(SHA256.HashData(input)).Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Damaged installed file: " + entry.Key);
                }
                Console.WriteLine("Installed executable files verified: " + installed.Count);
            }
            string baseline = Path.Combine(root, "content-integrity.json");
            var errors = new List<string>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Check(string relative, Action<string> validate)
            {
                try
                {
                    string path = DesktopPaths.Under(root, relative);
                    if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new InvalidDataException("missing or empty");
                    paths.Add(relative.Replace('\\', '/'));
                    validate?.Invoke(path);
                }
                catch (Exception ex) { errors.Add(relative + ": " + ex.Message); }
            }
            using (var stream = File.OpenRead(manifestPath))
            {
                foreach (var entry in ImportManifest.Load(stream).Files)
                {
                    if (entry.Path.StartsWith("sfx/", StringComparison.OrdinalIgnoreCase)) continue;
                    if (entry.Path.StartsWith("gfx/", StringComparison.OrdinalIgnoreCase) && entry.Path.EndsWith(".xnb", StringComparison.OrdinalIgnoreCase))
                    {
                        string stem = entry.Path[..^4];
                        if (File.Exists(DesktopPaths.Under(root, stem + ".spritefont.json")))
                        {
                            Check(stem + ".spritefont.json", p => { using var doc = JsonDocument.Parse(File.ReadAllText(p)); });
                            Check(stem + "_atlas.png", CheckPng);
                        }
                        else Check(stem + ".png", CheckPng);
                    }
                    else Check(entry.Path, p => {
                        using var input = File.OpenRead(p);
                        if (!Convert.ToHexString(SHA256.HashData(input)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("original content checksum mismatch");
                    });
                }
            }
            Check("sfx/cue_map.json", p => {
                using var map = JsonDocument.Parse(File.ReadAllText(p));
                var waves = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cues = map.RootElement.GetProperty("cues");
                foreach (string required in new[] { "solo_dish_fg", "solo_dish_bg" })
                    if (!cues.TryGetProperty(required, out var cue) || cue.GetProperty("files").GetArrayLength() == 0)
                        errors.Add("Missing guitar cue: " + required);
                foreach (var cue in cues.EnumerateObject())
                    foreach (var file in cue.Value.GetProperty("files").EnumerateArray())
                        waves.Add("sfx/" + file.GetProperty("file").GetString());
                int count = 0;
                foreach (string wave in waves)
                {
                    Check(wave, CheckAudio);
                    Console.WriteLine("Audio verified " + (++count) + "/" + waves.Count + " " + wave);
                }
            });
            string hd = Path.Combine(root, "hd2x-color-safe");
            if (Directory.Exists(hd))
            {
                foreach (string original in paths.Where(p => p.StartsWith("gfx/") && p.EndsWith(".png")).ToArray())
                    Check("hd2x-color-safe/" + original, CheckPng);
                foreach (string manifest in new[] { "manifest.json", "scenes.json" })
                    Check("hd2x-color-safe/" + manifest, p => {
                        using var doc = JsonDocument.Parse(File.ReadAllText(p));
                        foreach (var texture in doc.RootElement.GetProperty("Textures").EnumerateArray())
                        {
                            string relative = "hd2x-color-safe/" + texture.GetProperty("File").GetString();
                            Check(relative, png => {
                                CheckPng(png);
                                using var input = File.OpenRead(png);
                                if (!Convert.ToHexString(SHA256.HashData(input)).Equals(texture.GetProperty("Sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                                    throw new InvalidDataException("upscaled texture checksum mismatch");
                            });
                        }
                    });
            }
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (relative.StartsWith("fx/")) Check(relative, null);
            }
            if (File.Exists(baseline))
            {
                var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(baseline));
                foreach (var entry in saved)
                    Check(entry.Key, p => {
                        using var input = File.OpenRead(p);
                        if (!Convert.ToHexString(SHA256.HashData(input)).Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("derived content checksum mismatch");
                    });
            }
            if (errors.Count != 0) throw new InvalidDataException("Content verification failed:\n" + string.Join("\n", errors));
            if (record)
            {
                var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
                foreach (string relative in paths)
                {
                    using var input = File.OpenRead(DesktopPaths.Under(root, relative));
                    hashes.Add(relative, Convert.ToHexString(SHA256.HashData(input)));
                }
                DesktopPaths.AtomicWrite(baseline, JsonSerializer.Serialize(hashes));
            }
            Console.WriteLine("Content verification passed: " + paths.Count + " files. " +
                (File.Exists(baseline) ? "Derived-file checksums available." : "Legacy import: original checksums and derived-file format validation; no earlier derived-file baseline."));
        }
        private static void CheckPng(string path)
        {
            using var image = System.Drawing.Image.FromFile(path);
            if (image.Width < 1 || image.Height < 1) throw new InvalidDataException("invalid PNG dimensions");
        }
        private static void CheckAudio(string path)
        {
            using var stream = File.OpenRead(path);
            if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new NVorbis.VorbisReader(stream, false);
                var samples = new float[16384];
                long count = 0; int read;
                while ((read = reader.ReadSamples(samples, 0, samples.Length)) > 0)
                {
                    count += read;
                    for (int i = 0; i < read; i++) if (!float.IsFinite(samples[i])) throw new InvalidDataException("non-finite audio samples");
                }
                if (count == 0) throw new InvalidDataException("invalid Vorbis stream");
            }
            else
            {
                using var reader = new BinaryReader(stream);
                if (new string(reader.ReadChars(4)) != "RIFF" || reader.ReadUInt32() + 8L != stream.Length || new string(reader.ReadChars(4)) != "WAVE")
                    throw new InvalidDataException("invalid WAV header or length");
                bool format = false, data = false;
                while (stream.Position + 8 <= stream.Length)
                {
                    string type = new string(reader.ReadChars(4)); uint length = reader.ReadUInt32();
                    long end = stream.Position + length;
                    if (end > stream.Length) throw new InvalidDataException("truncated WAV chunk");
                    if (type == "fmt ")
                    {
                        if (length < 16 || reader.ReadUInt16() != 1 || reader.ReadUInt16() is < 1 or > 2 || reader.ReadUInt32() == 0)
                            throw new InvalidDataException("unsupported PCM format");
                        format = true;
                    }
                    if (type == "data" && length > 0) data = true;
                    stream.Position = end + (length & 1);
                }
                if (!format || !data) throw new InvalidDataException("WAV has no PCM data");
            }
        }
    }
}
