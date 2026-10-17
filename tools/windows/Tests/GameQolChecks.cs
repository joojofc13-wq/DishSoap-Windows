using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;

internal static class GameQolChecks
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static void Check(bool ok, string message)
    { if (!ok) throw new Exception("FAIL: " + message); Console.WriteLine("PASS: " + message); }
    public static void Run(string file)
    {
        var assembly = Assembly.LoadFrom(Path.GetFullPath(file));
        var mapType = assembly.GetType("projectDish.Map"); var charType = assembly.GetType("projectDish.Character");
        var globals = assembly.GetType("projectDish.Globals"); var input = assembly.GetType("Dishwasher.DesktopInput");
        var settings = Activator.CreateInstance(assembly.GetType("Dishwasher.DesktopSettings"));
        input.GetProperty("Settings").SetValue(null, settings);
        void Quick(bool value) => settings.GetType().GetProperty("QuickRoomExits").SetValue(settings, value);
        void Set(object o, string name, object value) => o.GetType().GetField(name, All).SetValue(o, value);
        object Map()
        {
            var map = RuntimeHelpers.GetUninitializedObject(mapType);
            Set(map, "xSize", 10); Set(map, "ySize", 10); Set(map, "exit", Enumerable.Repeat("", 8).ToArray());
            Set(map, "transGoal", ""); Set(map, "col", new int[10, 10]); return map;
        }
        object Player(Vector2 loc, bool left, bool right)
        {
            var c = Activator.CreateInstance(charType); Set(c, "exists", true); Set(c, "team", 0);
            Set(c, "keyLeft", left); Set(c, "keyRight", right); Set(c, "miniGrabbing", -1);
            charType.GetMethod("setLoc").Invoke(c, new object[] { loc }); return c;
        }
        void Exit(object map, object c)
        {
            var array = Array.CreateInstance(charType, 1); array.SetValue(c, 0);
            mapType.GetMethod("PortCheckRoomExits", All).Invoke(map, new object[] { array });
        }
        string Goal(object map) => (string)mapType.GetField("transGoal", All).GetValue(map);
        void Door(object map, int direction) => ((string[])mapType.GetField("exit", All).GetValue(map))[direction] = "test-room";
        globals.GetField("frameTime").SetValue(null, 1f / 60);
        globals.GetField("gameType").SetValue(null, 0); globals.GetField("grabFrame").SetValue(null, 0f);
        globals.GetField("overlay").SetValue(null, Activator.CreateInstance(assembly.GetType("projectDish.Overlay"), true));
        Quick(true);
        foreach (bool left in new[] { true, false })
        {
            var map = Map(); Door(map, left ? 0 : 2);
            var c = Player(new Vector2(left ? 80 : 1200, 600), left, !left); Exit(map, c);
            Check(Goal(map) == "test-room" && (float)mapType.GetField("transFrame", All).GetValue(map) == 10.5f,
                "real " + (left ? "left" : "right") + " exit opens on the first input tick and keeps the normal transition");
        }
        var idleMap = Map(); Door(idleMap, 2); Exit(idleMap, Player(new Vector2(1200, 600), false, false));
        Check(Goal(idleMap) == "", "standing near an exit without directional input does not leave the room");
        var missing = Map(); Exit(missing, Player(new Vector2(1200, 600), false, true));
        Check(Goal(missing) == "", "quick exits do not create an exit in a blocked room");
        var wall = Map(); Door(wall, 2); Set(wall, "shortRightExit", true);
        ((int[,])mapType.GetField("col", All).GetValue(wall))[9, 4] = 1;
        Exit(wall, Player(new Vector2(1130, 600), false, true));
        Check(Goal(wall) == "", "quick exits preserve wall collision checks");
        var busy = Map(); Door(busy, 2); Set(busy, "endFrame", 1f); Exit(busy, Player(new Vector2(1200, 600), false, true));
        Check(Goal(busy) == "", "level completion blocks side exits as before");
        var npcMap = Map(); Door(npcMap, 2); var npc = Player(new Vector2(1200, 600), false, true); Set(npc, "iNPC", true); Exit(npcMap, npc);
        Check(Goal(npcMap) == "", "NPCs cannot trigger room exits");
        var transition = Map(); Door(transition, 2); Set(transition, "transFrame", 10.4f); Exit(transition, Player(new Vector2(1200, 600), false, true));
        Check(Goal(transition) == "", "an active transition cannot be retriggered");
        var grabbingMap = Map(); Door(grabbingMap, 2); var grabbing = Player(new Vector2(1200, 600), false, true); Set(grabbing, "miniGrabbing", 1); Exit(grabbingMap, grabbing);
        Check(Goal(grabbingMap) == "", "grabbing restrictions still block room exits");
        Quick(false); var heldMap = Map(); Door(heldMap, 2); var held = Player(new Vector2(1200, 600), false, true);
        Exit(heldMap, held); Check(Goal(heldMap) == "", "disabling quick exits restores the first-tick delay");
        for (int i = 0; i < 60 && Goal(heldMap) == ""; i++) Exit(heldMap, held);
        Check(Goal(heldMap) == "test-room", "original hold eventually transitions after sustained input");
        foreach (bool top in new[] { true, false })
        {
            var vertical = Map(); Door(vertical, top ? 4 : 6); var c = Player(new Vector2(600, top ? 80 : 1200), false, false);
            charType.GetMethod("setTraj").Invoke(c, new object[] { new Vector2(0, top ? -100 : 100) }); Exit(vertical, c);
            Check(Goal(vertical) == "test-room", "vertical exits retain their original trajectory rules");
        }
    }
}
