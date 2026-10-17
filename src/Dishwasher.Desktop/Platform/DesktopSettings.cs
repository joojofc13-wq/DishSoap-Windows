using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace Dishwasher
{
    public sealed class DesktopSettings
    {
        public bool VSync { get; set; } = false;
        public bool Fullscreen { get; set; } = true;
        public bool ShowFps { get; set; } = true;
        public bool NativeResolution { get; set; } = false;
        public int FrameLimit { get; set; } = 180;
        public bool SmoothMotion { get; set; } = true;
        public bool DiscordRichPresence { get; set; } = true;
        public bool LocalCoop { get; set; } = false;
        public string TextureQuality { get; set; } = "Enhanced";
        public string GameLanguage { get; set; } = "en";
        public float Volume { get; set; } = 1;
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public Dictionary<string, string> Keys { get; set; } = new Dictionary<string, string>
        {
            ["A"] = "J", ["X"] = "K", ["B"] = "L", ["Y"] = "I",
            ["LeftShoulder"] = "Q", ["RightShoulder"] = "E",
            ["LeftTrigger"] = "U", ["RightTrigger"] = "O"
        };
        private static string FilePath => Path.Combine(DesktopPaths.Config, "desktop.json");
        public static DesktopSettings Load()
        {
            if (!File.Exists(FilePath)) return new DesktopSettings();
            try
            {
                var settings = JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(FilePath))
                    ?? throw new InvalidDataException("Empty settings.");
                if (settings.Width < 320 || settings.Width > 16384 || settings.Height < 180 || settings.Height > 16384)
                    throw new InvalidDataException("Resolution out of bounds.");
                if (settings.Keys == null) throw new InvalidDataException("Missing key mapping.");
                if (settings.FrameLimit < 30 || settings.FrameLimit > 360 || settings.Volume < 0 || settings.Volume > 1 ||
                    settings.TextureQuality is not ("Original" or "HD" or "Enhanced")) throw new InvalidDataException("Invalid desktop options.");
                foreach (var key in settings.Keys.Values)
                    if (!Enum.TryParse<Keys>(key, true, out _)) throw new InvalidDataException("Invalid key: " + key);
                // Migrate prior monitor/custom resolutions to fixed 720p output.
                settings.NativeResolution = false; settings.Width = 1280; settings.Height = 720;
                settings.GameLanguage=DesktopGameLanguage.Normalize(settings.GameLanguage);
                return settings;
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidDataException)
            {
                string backup = FilePath + ".corrupt-" + DateTime.UtcNow.Ticks;
                File.Copy(FilePath, backup);
                Log.Exception("Invalid settings; backup preserved at " + backup, ex);
                return new DesktopSettings();
            }
        }
        public void Save() => DesktopPaths.AtomicWrite(FilePath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        public static void SaveFullscreen(bool fullscreen)
        {
            var current=Load();current.Fullscreen=fullscreen;current.Save();
        }
    }
}
