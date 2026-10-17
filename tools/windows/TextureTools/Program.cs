using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

static Bitmap Read(string path)
{
    using var source = new Bitmap(path);
    // DrawImage would round-trip through premultiplied alpha and perturb
    // straight RGB at translucent edges. Clone preserves the decoded channels.
    return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
}
static byte[] Pixels(Bitmap bitmap)
{
    var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
    var bytes = new byte[bitmap.Width * bitmap.Height * 4];
    for (int y = 0; y < bitmap.Height; y++) Marshal.Copy(data.Scan0 + y * data.Stride, bytes, y * bitmap.Width * 4, bitmap.Width * 4);
    bitmap.UnlockBits(data); return bytes;
}
static void Save(byte[] bytes, int w, int h, string path)
{
    using var result = new Bitmap(w, h, PixelFormat.Format32bppArgb);
    var data = result.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
    for (int y = 0; y < h; y++) Marshal.Copy(bytes, y * w * 4, data.Scan0 + y * data.Stride, w * 4);
    result.UnlockBits(data);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    result.Save(path, ImageFormat.Png);
}
static Bitmap Resize(Bitmap source, int scale)
{
    var result = new Bitmap(source.Width * scale, source.Height * scale, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(result);
    g.CompositingMode = CompositingMode.SourceCopy;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    using var attributes = new ImageAttributes();
    attributes.SetWrapMode(WrapMode.TileFlipXY);
    g.DrawImage(source, new Rectangle(0, 0, result.Width, result.Height), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
    return result;
}
try
{
    if (args.Length < 1) throw new ArgumentException("prepare IN OUT | finish ORIGINAL AI OUT | resize IN OUT | compare ORIGINAL HD OUT x y w h");
    if (args[0] == "correct-tree")
    {
        int count = 0;
        foreach (string file in Directory.EnumerateFiles(Path.Combine(args[1], "gfx"), "*.xnb", SearchOption.AllDirectories))
        {
            var xnb = new Dishwasher.Import.XnbFile(File.ReadAllBytes(file), file);
            var texture = xnb.Primary as Dishwasher.Import.XnbTexture;
            bool font = xnb.Primary is Dishwasher.Import.XnbSpriteFont;
            if (font) texture = ((Dishwasher.Import.XnbSpriteFont)xnb.Primary).Texture;
            if (texture == null) continue;
            string relative = Path.ChangeExtension(Path.GetRelativePath(args[1], file), null) + (font ? "_atlas.png" : ".png");
            string output = Path.Combine(args[2], relative); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var data = Dishwasher.Import.XnbFile.DecodeTexture(texture, 0, true, true, false, true, swapColorIndexWords: true);
            Dishwasher.Import.PngWriter.WriteFile(output, texture.Width, texture.Height, data); count++;
        }
        Console.WriteLine($"Correctly decoded {count} owner textures; original files untouched.");
    }
    else if (args[0] == "decode")
    {
        var file = new Dishwasher.Import.XnbFile(File.ReadAllBytes(args[1]), args[1]);
        var texture = (Dishwasher.Import.XnbTexture)file.Primary;
        byte[] data = Dishwasher.Import.XnbFile.DecodeTexture(texture, 0, true, true, false, true, swapColorIndexWords: true);
        Dishwasher.Import.PngWriter.WriteFile(args[2], texture.Width, texture.Height, data);
        Console.WriteLine($"Decoded format={texture.Format}, {texture.Width}x{texture.Height} with Xbox 8-in-16 colour-index order.");
    }
    else if (args[0] == "prepare")
    {
        using var source = Read(args[1]); var bytes = Pixels(source);
        var valid = new bool[source.Width * source.Height];
        for (int i = 0; i < valid.Length; i++) valid[i] = bytes[i * 4 + 3] > 8;
        // Extend edge colours into transparent padding. The AI sees an opaque
        // colour image; the original alpha is restored independently afterwards.
        for (int pass = 0; pass < 12; pass++)
        {
            var previous = (bool[])valid.Clone();
            for (int y = 0; y < source.Height; y++) for (int x = 0; x < source.Width; x++)
            {
                int i = y * source.Width + x; if (previous[i]) continue;
                int neighbour = -1;
                if (x > 0 && previous[i - 1]) neighbour = i - 1;
                else if (x + 1 < source.Width && previous[i + 1]) neighbour = i + 1;
                else if (y > 0 && previous[i - source.Width]) neighbour = i - source.Width;
                else if (y + 1 < source.Height && previous[i + source.Width]) neighbour = i + source.Width;
                if (neighbour < 0) continue;
                for (int c = 0; c < 3; c++) bytes[i * 4 + c] = bytes[neighbour * 4 + c];
                valid[i] = true;
            }
        }
        for (int i = 0; i < valid.Length; i++) bytes[i * 4 + 3] = 255;
        Save(bytes, source.Width, source.Height, args[2]);
    }
    else if (args[0] == "recolor-pack")
    {
        if (Path.GetFullPath(args[2]) == Path.GetFullPath(args[3])) throw new InvalidDataException("Preserve the old pack; use a separate destination.");
        int count = 0;
        foreach (string manifestName in new[] { "manifest.json", "scenes.json" })
        {
            string manifestFile = Path.Combine(args[2], manifestName);
            if (!File.Exists(manifestFile)) continue;
            var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestFile))!;
            foreach (var entry in manifest["Textures"]!.AsArray())
            {
                string asset = entry!["Asset"]!.GetValue<string>(), relative = entry["File"]!.GetValue<string>();
                using var original = Read(Path.Combine(args[1], asset + ".png"));
                using var baseline = Resize(original, 2); using var old = Read(Path.Combine(args[2], relative));
                if (old.Size != baseline.Size) throw new InvalidDataException("Pack dimensions mismatch: " + asset);
                byte[] pixels = Pixels(old); Dishwasher.DesktopTextureColors.Preserve(Pixels(baseline), pixels);
                string output = Path.Combine(args[3], relative); Save(pixels, old.Width, old.Height, output);
                entry["Sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(output)));
                entry["Method"] = "neural-luminance-original-chroma-v2"; count++;
            }
            manifest["Recipe"] = "original-chroma-v2";
            Directory.CreateDirectory(args[3]);
            File.WriteAllText(Path.Combine(args[3], manifestName), manifest.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"Preserved original colours in {count} sheets; old pack untouched.");
    }
    else if (args[0] == "bloom-audit")
    {
        long tested = 0, falseBlue = 0, lostBlue = 0;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(args[1], "gfx/maps"), "*.png"))
        {
            using var original = Read(path); using var baseline = Resize(original, 2);
            using var hd = Read(Path.Combine(args[2], Path.GetRelativePath(args[1], path)));
            var b = Pixels(baseline); var a = Pixels(hd); long falseHere = 0, visible = 0;
            for (int i = 0; i < a.Length; i += 4)
            {
                if (b[i + 3] < 128) continue;
                bool sourceBlue = b[i] > b[i + 2], neuralBlue = a[i] > a[i + 2];
                if (!sourceBlue && neuralBlue) { falseBlue++; falseHere++; }
                if (sourceBlue && !neuralBlue) lostBlue++;
                tested++; visible++;
            }
            Console.WriteLine($"{Path.GetFileName(path)}: invented bloom pixels={falseHere}/{visible}");
        }
        Console.WriteLine($"TOTAL visible={tested}, invented bloom={falseBlue}, lost bloom={lostBlue}");
    }
    else if (args[0] == "finish")
    {
        using var original = Read(args[1]); using var ai = Read(args[2]); using var baseline = Resize(original, 2);
        if (ai.Size != baseline.Size) throw new InvalidDataException("AI output dimensions do not match 2x source.");
        var a = Pixels(ai); var b = Pixels(baseline);
        for (int i = 0; i < a.Length; i += 4)
        {
            // A restrained neural blend preserves the painted grain. Alpha is
            // always the original silhouette, not a hallucinated neural mask.
            for (int c = 0; c < 3; c++) a[i + c] = b[i + 3] == 0 ? (byte)0 : (byte)Math.Clamp((int)Math.Round(a[i + c] * 0.8 + b[i + c] * 0.2), 0, 255);
            a[i + 3] = b[i + 3];
        }
        Dishwasher.DesktopTextureColors.Preserve(b, a);
        Save(a, ai.Width, ai.Height, args[3]);
    }
    else if (args[0] == "lettering-check")
    {
        using var before=Read(args[1]);using var after=Read(args[2]);
        Lettering.Validate(before,after,args[3]);
    }
    else if (args[0] == "lettering")
    {
        using var original=Read(args[1]); using var hd=Read(args[2]);
        using var restored=Lettering.Restore(original,hd,args[4]);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
        restored.Save(args[3],ImageFormat.Png);
        Console.WriteLine("Reviewed lettering restored: "+args[4]);
    }
    else if (args[0] == "resize")
    {
        using var original = Read(args[1]); using var resized = Resize(original, 2);
        var data = Pixels(resized);
        for (int i = 0; i < data.Length; i += 4) if (data[i + 3] == 0) data[i] = data[i + 1] = data[i + 2] = 0;
        Save(data, resized.Width, resized.Height, args[2]);
    }
    else if (args[0] == "overview")
    {
        string[] files = Directory.GetFiles(args[1], "*.png"); Array.Sort(files);
        using var result = new Bitmap(1024, ((files.Length + 1) / 2) * 540);
        using var g = Graphics.FromImage(result); g.Clear(Color.FromArgb(32, 32, 34));
        using var font = new Font("Segoe UI", 14);
        for (int i = 0; i < files.Length; i++)
        {
            using var image = Read(files[i]); int x = i % 2 * 512, y = i / 2 * 540;
            g.DrawString(Path.GetFileName(files[i]), font, Brushes.White, x + 8, y + 3);
            g.DrawImage(image, new Rectangle(x, y + 28, 512, 512));
        }
        result.Save(args[2], ImageFormat.Png);
    }
    else if (args[0] == "finish4")
    {
        using var original = Read(args[1]); using var ai = Read(args[2]); using var baseline = Resize(original, 2);
        if (ai.Width != original.Width * 4 || ai.Height != original.Height * 4) throw new InvalidDataException("Expected 4x neural output.");
        using var reduced = new Bitmap(baseline.Width, baseline.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(reduced))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(ai, new Rectangle(0, 0, reduced.Width, reduced.Height));
        }
        var a = Pixels(reduced); var b = Pixels(baseline);
        for (int i = 0; i < a.Length; i += 4)
        {
            for (int c = 0; c < 3; c++) a[i + c] = b[i + 3] == 0 ? (byte)0 : (byte)Math.Clamp((int)Math.Round(a[i + c] * 0.9 + b[i + c] * 0.1), 0, 255);
            a[i + 3] = b[i + 3];
        }
        Dishwasher.DesktopTextureColors.Preserve(b, a);
        Save(a, reduced.Width, reduced.Height, args[3]);
    }
    else if (args[0] == "audit" || args[0] == "audit-maps")
    {
        int count = 0; long transparent = 0, partial = 0;
        foreach (string path in Directory.EnumerateFiles(Path.Combine(args[1], args[0] == "audit-maps" ? "gfx/maps" : "gfx"), "*.png", SearchOption.AllDirectories))
        {
            string asset=Path.GetRelativePath(args[1],path).Replace('\\','/');
            string target = Path.Combine(args[2], Path.GetRelativePath(args[1], path));
            using var original = Read(path); using var hd = Read(target); using var expected = Resize(original, 2);
            if (hd.Size != expected.Size) throw new InvalidDataException("Wrong HD dimensions: " + path);
            var a = Pixels(hd); var b = Pixels(expected);
            for (int i = 0; i < a.Length; i += 4)
            {
                if (a[i + 3] != b[i + 3]) throw new InvalidDataException("Alpha changed: " + path);
                bool lettering=Lettering.Contains(asset,(i/4)%hd.Width,(i/4)/hd.Width);
                if (lettering && (a[i]>a[i+2])!=(b[i]>b[i+2])) throw new InvalidDataException("Lettering changed bloom classification: "+path);
                if (!lettering && a[i + 3] > 0 && (a[i] - a[i + 2] != b[i] - b[i + 2] || a[i + 1] - a[i + 2] != b[i + 1] - b[i + 2]))
                    throw new InvalidDataException("Authored colour/mask changed: " + path);
                if (a[i + 3] == 0)
                {
                    transparent++;
                    if (a[i] != 0 || a[i + 1] != 0 || a[i + 2] != 0) throw new InvalidDataException("Transparent RGB not zero: " + path);
                }
                else if (a[i + 3] != 255) partial++;
            }
            count++;
        }
        Console.WriteLine($"PASS: {count} textures; exact 2x sizes, original bicubic alpha, {transparent} transparent and {partial} partial-alpha pixels preserved.");
    }
    else if (args[0] == "compare")
    {
        using var original = Read(args[1]); using var hd = Read(args[2]);
        int x = int.Parse(args[4]), y = int.Parse(args[5]), w = int.Parse(args[6]), h = int.Parse(args[7]);
        using var result = new Bitmap(w * 4 + 40, h * 2 + 60, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result); g.Clear(Color.FromArgb(28, 28, 30));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(original, new Rectangle(10, 45, w * 2, h * 2), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
        g.DrawImage(hd, new Rectangle(w * 2 + 30, 45, w * 2, h * 2), new Rectangle(x * 2, y * 2, w * 2, h * 2), GraphicsUnit.Pixel);
        using var font = new Font("Segoe UI", 13);
        g.DrawString(args.Length > 8 ? args[8] : "Enlarged original", font, Brushes.White, 10, 10);
        g.DrawString(args.Length > 9 ? args[9] : "Upscaled 2x", font, Brushes.White, w * 2 + 30, 10);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!); result.Save(args[3], ImageFormat.Png);
    }
    else throw new ArgumentException("Unknown operation.");
    return 0;
}
catch (Exception e) { Console.Error.WriteLine(e); return 1; }
