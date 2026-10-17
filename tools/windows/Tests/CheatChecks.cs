using Dishwasher;

internal static class CheatChecks
{
    private static void Check(bool ok, string name)
    { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
    public static void Run()
    {
        Check(!DesktopCheatSettings.Load().Enabled, "cheats disabled for existing installations");
        var value = new DesktopCheatSettings { Enabled = true, PlayerDamageTaken = .25, EnemyDamageTaken = 3, MovementSpeed = 1.75, OrbMultiplier = 5 };
        value.Save(); DesktopCheats.BeginFrame(true, true);
        double carry = 0;
        Check(Enumerable.Range(0, 4).Sum(_ => DesktopCheats.Damage(1, true, false, ref carry)) == 1, "fractional damage accumulates across small hits");
        Check(DesktopCheats.Damage(10, false, true, ref carry) == 30, "enemy damage multiplier");
        Check(DesktopCheats.Damage(10, false, false, ref carry) == 10, "noncombatants excluded");
        Check(DesktopCheats.EarnedOrbs(12, 4) == 32, "only newly earned orbs multiplied");
        Check(DesktopCheats.EarnedOrbs(int.MaxValue - 1, 500) == int.MaxValue, "orb growth saturates without overflow");
        Check(DesktopCheats.EarnedOrbs(100, -20) == 80, "orb spending is not multiplied");
        value.GodMode = true; value.Save(); DesktopCheats.BeginFrame(true, true);
        Check(DesktopCheats.Damage(1000, true, false, ref carry) == 0, "god mode takes priority over player damage multiplier");
        DesktopCheats.BeginFrame(false);
        Check(DesktopCheats.Damage(10, true, false, ref carry) == 10 && DesktopCheats.EarnedOrbs(1, 2) == 3, "menus do not apply cheats");
        File.WriteAllText(DesktopCheatSettings.FilePath, "{\"Enabled\":true,\"MovementSpeed\":999}");
        DesktopCheats.BeginFrame(true, true);
        Check(!DesktopCheats.Active && File.ReadAllText(DesktopCheatSettings.FilePath).Contains("999"), "invalid values disable cheats and preserve original file");
        File.WriteAllText(DesktopCheatSettings.FilePath, "{broken"); DesktopCheats.BeginFrame(true, true);
        Check(!DesktopCheats.Active && File.ReadAllText(DesktopCheatSettings.FilePath) == "{broken", "malformed cheat settings fail closed");
        new DesktopCheatSettings().Save(); DesktopCheats.BeginFrame(true, true);
        Check(!DesktopCheats.Active, "master switch disables every cheat");
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1, 10.1 })
        {
            bool rejected = false;
            try { new DesktopCheatSettings { PlayerDamageTaken = invalid }.Validate(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, "reject invalid player damage value " + invalid);
        }
        string file = Path.Combine(DesktopPaths.Saves, "cheat-backup-test.sav"); File.WriteAllText(file, "original save bytes");
        string backup = DesktopSaveBackup.Create(); string next = DesktopSaveBackup.Create();
        Check(backup != next && File.ReadAllText(Path.Combine(backup, Path.GetFileName(file))) == "original save bytes" &&
            File.ReadAllText(file) == "original save bytes", "save backups preserve bytes and never overwrite earlier backups");
    }
}
