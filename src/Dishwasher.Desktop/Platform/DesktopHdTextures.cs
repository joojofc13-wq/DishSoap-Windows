using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dishwasher
{
    public static class DesktopHdTextures
    {
        public const float RenderScale = 1.5f;
        public const int RenderWidth = 1920, RenderHeight = 1080;
        private sealed class SizeInfo { public int Width, Height; public SizeInfo(int w, int h) { Width = w; Height = h; } }
        private sealed class FontInfo { public float Scale; public FontInfo(float s) { Scale = s; } }
        public sealed class Pack { public int Version { get; set; } public int TextureCount { get; set; } public List<Entry> Textures { get; set; } }
        public sealed class Entry
        {
            public string Asset { get; set; }
            public string File { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Scale { get; set; }
            public string Sha256 { get; set; }
        }
        private static readonly ConditionalWeakTable<Texture2D, SizeInfo> Sizes = new();
        private static readonly ConditionalWeakTable<SpriteFont, FontInfo> Fonts = new();
        private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);
        private static string _root;
        private static string _correctedRoot;
        public static bool Active => Entries.Count > 0;
        public static string PackRoot(string contentRoot) => File.Exists(Path.Combine(contentRoot, "hd2x-color-safe/manifest.json"))
            ? Path.Combine(contentRoot, "hd2x-color-safe") : Path.Combine(contentRoot, "hd2x");
        public static void Initialize(string contentRoot, string quality = "Enhanced")
        {
            Entries.Clear(); _root = PackRoot(contentRoot);
            _correctedRoot = Path.Combine(contentRoot, "corrected");
            if (quality == "Original") { Log.Info("[hd] Original textures selected."); return; }
            string manifest = Path.Combine(_root, "manifest.json");
            if (!File.Exists(manifest)) { Log.Info("[hd] No 2x pack; using original textures on the 1080p internal canvas."); return; }
            var pack = JsonSerializer.Deserialize<Pack>(File.ReadAllText(manifest));
            if (pack == null || pack.Version != 1 || pack.Textures == null || pack.TextureCount == 0 || pack.TextureCount != pack.Textures.Count)
                throw new InvalidDataException("Incomplete HD manifest.");
            foreach (var entry in pack.Textures)
            {
                if (entry.Scale != 2 || entry.Width <= 0 || entry.Height <= 0 || string.IsNullOrEmpty(entry.Asset) || entry.Sha256?.Length != 64)
                    throw new InvalidDataException("Invalid HD entry.");
                if (!File.Exists(DesktopPaths.Under(_root, entry.File))) throw new FileNotFoundException("Missing HD texture: " + entry.File);
                Entries.Add(entry.Asset, entry);
            }
            string scenesFile = Path.Combine(_root, "scenes.json");
            if (quality == "Enhanced" && File.Exists(scenesFile))
            {
                var scenes = JsonSerializer.Deserialize<Pack>(File.ReadAllText(scenesFile));
                if (scenes?.Version != 1 || scenes.Textures == null || scenes.TextureCount != scenes.Textures.Count) throw new InvalidDataException("Invalid scene manifest.");
                foreach (var entry in scenes.Textures)
                {
                    if (!Entries.TryGetValue(entry.Asset, out var original) || entry.Scale != 2 || entry.Width != original.Width || entry.Height != original.Height || entry.Sha256?.Length != 64 ||
                        !File.Exists(DesktopPaths.Under(_root, entry.File))) throw new InvalidDataException("Invalid HD scene: " + entry.Asset);
                    Entries[entry.Asset] = entry;
                }
                Log.Info("[hd] Additional restoration: " + scenes.TextureCount + " scenes.");
            }
            Log.Info("[hd] Active 2x pack: " + Entries.Count + " textures; 1920x1080 composition.");
        }
        public static bool TryLoad(GraphicsDevice device, string asset, out Texture2D texture)
        {
            texture = null;
            if (!Entries.TryGetValue(asset.Replace('\\', '/'), out var entry))
            {
                if (_correctedRoot == null) return false;
                string corrected = DesktopPaths.Under(_correctedRoot, asset + ".png");
                if (!File.Exists(corrected)) return false;
                using var original = File.OpenRead(corrected);
                texture = Texture2D.FromStream(device, original, DefaultColorProcessors.ZeroTransparentPixels);
                return true;
            }
            string file = DesktopPaths.Under(_root, entry.File);
            using var stream = File.OpenRead(file);
            string hash = Convert.ToHexString(SHA256.HashData(stream));
            if (!hash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Modified/incomplete HD texture: " + asset);
            stream.Position = 0;
            texture = Texture2D.FromStream(device, stream, DefaultColorProcessors.ZeroTransparentPixels);
            if (texture.Width != entry.Width * entry.Scale || texture.Height != entry.Height * entry.Scale)
            { texture.Dispose(); texture = null; throw new InvalidDataException("Invalid HD dimensions: " + asset); }
            Register(texture, entry.Width, entry.Height);
            return true;
        }
        public static void Register(Texture2D texture, int logicalWidth, int logicalHeight)
        {
            if (logicalWidth <= 0 || logicalHeight <= 0) throw new ArgumentOutOfRangeException(nameof(logicalWidth));
            Sizes.Remove(texture); Sizes.Add(texture, new SizeInfo(logicalWidth, logicalHeight));
        }
        public static int Width(Texture2D texture) => Sizes.TryGetValue(texture, out var info) ? info.Width : texture.Width;
        public static int Height(Texture2D texture) => Sizes.TryGetValue(texture, out var info) ? info.Height : texture.Height;
        public static Vector2 Scale(Texture2D texture) => new((float)texture.Width / Width(texture), (float)texture.Height / Height(texture));
        public static Rectangle? Source(Texture2D texture, Rectangle? source)
        {
            if (!source.HasValue) return null;
            Vector2 scale = Scale(texture); Rectangle r = source.Value;
            int left = (int)Math.Round(r.Left * scale.X), top = (int)Math.Round(r.Top * scale.Y);
            return new Rectangle(left, top, (int)Math.Round(r.Right * scale.X) - left, (int)Math.Round(r.Bottom * scale.Y) - top);
        }
        public static Vector2 TargetScale(GraphicsDevice device)
        {
            var targets = device.GetRenderTargets();
            return targets.Length == 1 && targets[0].RenderTarget is Texture2D texture ? Scale(texture) : Vector2.One;
        }
        public static RenderTarget2D CreateRenderTarget(GraphicsDevice device, int width, int height, bool mipMap,
            SurfaceFormat format, DepthFormat depth, int samples, RenderTargetUsage usage)
        {
            var target = new RenderTarget2D(device, (int)Math.Round(width * RenderScale), (int)Math.Round(height * RenderScale), mipMap, format, depth, samples, usage);
            Register(target, width, height); return target;
        }
        public static SpriteFont CreateFont(Texture2D atlas, List<Rectangle> glyphs, List<Rectangle> cropping,
            List<char> chars, int lineSpacing, float spacing, List<Vector3> kerning, char? defaultCharacter)
        {
            float scale = Scale(atlas).X;
            if (scale != Scale(atlas).Y) throw new InvalidDataException("Font atlas must use uniform scaling.");
            if (scale != 1)
            {
                for (int i = 0; i < glyphs.Count; i++) glyphs[i] = Source(atlas, glyphs[i]).Value;
                for (int i = 0; i < cropping.Count; i++)
                {
                    var r = cropping[i]; cropping[i] = new Rectangle((int)(r.X * scale), (int)(r.Y * scale), (int)(r.Width * scale), (int)(r.Height * scale));
                }
                for (int i = 0; i < kerning.Count; i++) kerning[i] *= scale;
            }
            var font = new SpriteFont(atlas, glyphs, cropping, chars, (int)(lineSpacing * scale), spacing * scale, kerning, defaultCharacter);
            Fonts.Add(font, new FontInfo(scale)); return font;
        }
        public static float FontScale(SpriteFont font) => Fonts.TryGetValue(font, out var info) ? info.Scale : 1;
        public static Vector2 MeasureString(SpriteFont font, string text) => font.MeasureString(text) / FontScale(font);
    }
}
