#if DISHWASHER_GAME
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Dishwasher
{
    internal static class DesktopBootstrap
    {
        public static int Run()
        {
            if (!DesktopImporter.TryGetActive(out string root)) throw new InvalidOperationException("Import your assets first.");
            PublicBuild.ContentRoot = root;
            System.IO.Directory.SetCurrentDirectory(root);
            var settings = DesktopSettings.Load();
            DesktopGameLanguage.Apply(settings.GameLanguage);
            DesktopHdTextures.Initialize(root, settings.TextureQuality);
            DesktopInput.Settings = settings;
            // Every physical pad may select its own local slot without LIVE.
            var gamers = Microsoft.Xna.Framework.GamerServices.Gamer.SignedInGamers;
            for (int i = 1; i < 4; i++)
                if (gamers[(PlayerIndex)i] == null)
                    gamers.Add(new Microsoft.Xna.Framework.GamerServices.SignedInGamer { PlayerIndex = (PlayerIndex)i, Gamertag = "Player " + (i + 1) });
            using var game = new DesktopGame();
            using var presence = new DesktopDiscordPresence(settings.DiscordRichPresence);
            game.DiscordPresence = presence;
            settings.NativeResolution = false; settings.Width = 1280; settings.Height = 720;
            settings.Save();
            var manager = game.Services.GetService(typeof(IGraphicsDeviceManager)) as GraphicsDeviceManager
                ?? throw new InvalidOperationException("Game1 did not register GraphicsDeviceManager.");
            manager.PreferredBackBufferWidth = 1280;
            manager.PreferredBackBufferHeight = 720;
            manager.SynchronizeWithVerticalRetrace = settings.VSync;
            manager.HardwareModeSwitch = false;
            manager.IsFullScreen = settings.Fullscreen;
            game.Window.AllowUserResizing = false;
            game.Window.Title = "dishwasher";
            // Original simulation is stepped explicitly at 60 Hz by DesktopGame;
            // the outer loop renders independently at the selected frame limit.
            game.IsFixedTimeStep = false;
            game.InactiveSleepTime = TimeSpan.FromMilliseconds(20);
            Microsoft.Xna.Framework.Audio.SoundEffect.MasterVolume = settings.Volume;
            Log.Info("[timing] render limit=" + settings.FrameLimit + "; simulation=60Hz; interpolation=" + settings.SmoothMotion + "; VSync=" + settings.VSync);
            game.Components.Add(new DesktopLifecycle(game, manager, settings));
            PortExit.Game = game;
            game.Deactivated += (_, _) => { DesktopInput.Focused = false; SaveAutosave.Save("focus lost"); };
            game.Activated += (_, _) => DesktopInput.Focused = true;
            game.Exiting += (_, _) => { SaveAutosave.Save("exit"); };
            try { game.Run(); return 0; }
            finally { PortExit.Game = null; DesktopPresentation.Dispose(); DesktopSceneFilter.Dispose(); DesktopFpsOverlay.Dispose(); }
        }
    }
    internal sealed class DesktopLifecycle : GameComponent
    {
        private readonly GraphicsDeviceManager _manager;
        private readonly DesktopSettings _settings;
        private bool _altEnter;
        public DesktopLifecycle(Game game, GraphicsDeviceManager manager, DesktopSettings settings) : base(game)
        { _manager = manager; _settings = settings; }
        public override void Update(GameTime gameTime)
        {
            KeyboardState keys = Keyboard.GetState();
            bool chord = Game.IsActive && keys.IsKeyDown(Keys.Enter) && (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
            if (chord && !_altEnter)
            {
                _manager.ToggleFullScreen();
                _settings.Fullscreen = _manager.IsFullScreen;
                DesktopSettings.SaveFullscreen(_settings.Fullscreen);
            }
            _altEnter = chord;
            base.Update(gameTime);
        }
    }
    public static class PortExit
    {
        internal static Game Game;
        public static void Quit() { SaveAutosave.Save("menu exit"); Game?.Exit(); }
    }
    public static class SaveAutosave
    {
        public static void Save(string reason)
        {
            var player = projectDish.Globals.player;
            if (player == null || player.device == null || projectDish.Globals.trial ||
                projectDish.Globals.deviceFailed || !projectDish.Globals.hasDevice || !projectDish.Globals.initialRead) return;
            try
            {
                // Same routines and guards as the supplied Android SaveAutosave.
                projectDish.Globals.settings?.Write(player);
                player.Write();
                var coop = projectDish.Globals.coopPlayer;
                if (coop?.device != null) coop.Write(coop: true);
                Log.Info("[save] " + reason + " playSeconds=" + player.playSeconds);
            }
            catch (Exception ex) { Log.Exception("save " + reason, ex); throw; }
        }
    }
}
#endif
