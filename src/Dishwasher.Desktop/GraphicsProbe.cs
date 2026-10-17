using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Dishwasher
{
    // Explicit integration diagnostic, never used as a replacement for Game1.
    internal sealed class GraphicsProbe : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        public int Failed { get; private set; }
        public static string TextureAuditRoot;
        public static bool Benchmark;
        private readonly System.Diagnostics.Stopwatch _benchmarkClock = new();
        private FixedSimulationClock _simulation = new();
        private readonly DesktopFramePacer _pacer = new();
        private double _previousTime;
        private int _frames;
        private Texture2D _benchmarkPixel;
        private DesktopSpriteBatch _benchmarkBatch;
        private bool _benchmarkMeasuring;
        public GraphicsProbe()
        {
            _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 320, PreferredBackBufferHeight = 180 };
            Window.Title = "DishSoap: DesktopGL diagnostics (not the game)";
            Content.RootDirectory = AppContext.BaseDirectory;
        }
        protected override void LoadContent()
        {
            Log.Info("[graphics] Device: " + GraphicsDevice.Adapter.Description + "; profile=" + GraphicsDevice.GraphicsProfile);
            using var target = new RenderTarget2D(GraphicsDevice, 4, 4);
            using var pixel = new Texture2D(GraphicsDevice, 1, 1);
            using var batch = new SpriteBatch(GraphicsDevice);
            pixel.SetData(new[] { new Color(200, 100, 50, 128) });
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(Color.Black);
            batch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
            batch.Draw(pixel, new Rectangle(0, 0, 4, 4), Color.White);
            batch.End();
            GraphicsDevice.SetRenderTarget(null);
            var rgba = new Color[16]; target.GetData(rgba);
            if (Math.Abs(rgba[0].R - 100) > 2 || Math.Abs(rgba[0].G - 50) > 2 || Math.Abs(rgba[0].B - 25) > 2)
            {
                Failed++; Log.Error("[graphics] Straight-alpha mismatch: " + rgba[0]);
            }
            else Log.Info("[graphics] Straight-alpha GPU readback PASS: " + rgba[0]);
            foreach (string path in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "fx"), "*.xnb"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                try
                {
                    using var shaderBatch = new SpriteBatch(GraphicsDevice);
                    Effect effect = Content.Load<Effect>("fx/" + name);
                    if (effect.Parameters["MatrixTransform"] == null && name != "Sprite" && name != "SpriteShadow")
                        throw new InvalidDataException("Missing MatrixTransform");
                    GraphicsDevice.SetRenderTarget(target);
                    GraphicsDevice.Clear(Color.Black);
                    effect.Begin();
                    shaderBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied, effect: effect);
                    shaderBatch.Draw(pixel, new Rectangle(0, 0, 4, 4), Color.White);
                    shaderBatch.End();
                    GraphicsDevice.SetRenderTarget(null);
                    target.GetData(rgba);
                    Log.Info("[graphics] Shader load/draw/readback PASS: " + name);
                }
                catch (Exception ex)
                {
                    Failed++; GraphicsDevice.SetRenderTarget(null); Log.Exception("[graphics] Shader " + name, ex);
                }
            }
            DesktopPresentation.Initialize(GraphicsDevice);
            {
                DesktopPresentation.Bind(GraphicsDevice);
                GraphicsDevice.Clear(new Color(20, 40, 60));
                DesktopPresentation.Present(GraphicsDevice);
                var frame = new Color[320 * 180];
                GraphicsDevice.GetBackBufferData(frame);
                var center = frame[90 * 320 + 160];
                if (center.R != 20 || center.G != 40 || center.B != 60)
                { Failed++; Log.Error("[graphics] Presentation mismatch: " + center); }
                else Log.Info("[graphics] Fixed full-resolution presentation GPU readback PASS");
            }
            DesktopSceneFilter.Initialize(Services, GraphicsDevice);
            using (var edge = new RenderTarget2D(GraphicsDevice, 128, 128))
            using (var source = new Texture2D(GraphicsDevice, 128, 128))
            {
                var pixels = new Color[128 * 128];
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                    pixels[y * 128 + x] = x / 2 >= y / 4 + 20 ? Color.White : Color.Black;
                source.SetData(pixels);
                GraphicsDevice.SetRenderTarget(edge);
                batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
                batch.Draw(source, Vector2.Zero, Color.White); batch.End();
                using (var file = File.Create(Path.Combine(DesktopPaths.Logs, "edge-before.png")))
                    source.SaveAsPng(file, 128, 128);
                DesktopSceneFilter.Apply(GraphicsDevice, edge);
                GraphicsDevice.SetRenderTarget(null);
                var filtered = new Color[pixels.Length]; edge.GetData(filtered);
                int blended = 0;
                for (int i = 0; i < pixels.Length; i++) if (filtered[i].R > 0 && filtered[i].R < 255) blended++;
                if (blended == 0 || filtered[0] != Color.Black || filtered[127] != Color.White)
                { Failed++; Log.Error("[graphics] Edge smoothing did not preserve flats / smooth diagonals"); }
                else Log.Info("[graphics] Edge smoothing PASS: " + blended + " diagonal pixels softened; flat regions intact");
                using var result = new Texture2D(GraphicsDevice, 128, 128);
                result.SetData(filtered);
                using (var file = File.Create(Path.Combine(DesktopPaths.Logs, "edge-after.png")))
                    result.SaveAsPng(file, 128, 128);
            }
            DesktopSceneFilter.Dispose();
            DesktopPresentation.Dispose();
            Failed += HdGraphicsChecks.Run(GraphicsDevice, Services, TextureAuditRoot);
            Log.Info("[graphics] Failures=" + Failed + "; this test does not validate the visual composition of the game.");
            if (Benchmark && Failed == 0)
            {
                IsFixedTimeStep = false;
                _graphics.SynchronizeWithVerticalRetrace = false; _graphics.ApplyChanges();
                Window.Title = "DishSoap: technical 180 FPS test (no gameplay or input)";
                _benchmarkPixel = new Texture2D(GraphicsDevice, 1, 1); _benchmarkPixel.SetData(new[] { Color.White });
                _benchmarkBatch = new DesktopSpriteBatch(GraphicsDevice);
                DesktopPresentation.Initialize(GraphicsDevice);
                _benchmarkClock.Start();
            }
            else Exit();
        }
        protected override void Update(GameTime gameTime)
        {
            if (!_benchmarkClock.IsRunning) return;
            _pacer.Wait(180);
            double now = _benchmarkClock.Elapsed.TotalSeconds;
            _simulation.Advance(now - _previousTime, () => { }); _previousTime = now;
            if (!_benchmarkMeasuring && now >= 1)
            {
                _simulation = new FixedSimulationClock(); _frames = 0; _previousTime = 0;
                _benchmarkClock.Restart(); _benchmarkMeasuring = true; return;
            }
            if (_benchmarkMeasuring && now >= 3)
            {
                double fps = _frames / now, hz = _simulation.Ticks / now;
                Log.Info("[benchmark] 1080p render=" + fps.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " FPS; simulation=" + hz.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " Hz; target=180; synthetic scene, not campaign benchmark.");
                if (fps < 165 || hz < 59 || hz > 61) { Failed++; Log.Error("[benchmark] Timing outside tolerance."); }
                Exit();
            }
        }
        protected override void Draw(GameTime gameTime)
        {
            if (!_benchmarkClock.IsRunning) return;
            DesktopPresentation.Bind(GraphicsDevice); GraphicsDevice.Clear(new Color(20, 22, 25));
            _benchmarkBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            for (int i = 0; i < 100; i++)
                _benchmarkBatch.Draw(_benchmarkPixel, new Rectangle((i % 20) * 64, (i / 20) * 140, 40, 100), new Color(100, 120, 150, 180));
            _benchmarkBatch.End(); DesktopPresentation.Present(GraphicsDevice);
            DesktopFpsOverlay.Draw(GraphicsDevice, 180, 60); _frames++;
        }
    }
}
