using System;
using System.IO;
using System.Text.Json;

namespace Dishwasher
{
    public sealed class DesktopCheatSettings
    {
        public bool Enabled { get; set; }
        public bool GodMode { get; set; }
        public bool InfiniteAmmo { get; set; }
        public bool InfiniteMagic { get; set; }
        public bool InfiniteEnergy { get; set; }
        public int AmmoValue { get; set; } = 50;
        public int MagicValue { get; set; } = 3;
        public double PlayerDamageTaken { get; set; } = 1;
        public double EnemyDamageTaken { get; set; } = 1;
        public double MovementSpeed { get; set; } = 1;
        public int OrbMultiplier { get; set; } = 1;

        public static string FilePath => Path.Combine(DesktopPaths.Config, "cheats.json");
        public void Validate()
        {
            if (AmmoValue < 1 || AmmoValue > 999 || MagicValue < 1 || MagicValue > 9 ||
                !InRange(PlayerDamageTaken, 0, 10) || !InRange(EnemyDamageTaken, .1, 20) ||
                !InRange(MovementSpeed, .5, 3) || OrbMultiplier < 1 || OrbMultiplier > 100)
                throw new InvalidDataException("Cheat values are out of range.");
        }
        private static bool InRange(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
        public static DesktopCheatSettings Load()
        {
            if (!File.Exists(FilePath)) return new DesktopCheatSettings();
            try
            {
                var settings = JsonSerializer.Deserialize<DesktopCheatSettings>(File.ReadAllText(FilePath))
                    ?? throw new InvalidDataException("Empty cheat settings.");
                settings.Validate(); return settings;
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidDataException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Exception("Cheat settings could not be loaded; cheats disabled. Original file preserved.", ex);
                return new DesktopCheatSettings();
            }
        }
        public void Save()
        {
            Validate();
            DesktopPaths.AtomicWrite(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
