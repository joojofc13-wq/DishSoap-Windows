using System.Reflection;

// Exercises the locally compiled, owner-supplied game classes without starting
// gameplay or reading a player's saves. No game source is part of these tests.
internal static class GameCheatChecks
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static void Check(bool ok, string name)
    { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
    public static void Run(string assemblyPath, string root)
    {
        root = Path.GetFullPath(root);
        if (Directory.Exists(root)) throw new IOException("Use a new isolated test folder.");
        var assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        assembly.GetType("Dishwasher.DesktopPaths").GetMethod("Initialize").Invoke(null, new object[] { root });
        var runtime = assembly.GetType("Dishwasher.DesktopCheats");
        var begin = runtime.GetMethod("BeginFrame", All);
        string config = Path.Combine(root, "Config", "cheats.json");
        void Apply(string json, bool playing = true) { File.WriteAllText(config, json); begin.Invoke(null, new object[] { playing, true }); }
        var type = assembly.GetType("projectDish.Character");
        object New(int team)
        {
            object instance = Activator.CreateInstance(type);
            type.GetField("exists", All).SetValue(instance, true); type.GetField("team", All).SetValue(instance, team);
            type.GetField("_portCheatsReady", All).SetValue(instance, true); type.GetField("HP", All).SetValue(instance, 100);
            return instance;
        }
        int Hp(object c) => (int)type.GetField("HP", All).GetValue(c);
        void Hit(object c, int damage) => type.GetMethod("PortTakeDamage").Invoke(c, new object[] { damage });
        Apply("{\"Enabled\":true,\"GodMode\":true,\"EnemyDamageTaken\":3}");
        object player = New(0), enemy = New(1), npc = New(0);
        type.GetField("iNPC", All).SetValue(npc, true);
        Hit(player, 1000); Hit(enemy, 10); Hit(npc, 10);
        Check(Hp(player) == 100 && Hp(enemy) == 70 && Hp(npc) == 90, "real Character damage hooks protect players and exclude NPCs");
        Apply("{\"Enabled\":true,\"PlayerDamageTaken\":0.25}");
        for (int i = 0; i < 4; i++) Hit(player, 1);
        Check(Hp(player) == 99, "real Character accumulates fractional damage");
        Apply("{}"); type.GetProperty("ammo").SetValue(player, 7); type.GetProperty("skulls").SetValue(player, 1);
        type.GetProperty("MP").SetValue(player, 35); type.GetField("mMP", All).SetValue(player, 100);
        type.GetProperty("speed", All).SetValue(player, 200f);
        Apply("{\"Enabled\":true,\"InfiniteAmmo\":true,\"InfiniteMagic\":true,\"InfiniteEnergy\":true,\"AmmoValue\":123,\"MagicValue\":6,\"MovementSpeed\":2}");
        Check((int)type.GetProperty("ammo").GetValue(player) == 123 && (int)type.GetProperty("skulls").GetValue(player) == 6 &&
            (int)type.GetProperty("MP").GetValue(player) == 100, "real resource getters use the configured values");
        type.GetProperty("ammo").SetValue(player, 122); type.GetProperty("skulls").SetValue(player, 5); type.GetProperty("MP").SetValue(player, 60);
        Check((float)type.GetProperty("speed", All).GetValue(player) == 400 && (float)type.GetProperty("speed", All).GetValue(player) == 400, "movement multiplier does not compound");
        Apply("{}");
        Check((int)type.GetProperty("ammo").GetValue(player) == 7 && (int)type.GetProperty("skulls").GetValue(player) == 1 &&
            (int)type.GetProperty("MP").GetValue(player) == 35 && (float)type.GetProperty("speed", All).GetValue(player) == 200, "disabling restores underlying resources and original movement speed");
        Hit(player, 10); Check(Hp(player) == 89, "damage resumes immediately after disabling cheats");
        Apply("{\"Enabled\":true,\"InfiniteAmmo\":true}"); type.GetField("_portCheatsReady", All).SetValue(player, false);
        type.GetProperty("ammo").SetValue(player, 50); Apply("{}");
        Check((int)type.GetProperty("ammo").GetValue(player) == 50, "character initialization bypasses resource locks");
        var playerType = assembly.GetType("projectDish.Player"); var profile = Activator.CreateInstance(playerType);
        Apply("{\"Enabled\":true,\"OrbMultiplier\":5}");
        playerType.GetField("expOrbs").SetValue(profile, 100);
        Check((int)playerType.GetField("expOrbs").GetValue(profile) == 100, "loading existing orbs is unaffected");
        playerType.GetMethod("PortAddOrbs").Invoke(profile, new object[] { 3 });
        Check((int)playerType.GetField("expOrbs").GetValue(profile) == 115, "real Player earned-orb hook multiplies gains");
    }

    public static void Configurator(string assemblyPath, string root)
    {
        root = Path.GetFullPath(root);
        if (Directory.Exists(root)) throw new IOException("Use a new isolated test folder.");
        var assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        assembly.GetType("Dishwasher.DesktopPaths").GetMethod("Initialize").Invoke(null, new object[] { root });
        string original = Path.Combine(root, "Saves", "profile.sav"); File.WriteAllText(original, "untouched profile bytes");
        var type = assembly.GetType("Dishwasher.CheatConfigurationPanel");
        using var panel = (System.Windows.Forms.Control)Activator.CreateInstance(type, true);
        void Toggle(string name, bool value) => ((System.Windows.Forms.CheckBox)type.GetField(name, All).GetValue(panel)).Checked = value;
        void Number(string name, decimal value) => ((System.Windows.Forms.NumericUpDown)type.GetField(name, All).GetValue(panel)).Value = value;
        var save = type.GetMethod("SaveChoices", All);
        Toggle("_enabled", true); Toggle("_ammo", true); Number("_ammoValue", 125); Number("_incoming", .25m);
        save.Invoke(panel, null);
        string backup = (string)type.GetProperty("LastBackup", All).GetValue(panel);
        Check(File.ReadAllText(Path.Combine(backup, "profile.sav")) == "untouched profile bytes", "configurator automatically backs up saves before enabling cheats");
        using var settings = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Config", "cheats.json")));
        Check(settings.RootElement.GetProperty("AmmoValue").GetInt32() == 125 && settings.RootElement.GetProperty("PlayerDamageTaken").GetDouble() == .25, "numeric cheat values roundtrip through actual configurator");
        save.Invoke(panel, null);
        Check(type.GetProperty("LastBackup", All).GetValue(panel) == null, "unchanged cheats do not create duplicate automatic backups");
        Number("_ammoValue", 126);
        bool failed = false;
        using (var locked = new FileStream(original, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            try { save.Invoke(panel, null); } catch (TargetInvocationException ex) when (ex.InnerException is IOException) { failed = true; }
        }
        using var unchanged = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Config", "cheats.json")));
        Check(failed && unchanged.RootElement.GetProperty("AmmoValue").GetInt32() == 125, "failed save backup prevents cheat changes from being applied");
        type.GetMethod("SetValues", All).Invoke(panel, new[] { Activator.CreateInstance(assembly.GetType("Dishwasher.DesktopCheatSettings")) });
        save.Invoke(panel, null);
        using var reset = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Config", "cheats.json")));
        Check(!reset.RootElement.GetProperty("Enabled").GetBoolean() && reset.RootElement.GetProperty("MovementSpeed").GetDouble() == 1, "reset cheats disables modifiers and restores default values");
    }
}
