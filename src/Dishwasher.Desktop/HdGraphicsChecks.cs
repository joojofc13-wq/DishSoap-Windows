using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dishwasher
{
    internal static class HdGraphicsChecks
    {
        public static int Run(GraphicsDevice device, IServiceProvider services, string auditRoot)
        {
            int failures = 0;
            void Check(bool success, string message)
            {
                if (!success) { failures++; Log.Error("[hd-check] FAIL: " + message); }
                else Log.Info("[hd-check] PASS: " + message);
            }
            using var atlas = new Texture2D(device, 4, 4);
            var pixels = new Color[16];
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
                pixels[y * 4 + x] = y < 2 ? (x < 2 ? Color.Red : Color.Lime) : (x < 2 ? Color.Blue : Color.White);
            atlas.SetData(pixels); DesktopHdTextures.Register(atlas, 2, 2);
            using var target = DesktopHdTextures.CreateRenderTarget(device, 64, 64, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            using var batch = new DesktopSpriteBatch(device);
            Check(target.Width == 96 && target.Height == 96 && DesktopHdTextures.Width(target) == 64, "1080p render target preserves logical size");
            Check(DesktopHdTextures.Source(atlas, new Rectangle(1, 0, 1, 1)) == new Rectangle(2, 0, 2, 2), "atlas source rectangle scales exactly");
            device.SetRenderTarget(target); device.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
            batch.Draw(atlas, new Vector2(4, 4), Color.White);
            batch.Draw(atlas, new Rectangle(16, 4, 8, 8), new Rectangle(1, 0, 1, 1), Color.White);
            batch.Draw(atlas, new Vector2(32, 4), new Rectangle(0, 1, 1, 1), Color.White, 0, Vector2.Zero, 8f, SpriteEffects.None, 0);
            batch.Draw(atlas, new Vector2(48, 8), new Rectangle(1, 1, 1, 1), Color.White, 0, new Vector2(0.5f), new Vector2(8, 6), SpriteEffects.FlipHorizontally, 0);
            batch.Draw(atlas, new Rectangle(8, 32, 8, 8), new Rectangle(0, 0, 1, 1), Color.White, 0, new Vector2(0.5f), SpriteEffects.None, 0);
            batch.End(); device.SetRenderTarget(null);
            var frame = new Color[96 * 96]; target.GetData(frame);
            Color At(int x, int y) => frame[y * 96 + x];
            Check(At(6, 6) == Color.Red && At(8, 8) == Color.White && At(9, 9) == Color.Black, "unscaled position draw retains original world size");
            Check(At(30, 12) == Color.Lime && At(23, 12) == Color.Black, "destination rectangle selects correct atlas cell");
            Check(At(54, 12) == Color.Blue, "scalar scale preserves source region");
            Check(At(72, 12) == Color.White && At(78, 12) == Color.Black, "vector scale, flip and pivot preserve bounds");
            Check(At(12, 48) == Color.Red && At(18, 48) == Color.Black, "rectangle rotation origin uses logical source pixels");

            using var effectContent = new Microsoft.Xna.Framework.Content.ContentManager(services, AppContext.BaseDirectory);
            // Exercise the actual bloom shader: a one-byte blue drift in a
            // neutral texture used to turn a dark floor into bright coloured dots.
            using (var bloomSource = new Texture2D(device, 1, 1))
            using (var bloomTarget = new RenderTarget2D(device, 4, 4))
            using (var bloomBatch = new SpriteBatch(device))
            {
                var bloom = effectContent.Load<Effect>("fx/bloom");
                Color RunBloom(Color input)
                {
                    bloomSource.SetData(new[] { input }); device.SetRenderTarget(bloomTarget); device.Clear(Color.Black);
                    bloom.Begin(); bloomBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, effect: bloom);
                    bloomBatch.Draw(bloomSource, new Rectangle(0, 0, 4, 4), Color.White); bloomBatch.End(); device.SetRenderTarget(null);
                    var output = new Color[16]; bloomTarget.GetData(output); return output[5];
                }
                byte[] neutral = { 100,100,100,255 }, drift = { 101,100,100,255 };
                var faulty = RunBloom(new Color(drift[2], drift[1], drift[0], drift[3]));
                DesktopTextureColors.Preserve(neutral, drift);
                var corrected = RunBloom(new Color(drift[2], drift[1], drift[0], drift[3]));
                Check(faulty.B > 100 && corrected.B == 0, "GPU bloom rejects neural chroma noise after colour preservation");
                Check(RunBloom(new Color(60, 60, 180)).B > 200, "GPU bloom retains authored blue light effects");
            }
            var effect = effectContent.Load<Effect>("fx/sceneEdges");
            effect.Parameters["TexelSize"].SetValue(new Vector2(1f / 4));
            device.SetRenderTarget(target); device.Clear(Color.Black);
            effect.Begin();
            batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            effect.CurrentTechnique.Passes[0].Begin();
            batch.Draw(atlas, new Rectangle(56, 56, 8, 8), new Rectangle(0, 0, 1, 1), Color.White);
            batch.End(); device.SetRenderTarget(null); target.GetData(frame);
            Check(At(92, 92).R > 200 && At(83, 92) == Color.Black, "custom effect projection covers 1080p target");

            var font = DesktopHdTextures.CreateFont(atlas, new List<Rectangle> { new(0, 0, 1, 1) },
                new List<Rectangle> { new(0, 0, 1, 1) }, new List<char> { 'A' }, 2, 0, new List<Vector3> { new(0, 1, 0) }, null);
            Check(DesktopHdTextures.MeasureString(font, "AA").X == 2 && DesktopHdTextures.FontScale(font) == 2, "HD font metrics retain logical character advances");
            device.SetRenderTarget(target); device.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
            batch.DrawString(font, "AA", new Vector2(8, 8), Color.White, 0, Vector2.Zero, new Vector2(8), SpriteEffects.None, 0);
            batch.End(); device.SetRenderTarget(null); target.GetData(frame);
            Check(At(18, 18).R > 200 && At(30, 18).R > 200 && At(36, 18) == Color.Black, "HD font drawing retains layout and sharp glyph atlas");

            if (auditRoot != null)
            {
                DesktopHdTextures.Initialize(auditRoot);
                var pack = System.Text.Json.JsonSerializer.Deserialize<DesktopHdTextures.Pack>(File.ReadAllText(Path.Combine(DesktopHdTextures.PackRoot(auditRoot), "manifest.json")));
                int count = 0;
                foreach (var entry in pack.Textures)
                {
                    if (!DesktopHdTextures.TryLoad(device, entry.Asset, out var texture)) throw new InvalidDataException(entry.Asset);
                    using (texture)
                    {
                        Check(texture.Width == entry.Width * 2 && texture.Height == entry.Height * 2 && DesktopHdTextures.Width(texture) == entry.Width,
                            "texture GPU/hash/dimensions " + entry.Asset);
                    }
                    count++;
                }
                Check(count == pack.TextureCount, "complete HD texture pack loaded on GPU: " + count);
            }
            return failures;
        }
    }
}
