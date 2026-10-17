using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dishwasher
{
    public static class DesktopFpsOverlay
    {
        private static Texture2D _pixel;
        private static SpriteBatch _batch;
        private static readonly Dictionary<char, string[]> Glyphs = new()
        {
            ['0']=new[]{"01110","10001","10011","10101","11001","10001","01110"},
            ['1']=new[]{"00100","01100","00100","00100","00100","00100","01110"},
            ['2']=new[]{"01110","10001","00001","00010","00100","01000","11111"},
            ['3']=new[]{"11110","00001","00001","01110","00001","00001","11110"},
            ['4']=new[]{"00010","00110","01010","10010","11111","00010","00010"},
            ['5']=new[]{"11111","10000","10000","11110","00001","00001","11110"},
            ['6']=new[]{"01110","10000","10000","11110","10001","10001","01110"},
            ['7']=new[]{"11111","00001","00010","00100","01000","01000","01000"},
            ['8']=new[]{"01110","10001","10001","01110","10001","10001","01110"},
            ['9']=new[]{"01110","10001","10001","01111","00001","00001","01110"},
            ['F']=new[]{"11111","10000","10000","11110","10000","10000","10000"},
            ['P']=new[]{"11110","10001","10001","11110","10000","10000","10000"},
            ['S']=new[]{"01111","10000","10000","01110","00001","00001","11110"},
            ['H']=new[]{"10001","10001","10001","11111","10001","10001","10001"},
            ['Z']=new[]{"11111","00001","00010","00100","01000","10000","11111"}
        };
        public static void Draw(GraphicsDevice device, double fps, double simulationHz)
        {
            if (_pixel == null) { _pixel = new Texture2D(device, 1, 1); _pixel.SetData(new[] { Color.White }); _batch = new SpriteBatch(device); }
            _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            _batch.Draw(_pixel, new Rectangle(12, 12, 136, 46), new Color(0, 0, 0, 180));
            Text(((int)Math.Round(fps)).ToString() + " FPS", 20, 18, Color.White);
            Text(((int)Math.Round(simulationHz)).ToString() + " HZ", 20, 38, new Color(170, 210, 175));
            _batch.End();
        }
        private static void Text(string text, int x, int y, Color color)
        {
            foreach (char c in text)
            {
                if (Glyphs.TryGetValue(c, out var rows))
                    for (int row = 0; row < 7; row++) for (int column = 0; column < 5; column++)
                        if (rows[row][column] == '1') _batch.Draw(_pixel, new Rectangle(x + column * 2, y + row * 2, 2, 2), color);
                x += 12;
            }
        }
        public static void Dispose() { _batch?.Dispose(); _pixel?.Dispose(); _batch = null; _pixel = null; }
    }
}
