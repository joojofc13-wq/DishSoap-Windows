using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Dishwasher
{
    internal static class RenderingRegressionChecks
    {
        internal static int Run(GraphicsDevice device, IServiceProvider services)
        {
            int failures = 0;
            void Check(bool value, string message)
            { if (value) Log.Info("[render-check] PASS: " + message); else { failures++; Log.Error("[render-check] FAIL: " + message); } }
            using var content = new ContentManager(services, AppContext.BaseDirectory);
            using var batch = new SpriteBatch(device);
            using var target = new RenderTarget2D(device, 256, 128);
            using var background = new Texture2D(device, 256, 128);
            using var mask = new Texture2D(device, 256, 128);
            var backdrop = new Color[256 * 128]; var masks = new Color[backdrop.Length];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 256; x++)
                backdrop[y * 256 + x] = ((x / 8 + y / 8) & 1) == 0 ? new Color(60, 80, 100) : new Color(160, 130, 110);
            background.SetData(backdrop);
            Color[] Refract(Effect effect, Color color)
            {
                for (int y = 0; y < 128; y++) for (int x = 0; x < 256; x++)
                    masks[y * 256 + x] = x >= 64 && x < 192 && y >= 24 && y < 104 ? color : Color.Black;
                mask.SetData(masks); device.SetRenderTarget(target); device.Clear(Color.Black);
                effect.Parameters["edgeBlur"].SetValue(0f); effect.Begin();
                batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
                effect.CurrentTechnique.Passes[0].Begin();
                device.Textures[1] = background; device.SamplerStates[1] = SamplerState.PointClamp;
                batch.Draw(mask, new Rectangle(0, 0, 256, 128), Color.White); batch.End();
                device.SetRenderTarget(null); device.Textures[1] = null;
                var result = new Color[backdrop.Length]; target.GetData(result); return result;
            }
            int Changed(Color[] a, Color[] b)
            { int count = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) count++; return count; }
            void Capture(Color[] pixels, string name)
            {
                using var image = new Texture2D(device, 256, 128); image.SetData(pixels);
                using var file = File.Create(Path.Combine(DesktopPaths.Logs, name + ".png")); image.SaveAsPng(file, 256, 128);
            }
            var lens = content.Load<Effect>("fx/lense");
            var empty = Refract(lens, Color.Black); var green = Refract(lens, new Color(0, 160, 0)); var red = Refract(lens, new Color(160, 0, 0));
            Check(Changed(empty, backdrop) == 0, "empty refraction mask leaves the scene unchanged");
            Check(Changed(green, empty) > 1000, "green cloak mask visibly refracts the scene");
            Check(Changed(red, empty) > 0, "red impact refraction remains active");
            Capture(green, "cloak-after");

            using var bloomSource = new Texture2D(device, 256, 128);
            var ramp = new Color[backdrop.Length];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 256; x++) ramp[y * 256 + x] = new Color(100, 100, 98 + x / 8);
            bloomSource.SetData(ramp);
            Color[] Bloom(Effect effect)
            {
                device.SetRenderTarget(target); device.Clear(Color.Black); effect.Begin();
                batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, effect: effect);
                batch.Draw(bloomSource, new Rectangle(0, 0, 256, 128), Color.White); batch.End(); device.SetRenderTarget(null);
                var result = new Color[backdrop.Length]; target.GetData(result); return result;
            }
            int MaxJump(Color[] pixels)
            { int max = 0; for (int x = 1; x < 256; x++) max = Math.Max(max, Math.Abs(pixels[64 * 256 + x].B - pixels[64 * 256 + x - 1].B)); return max; }
            var bloom = content.Load<Effect>("fx/bloom"); var smooth = Bloom(bloom);
            Check(MaxJump(smooth) <= 12, "near-neutral theatre lighting has no binary bloom jumps (max=" + MaxJump(smooth) + ")");
            Capture(smooth, "theatre-bloom-after");

            // Optional old compiled effects allow the same GPU fixture to prove
            // both failures before the fix, without shipping an extra shader.
            string referenceRoot = Path.Combine(DesktopPaths.Cache, "ReferenceEffects");
            if (File.Exists(Path.Combine(referenceRoot, "lense.xnb")) && File.Exists(Path.Combine(referenceRoot, "bloom.xnb")))
            {
                using var reference = new ContentManager(services, referenceRoot);
                var oldLens = reference.Load<Effect>("lense"); var oldGreen = Refract(oldLens, new Color(0, 160, 0));
                Check(Changed(oldGreen, empty) == 0, "old lens reproduces completely invisible green cloak");
                Check(Changed(Refract(oldLens, new Color(160, 0, 0)), red) == 0, "red refraction matches the previous shader exactly");
                Capture(oldGreen, "cloak-before");
                var oldBloom = Bloom(reference.Load<Effect>("bloom"));
                Check(MaxJump(oldBloom) > 12, "old bloom reproduces hard spotlight colour blocks (max=" + MaxJump(oldBloom) + ")");
                Capture(oldBloom, "theatre-bloom-before");
            }
            return failures;
        }
    }
}
