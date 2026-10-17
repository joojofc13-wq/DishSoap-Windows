using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dishwasher.Import;

namespace Dishwasher
{
    public static class DesktopImporter
    {
        // Derived outputs required before switching the active content pointer.
        public static readonly string[] BootOutputs = {
            "data/levels.zdx", "gfx/text.png", "gfx/Arials_atlas.png",
            "gfx/Arials.spritefont.json", "Resources/dishX.Resources.Strings.resources"
        };
        public static bool TryGetActive(out string root)
        {
            root = null;
            if (!File.Exists(DesktopPaths.ActiveFile)) return false;
            string id = File.ReadAllText(DesktopPaths.ActiveFile).Trim();
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("Instalação ativa inválida.");
            string candidate = DesktopPaths.Under(DesktopPaths.Imports, id);
            if (!File.Exists(Path.Combine(candidate, "import-report.json")) ||
                BootOutputs.Any(p => !File.Exists(DesktopPaths.Under(candidate, p)))) return false;
            root = candidate;
            return true;
        }
        public static string Import(string sourcePath, Action<ImportProgress> progress = null)
        {
            using var manifestStream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "import-manifest.json"));
            ImportManifest manifest = ImportManifest.Load(manifestStream);
            foreach (var file in manifest.Files) _ = DesktopPaths.Under(DesktopPaths.Imports, file.Path);
            string id = Guid.NewGuid().ToString("N");
            string stage = DesktopPaths.Under(DesktopPaths.Imports, id);
            string temp = DesktopPaths.Under(DesktopPaths.Cache, id);
            Directory.CreateDirectory(stage); Directory.CreateDirectory(temp);
            // Failed stages are intentionally preserved with diagnostics; they
            // never become active and cannot modify the previous installation.
            using IGameContentSource source = Directory.Exists(sourcePath)
                ? new DirectoryContentSource(sourcePath)
                : ContentSourceFactory.FromLocalFile(sourcePath, temp);
            ImportReport report = ContentImporter.Validate(source, manifest, progress);
            Log.Info("[import] " + report.Summary());
            foreach (var missing in report.MissingFiles) Log.Info("[import] ausente: " + missing);
            foreach (var differed in report.DifferedFiles) Log.Info("[import] diferente: " + differed);
            if (report.NothingUsable)
                throw new InvalidDataException("Arquivos indispensáveis ausentes: " +
                    string.Join(", ", report.MissingFiles.Where(ContentImporter.IsCore)) +
                    ". Selecione a raiz que contém data, gfx, Resources e sfx.");
            var audio = ContentImporter.StageAndDerive(source, manifest, stage, progress);
            DesktopPaths.AtomicWrite(Path.Combine(stage, "import-report.json"), JsonSerializer.Serialize(new {
                createdUtc = DateTime.UtcNow, matched = report.Matched, differed = report.Differed,
                missing = report.MissingFiles, notes = report.Notes, staged = audio.Staged,
                failed = audio.StageFailed, decodedWaves = audio.WavesDecoded,
                failedWaves = audio.WavesFailed, audioNotes = audio.Notes,
                decoder = NativeXma.FfmpegPath, textureDecoder = "xbox-dxt-8in16-v2"
            }, new JsonSerializerOptions { WriteIndented = true }));
            var absent = BootOutputs.Where(p => !File.Exists(DesktopPaths.Under(stage, p))).ToArray();
            if (absent.Length != 0)
                throw new InvalidDataException("Falha ao converter recursos indispensáveis: " + string.Join(", ", absent));
            foreach (string effect in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "fx"), "*.xnb"))
            {
                Directory.CreateDirectory(Path.Combine(stage, "fx"));
                File.Copy(effect, Path.Combine(stage, "fx", Path.GetFileName(effect)), false);
            }
            DesktopPaths.AtomicWrite(DesktopPaths.ActiveFile, id);
            Log.Info("[import] Instalação ativada: " + stage);
            if (audio.WavesFailed != 0 || audio.StageFailed != 0 || !string.IsNullOrEmpty(audio.Notes))
                Log.Error("[import] Instalação parcial; veja import-report.json. " + audio.Notes);
            return stage;
        }
    }
}
