using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dishwasher
{
    // Original source pixels remain the public coordinate system. Larger
    // textures and targets never alter world positions, pivots or animation UVs.
    public sealed class DesktopSpriteBatch : SpriteBatch
    {
        public DesktopSpriteBatch(GraphicsDevice device) : base(device) { }
        public new void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null,
            SamplerState samplerState = null, DepthStencilState depthStencilState = null,
            RasterizerState rasterizerState = null, Effect effect = null, Matrix? transformMatrix = null)
        {
            var s = DesktopHdTextures.TargetScale(GraphicsDevice);
            Matrix transform = (transformMatrix ?? Matrix.Identity) * Matrix.CreateScale(s.X, s.Y, 1);
            base.Begin(sortMode, blendState, samplerState ?? SamplerState.LinearClamp, depthStencilState, rasterizerState, effect, transform);
        }
        public new void Draw(Texture2D texture, Vector2 position, Color color) =>
            Draw(texture, position, null, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
        public new void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color) =>
            Draw(texture, position, sourceRectangle, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
        public new void Draw(Texture2D texture, Rectangle destinationRectangle, Color color) =>
            Draw(texture, destinationRectangle, null, color, 0, Vector2.Zero, SpriteEffects.None, 0);
        public new void Draw(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color) =>
            Draw(texture, destinationRectangle, sourceRectangle, color, 0, Vector2.Zero, SpriteEffects.None, 0);
        public new void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
            float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth) =>
            Draw(texture, position, sourceRectangle, color, rotation, origin, new Vector2(scale), effects, layerDepth);
        public new void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
            float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
        {
            var s = DesktopHdTextures.Scale(texture);
            base.Draw(texture, position, DesktopHdTextures.Source(texture, sourceRectangle), color, rotation, origin * s, scale / s, effects, layerDepth);
        }
        public new void Draw(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color,
            float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)
        {
            base.Draw(texture, destinationRectangle, DesktopHdTextures.Source(texture, sourceRectangle), color,
                rotation, origin * DesktopHdTextures.Scale(texture), effects, layerDepth);
        }
        public new void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color) =>
            DrawString(spriteFont, text, position, color, 0, Vector2.Zero, Vector2.One, SpriteEffects.None, 0);
        public new void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation,
            Vector2 origin, float scale, SpriteEffects effects, float layerDepth) =>
            DrawString(spriteFont, text, position, color, rotation, origin, new Vector2(scale), effects, layerDepth);
        public new void DrawString(SpriteFont spriteFont, string text, Vector2 position, Color color, float rotation,
            Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
        {
            float s = DesktopHdTextures.FontScale(spriteFont);
            base.DrawString(spriteFont, text, position, color, rotation, origin * s, scale / s, effects, layerDepth);
        }
    }
}
