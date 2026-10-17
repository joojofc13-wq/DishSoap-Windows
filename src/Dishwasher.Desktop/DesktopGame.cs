#if DISHWASHER_GAME
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Dishwasher
{
    internal sealed class DesktopGame : projectDish.Game1
    {
        public static double SmokeSeconds;
        public static string CaptureDirectory;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private int _lastSecond = -1, _lastCapture = -1;
        private bool _profileReady;
        private double _fpsStart;
        private int _fpsFrames;
        private readonly FixedSimulationClock _simulation = new();
        private readonly DesktopFramePacer _pacer = new();
        private double _lastUpdateTime = -1, _lastDrawTime = -1;
        private long _lastStatsTicks;
        private int _statsSeconds;
        private double _shownFps = 0, _shownSimulationHz = 60;
        private projectDish.Character[] _snapshotCharacters = Array.Empty<projectDish.Character>();
        private Vector2[] _beforePositions = Array.Empty<Vector2>(), _drawPositions = Array.Empty<Vector2>();
        private bool[] _changed = Array.Empty<bool>();
        private Vector2 _beforeScroll, _beforeGlobalScroll;
        private bool _haveSnapshot;
        protected override void Initialize()
        {
            if (CaptureDirectory != null) Directory.CreateDirectory(CaptureDirectory);
            base.Initialize();
            DesktopSceneFilter.Initialize(Services, GraphicsDevice);
            var manager = (GraphicsDeviceManager)Services.GetService(typeof(IGraphicsDeviceManager));
            var settings = DesktopInput.Settings;
            manager.PreferredBackBufferWidth = settings.Width;
            manager.PreferredBackBufferHeight = settings.Height;
            manager.IsFullScreen = settings.Fullscreen;
            manager.ApplyChanges();
            Window.AllowUserResizing = false;
        }
        protected override void Update(GameTime gameTime)
        {
            _pacer.Wait(IsActive ? DesktopInput.Settings.FrameLimit : Math.Min(30, DesktopInput.Settings.FrameLimit));
            double now = _clock.Elapsed.TotalSeconds;
            double elapsed = _lastUpdateTime < 0 ? FixedSimulationClock.StepSeconds : now - _lastUpdateTime;
            _lastUpdateTime = now;
            _simulation.Advance(elapsed, () => {
                DesktopInput.MenuContext = gameMode != 0 || projectDish.Globals.paused;
                CapturePreviousState();
                base.Update(new GameTime(TimeSpan.FromSeconds(_simulation.Ticks * FixedSimulationClock.StepSeconds),
                    TimeSpan.FromSeconds(FixedSimulationClock.StepSeconds)));
            });
            if (!_profileReady && gameMode == 1 && projectDish.Globals.settings != null)
            {
                DesktopProfile.Initialize();
                _profileReady = true;
            }
            int second = (int)now;
            if (SmokeSeconds > 0 && second != _lastSecond)
            {
                _lastSecond = second;
                Log.Info("[smoke] t=" + second + " mode=" + gameMode + " menu=" + mainMenu?.getLevel()
                    + " fail=" + projectDish.Globals.failDieLev + " initialRead=" + projectDish.Globals.initialRead
                    + " playSeconds=" + projectDish.Globals.player?.playSeconds);
            }
            if (SmokeSeconds > 0 && now >= SmokeSeconds) Exit();
        }
        protected override void Draw(GameTime gameTime)
        {
            DesktopPresentation.Initialize(GraphicsDevice);
            DesktopPresentation.Bind(GraphicsDevice);
            double drawNow = _clock.Elapsed.TotalSeconds;
            double drawDelta = _lastDrawTime < 0 ? FixedSimulationClock.StepSeconds : Math.Clamp(drawNow - _lastDrawTime, 0, 0.25);
            _lastDrawTime = drawNow;
            Vector2 originalScroll = scroll, originalGlobal = projectDish.Globals.gScroll;
            float originalFrameTime = projectDish.Globals.frameTime;
            DesktopRenderContext.Begin(_simulation.Ticks);
            bool interpolate = _haveSnapshot && gameMode == 0 && !projectDish.Globals.paused &&
                DesktopInput.Settings.SmoothMotion && DesktopInput.Settings.FrameLimit > 60;
            try
            {
                projectDish.Globals.frameTime = (float)drawDelta;
                if (interpolate)
                {
                    Array.Clear(_changed);
                    float alpha = (float)_simulation.Alpha;
                    for (int i = 0; i < character.Length && i < _snapshotCharacters.Length; i++)
                    {
                        var c = character[i]; _changed[i] = false;
                        if (c == null || c != _snapshotCharacters[i] || !c.exists) continue;
                        _drawPositions[i] = c.PortVisualLocation;
                        if (Vector2.DistanceSquared(_beforePositions[i], _drawPositions[i]) > 256 * 256) continue;
                        c.PortVisualLocation = Vector2.Lerp(_beforePositions[i], _drawPositions[i], alpha); _changed[i] = true;
                    }
                    if (Vector2.DistanceSquared(_beforeScroll, scroll) < 256 * 256) scroll = Vector2.Lerp(_beforeScroll, scroll, alpha);
                    if (Vector2.DistanceSquared(_beforeGlobalScroll, projectDish.Globals.gScroll) < 256 * 256)
                        projectDish.Globals.gScroll = Vector2.Lerp(_beforeGlobalScroll, projectDish.Globals.gScroll, alpha);
                }
                base.Draw(new GameTime(TimeSpan.FromSeconds(_simulation.Ticks * FixedSimulationClock.StepSeconds), TimeSpan.FromSeconds(drawDelta)));
            }
            finally
            {
                if (interpolate)
                    for (int i = 0; i < _snapshotCharacters.Length; i++) if (_changed[i]) _snapshotCharacters[i].PortVisualLocation = _drawPositions[i];
                if (interpolate) { scroll = originalScroll; projectDish.Globals.gScroll = originalGlobal; }
                projectDish.Globals.frameTime = originalFrameTime; DesktopRenderContext.End();
            }
            DesktopPresentation.Present(GraphicsDevice);
            if (DesktopInput.Settings.ShowFps) DesktopFpsOverlay.Draw(GraphicsDevice, _shownFps, _shownSimulationHz);
            _fpsFrames++;
            double now = _clock.Elapsed.TotalSeconds;
            if (now - _fpsStart >= 1)
            {
                double fps = _fpsFrames / (now - _fpsStart);
                double simulationHz = (_simulation.Ticks - _lastStatsTicks) / (now - _fpsStart);
                _shownFps = fps; _shownSimulationHz = simulationHz;
                if (++_statsSeconds % 5 == 0) Log.Info("[timing] rendered=" + fps.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " FPS; simulation=" + simulationHz.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " Hz");
                _lastStatsTicks = _simulation.Ticks;
                _fpsStart = now; _fpsFrames = 0;
            }
            int capture = (int)_clock.Elapsed.TotalSeconds / 5;
            if (SmokeSeconds <= 0 && capture > 6) return;
            if (CaptureDirectory == null || capture == _lastCapture) return;
            _lastCapture = capture;
            var p = GraphicsDevice.PresentationParameters;
            var rgba = new Color[p.BackBufferWidth * p.BackBufferHeight];
            GraphicsDevice.GetBackBufferData(rgba);
            using var texture = new Texture2D(GraphicsDevice, p.BackBufferWidth, p.BackBufferHeight);
            texture.SetData(rgba);
            string file = Path.Combine(CaptureDirectory, "frame-" + (capture * 5).ToString("D3") + ".png");
            using var stream = File.Create(file);
            texture.SaveAsPng(stream, texture.Width, texture.Height);
            Log.Info("[capture] " + file);
        }
        private void CapturePreviousState()
        {
            _haveSnapshot = gameMode == 0 && character != null;
            if (!_haveSnapshot) return;
            if (_snapshotCharacters.Length != character.Length)
            {
                _snapshotCharacters = new projectDish.Character[character.Length];
                _beforePositions = new Vector2[character.Length]; _drawPositions = new Vector2[character.Length]; _changed = new bool[character.Length];
            }
            for (int i = 0; i < character.Length; i++)
            { _snapshotCharacters[i] = character[i]; _beforePositions[i] = character[i]?.PortVisualLocation ?? Vector2.Zero; _changed[i] = false; }
            _beforeScroll = scroll; _beforeGlobalScroll = projectDish.Globals.gScroll;
        }
    }
    internal static class DesktopProfile
    {
        public static void Initialize()
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(projectDish.Save));
            AtomicSave.Recover(Path.Combine(DesktopPaths.Saves, "profile.sav"), serializer);
            AtomicSave.Recover(Path.Combine(DesktopPaths.Saves, "settings.sav"), serializer);
            projectDish.Globals.mainPlayerIndex = 0;
            projectDish.Globals.preGamerTag = "Player";
            projectDish.Globals.player.device = new Microsoft.Xna.Framework.Storage.StorageDevice();
            projectDish.Globals.hasDevice = true;
            projectDish.Globals.deviceFailed = false;
            projectDish.Globals.deviceLoadFrame = 10f;
            int result = projectDish.Globals.player.Read(coop: false, overwrite: false);
            if (result == 2 || result == 4)
                throw new InvalidDataException("Corrupt save. Preserve/recover profile.sav at " + DesktopPaths.Saves);
            projectDish.Globals.settings.Read(projectDish.Globals.player);
            projectDish.Globals.initialRead = true;
            if (result == 3)
            {
                projectDish.Globals.player.Write();
                projectDish.Globals.settings.Write(projectDish.Globals.player);
            }
            Log.Info("[profile] Local Player profile active; read=" + result);
        }
    }
}
#endif
