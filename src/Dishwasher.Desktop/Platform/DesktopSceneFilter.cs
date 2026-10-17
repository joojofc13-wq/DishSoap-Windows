using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Dishwasher
{
    public static class DesktopSceneFilter
    {
        private static ContentManager _content;
        private static Effect _effect;
        private static SpriteBatch _batch;
        private static RenderTarget2D _copy;
        public static void Initialize(IServiceProvider services, GraphicsDevice device)
        {
            if (_effect != null) return;
            _content = new ContentManager(services, AppContext.BaseDirectory);
            _effect = _content.Load<Effect>("fx/sceneEdges");
            _batch = new SpriteBatch(device);
        }
        // Hook immediately after the background layers, before the characters,
        // wall blood, HUD and the game's remaining post effects are composited.
        public static void Apply(GraphicsDevice device, RenderTarget2D scene)
        {
            // The restored textures already have smoother edges. Avoid a
            // second smoothing pass over the higher-resolution painting.
            if (DesktopHdTextures.Active || DesktopInput.Settings?.TextureQuality == "Original") return;
            if (_effect == null) throw new InvalidOperationException("Scene filter has not been initialized.");
            if (_copy == null || _copy.Width != scene.Width || _copy.Height != scene.Height)
            {
                _copy?.Dispose();
                _copy = new RenderTarget2D(device, scene.Width, scene.Height, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            }
            device.SetRenderTarget(_copy);
            _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
            _batch.Draw(scene, new Rectangle(0, 0, scene.Width, scene.Height), Color.White);
            _batch.End();
            device.SetRenderTarget(scene);
            _effect.Parameters["TexelSize"].SetValue(new Vector2(1f / DesktopHdTextures.Width(scene), 1f / DesktopHdTextures.Height(scene)));
            _effect.Parameters["MatrixTransform"].SetValue(Matrix.CreateOrthographicOffCenter(
                0, scene.Width, scene.Height, 0, 0, -1));
            _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, effect: _effect);
            _batch.Draw(_copy, new Rectangle(0, 0, scene.Width, scene.Height), Color.White);
            _batch.End();
        }
        public static void Dispose()
        {
            _copy?.Dispose(); _batch?.Dispose(); _content?.Dispose();
            _copy = null; _batch = null; _effect = null; _content = null;
        }
    }
}
