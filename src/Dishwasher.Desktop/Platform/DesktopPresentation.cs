using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dishwasher
{
    public static class DesktopPresentation
    {
        private static RenderTarget2D _canvas;
        private static SpriteBatch _batch;
        public static void Initialize(GraphicsDevice device)
        {
            if (_canvas != null) return;
            _canvas = new RenderTarget2D(device, DesktopHdTextures.RenderWidth, DesktopHdTextures.RenderHeight, false, SurfaceFormat.Color,
                DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            DesktopHdTextures.Register(_canvas, 1280, 720);
            _batch = new SpriteBatch(device);
        }
        // Replaces ONLY SetRenderTarget(null) in the locally generated source.
        // Original render targets and effect passes remain intact at 1280x720.
        public static void Bind(GraphicsDevice device) => device.SetRenderTarget(_canvas);
        public static Rectangle Fit(int width, int height)
        {
            double scale = Math.Min(width / 1280.0, height / 720.0);
            int w = (int)Math.Round(1280 * scale), h = (int)Math.Round(720 * scale);
            return new Rectangle((width - w) / 2, (height - h) / 2, w, h);
        }
        public static void Present(GraphicsDevice device)
        {
            device.SetRenderTarget(null);
            device.Clear(Color.Black);
            var p = device.PresentationParameters;
            _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
            _batch.Draw(_canvas, Fit(p.BackBufferWidth, p.BackBufferHeight), Color.White);
            _batch.End();
        }
        public static void Dispose()
        {
            _batch?.Dispose(); _canvas?.Dispose();
            _batch = null; _canvas = null;
        }
    }
}
