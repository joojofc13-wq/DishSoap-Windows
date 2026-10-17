using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Dishwasher;
using Dishwasher.Audio;
using Microsoft.Xna.Framework.Audio;
using Cue = Dishwasher.Audio.Cue;
using AudioEngine = Dishwasher.Audio.AudioEngine;
using SoundBank = Dishwasher.Audio.SoundBank;

if (args.Length != 2) throw new ArgumentException("Provide an imported content root and isolated diagnostic data root.");
DesktopPaths.Initialize(args[1]); Log.Initialize(); PublicBuild.ContentRoot = Path.GetFullPath(args[0]);
static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
SoundEffect.Initialize();
using var engine = new AudioEngine("sfx/sfxproj.xgs");
using var bank = new SoundBank(engine, "sfx/solo_dish.xsb");
using var foreground = bank.GetCue("solo_dish_fg");
using var background = bank.GetCue("solo_dish_bg");
bool firstFg = foreground.Prepare(), firstBg = background.Prepare();
Check(!firstFg && !firstBg, "cold guitar stems are pending before countdown");
var timeout = Stopwatch.StartNew();
while (true)
{
    engine.Update();
    bool fg = foreground.Prepare(), bg = background.Prepare();
    if (fg && bg) break;
    if (timeout.Elapsed.TotalSeconds > 15) throw new TimeoutException("Guitar stems did not finish decoding.");
    Thread.Sleep(10);
}
Check(!foreground.IsPreparing && !background.IsPreparing, "both guitar stems finish preparation without starting playback");
Check(!foreground.IsPlaying && !background.IsPlaying, "preparation does not start the song early");
using var map = JsonDocument.Parse(File.ReadAllText(Path.Combine(PublicBuild.ContentRoot, "sfx/cue_map.json")));
string extra = map.RootElement.GetProperty("cues").EnumerateObject()
    .Where(p => p.Value.GetProperty("category_id").GetInt32() == 2)
    .SelectMany(p => p.Value.GetProperty("files").EnumerateArray())
    .Select(f => f.GetProperty("file").GetString()).First(f => f.EndsWith(".ogg"));
timeout.Restart();
while (SoundLoader.Get("sfx/", extra) == null)
{
    engine.Update(); Thread.Sleep(10);
    if (timeout.Elapsed.TotalSeconds > 15) throw new TimeoutException("Third audio track did not prepare.");
}
Check(foreground.Prepare() && background.Prepare(), "prepared guitar buffers survive music-cache pressure");
foreground.Stop(Dishwasher.Audio.AudioStopOptions.Immediate);
background.Stop(Dishwasher.Audio.AudioStopOptions.Immediate);
var pins = (System.Collections.IDictionary)typeof(SoundLoader).GetField("_playing", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
Check(pins.Count == 0, "cancelling before playback releases prepared buffers");
Check(foreground.Prepare() && background.Prepare(), "prepared stems can be reused immediately");
engine.GetCategory("solo_fg").SetVolume(.1f);
foreground.Play(); background.Play();
Check(foreground.IsPlaying && background.IsPlaying, "one Play call starts both guitar stems after preparation");
var instanceField = typeof(Cue).GetField("_instance", BindingFlags.Instance | BindingFlags.NonPublic);
var fgInstance = (SoundEffectInstance)instanceField.GetValue(foreground);
var bgInstance = (SoundEffectInstance)instanceField.GetValue(background);
float backingVolume = bgInstance.Volume;
Check(Math.Abs(fgInstance.Volume - .1f) < .001f, "missed-note foreground attenuation is applied");
engine.GetCategory("solo_fg").SetVolume(1f);
Check(Math.Abs(fgInstance.Volume - 1f) < .001f && bgInstance.Volume == backingVolume,
    "correct-note volume restores the guitar without changing the backing track");
var definition = map.RootElement.GetProperty("cues").GetProperty("solo_dish_fg");
string audioFile = definition.GetProperty("files")[0].GetProperty("file").GetString();
using (var stream = File.OpenRead(Path.Combine(PublicBuild.ContentRoot, "sfx", audioFile)))
using (var reader = new NVorbis.VorbisReader(stream, false))
{
    var samples = new float[65536]; float peak = 0; int count;
    while ((count = reader.ReadSamples(samples, 0, samples.Length)) > 0 && peak < .001f)
        for (int i = 0; i < count; i++) peak = Math.Max(peak, Math.Abs(samples[i]));
    Check(peak >= .001f, "imported foreground contains audible guitar samples");
}
Thread.Sleep(100);
foreground.Stop(Dishwasher.Audio.AudioStopOptions.Immediate); background.Stop(Dishwasher.Audio.AudioStopOptions.Immediate);
Check(pins.Count == 0, "stopping both stems releases playback buffers");
Console.WriteLine("Guitar audio checks passed; gameplay and saves were not modified.");
