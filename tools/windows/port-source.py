"""Transform a local ILSpy 9.1 tree into a separate desktop-only GameSource.
Never modifies the input. No proprietary source is included in this script.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re


def arguments(text):
    result, begin, depth = [], 0, 0
    for i, char in enumerate(text):
        if char in '([{': depth += 1
        elif char in ')]}': depth -= 1
        elif char == ',' and depth == 0:
            result.append(text[begin:i].strip()); begin = i + 1
    result.append(text[begin:].strip())
    return result


def calls(text, pattern, transform):
    for match in reversed(list(re.finditer(pattern, text))):
        start = match.end(); pos = start; depth = 1
        while depth and pos < len(text):
            if text[pos] == '(': depth += 1
            elif text[pos] == ')': depth -= 1
            pos += 1
        old = text[start:pos - 1]
        new = transform(arguments(old))
        if new is not None: text = text[:start] + new + text[pos - 1:]
    return text


def port(text):
    def begin(args):
        if not args or not args[0].startswith('SpriteBlendMode.'): return None
        blend = {'AlphaBlend': 'NonPremultiplied', 'Additive': 'Additive', 'None': 'Opaque'}
        mode = args[0].split('.')[-1]
        if mode not in blend: raise ValueError('Unknown blend mode: ' + mode)
        sort = args[1] if len(args) > 1 else 'SpriteSortMode.Deferred'
        if len(args) > 3: raise ValueError('Review SpriteBatch transform overload manually')
        return sort + ', BlendState.' + blend[mode]
    text = calls(text, r'\b\w+\.Begin\(', begin)
    def target(args):
        if len(args) not in (5, 7): raise ValueError('Unknown RenderTarget2D constructor')
        return ', '.join(args[:3] + ['false', args[4], 'DepthFormat.None', '0', 'RenderTargetUsage.PreserveContents'])
    text = calls(text, r'new RenderTarget2D\(', target)
    text = re.sub(r'\.SetRenderTarget\(0,\s*', '.SetRenderTarget(', text)
    text = text.replace('.GetTexture()', '')
    text = text.replace('new RenderTarget2D(', 'Dishwasher.DesktopHdTextures.CreateRenderTarget(')
    text = re.sub(r'\bSpriteBatch\b', 'Dishwasher.DesktopSpriteBatch', text)
    # These are the only imported-texture dimension reads in the referenced
    # source. Preserve layout, centering and comic panel sizes after upscaling.
    text = re.sub(r'\b(imgTex\[img\]|panelTex\[panel\]|xblaTex|skaLogoTex|releaseTex)\.(Width|Height)\b',
                  lambda m: 'Dishwasher.DesktopHdTextures.' + m[2] + '(' + m[1] + ')', text)
    text = text.replace('fontTex.MeasureString(', 'Dishwasher.DesktopHdTextures.MeasureString(fontTex, ')
    text = re.sub(r'^.*Thread\.CurrentThread\.SetProcessorAffinity\([^\n]+\n', '', text, flags=re.M)
    text = re.sub(r'^.*FullScreenRefreshRateInHz\s*=.*\n', '', text, flags=re.M)
    text = re.sub(r'^.*Components.Add\(new GamerServicesComponent.*\n', '', text, flags=re.M)
    text = text.replace('.CreationParameters.Adapter', '.Adapter')
    text = text.replace('LoadGraphicsContent(bool loadAllContent)', 'LoadContent()')
    text = text.replace('UnloadGraphicsContent(bool unloadAllContent)', 'UnloadContent()')
    text = text.replace('if (unloadAllContent)', 'if (true) // PORT: UnloadContent always unloads')
    text = text.replace('new Color(0, 0, 0, color.A)', 'new Color(0, 0, 0, (int)color.A)')
    text = text.replace('BlendState.AlphaBlend', 'BlendState.NonPremultiplied')
    text = text.replace('using Microsoft.Xna.Framework.Audio;', 'using Dishwasher.Audio;')
    text = text.replace('GamePad.GetState(', 'Dishwasher.DesktopInput.GetState(')
    text = text.replace('GamePad.GetCapabilities(', 'Dishwasher.DesktopInput.GetCapabilities(')
    text = text.replace('GamePad.SetVibration(', 'Dishwasher.DesktopInput.SetVibration(')
    text = re.sub(r'([\w.]+)\.SetRenderTarget\((?:\(RenderTarget2D\))?null\)', r'Dishwasher.DesktopPresentation.Bind(\1)', text)
    text = re.sub(r'([\w.]+)\.Abort\(\);', r'Dishwasher.DesktopThreads.RequestStop(\1);', text)
    text = text.replace('while (!loaded)', 'while (!loaded && !Dishwasher.DesktopThreads.StopRequested)')
    text = re.sub(r'new Thread\((\w+)\);', r'new Thread(\1) { IsBackground = true };', text)
    return text


def integrate(relative, text):
    # Hook health-loss operations, not HP assignments used by initialization,
    # weapon changes, healing or scripted cleanup. Fail if the owned revision
    # has different call sites rather than silently shipping incomplete cheats.
    damage_counts = {'projectDish/Character.cs': 8, 'projectDish/Game1.cs': 3, 'projectDish/SpriteManager.cs': 2}
    if relative.as_posix() in damage_counts:
        text, subtracts = re.subn(r'^(\s*)(?:(\w+\[[^\n;]+?\])\.)?HP -= ([^;\n]+);',
            lambda m: m[1] + (m[2] + '.' if m[2] else '') + 'PortTakeDamage(' + m[3] + ');', text, flags=re.M)
        text, decrements = re.subn(r'^(\s*)HP--;', r'\1PortTakeDamage(1);', text, flags=re.M)
        if subtracts + decrements != damage_counts[relative.as_posix()]:
            raise ValueError('Review cheat damage hooks: ' + relative.as_posix())
    if relative.as_posix() in ('projectDish/Game1.cs', 'projectDish/SpriteManager.cs'):
        text, additions = re.subn(r'Globals\.player\.expOrbs \+= ([^;\n]+);', r'Globals.player.PortAddOrbs(\1);', text)
        text, increments = re.subn(r'Globals\.player\.expOrbs\+\+;', 'Globals.player.PortAddOrbs(1);', text)
        expected = 1 if relative.as_posix() == 'projectDish/Game1.cs' else 2
        if additions + increments != expected: raise ValueError('Review earned-orb call sites: ' + relative.as_posix())
    if relative.as_posix() == 'projectDish/GuitarSolo.cs':
        marker = 'if (needsStart)\n\t\t{'
        if text.count(marker) != 1:
            raise ValueError('Review guitar audio readiness hook')
        text = text.replace(marker, marker + '\n' +
            '\t\t\t// PORT: prepare both stems before starting the rhythm clock.\n' +
            '\t\t\tbool portForegroundReady = fgCue.Prepare();\n' +
            '\t\t\tbool portBackgroundReady = bgCue.Prepare();\n' +
            '\t\t\tif (!portForegroundReady || !portBackgroundReady) return;')
    if relative.as_posix() == 'projectDish/Globals.cs':
        text = text.replace('random.NextDouble()', 'Dishwasher.DesktopRenderContext.RandomFor(random).NextDouble()')
        text = text.replace('random.Next(', 'Dishwasher.DesktopRenderContext.RandomFor(random).Next(')
        marker = 'public static StorageContainer GetContainer(Player _player)'
        start = text.index('{', text.index(marker)); end = start + 1; depth = 1
        while depth:
            if text[end] == '{': depth += 1
            elif text[end] == '}': depth -= 1
            end += 1
        body = '''{
        // PORT: local shim opens synchronously; no Xbox device timeout/thread abort.
        if (trial || _player?.device == null) return null;
        if (!hasStorageContainer || globalContainer == null || globalContainer.IsDisposed)
        {
            globalContainer = _player.device.OpenContainer("TheDishwasher", false, out _);
            hasStorageContainer = true;
        }
        return globalContainer;
    }'''
        text = text[:start] + body + text[end:]
    if relative.as_posix() == 'projectDish/ThreadLoader.cs':
        text = text.replace('i < loadList.Length;', 'i < loadList.Length && !Dishwasher.DesktopThreads.StopRequested;')
    if relative.as_posix() in ('projectDish/Player.cs', 'projectDish/Settings.cs'):
        if relative.as_posix() == 'projectDish/Player.cs':
            marker = 'public int expOrbs;'
            if text.count(marker) != 1: raise ValueError('Review earned-orb hook')
            text = text.replace(marker, marker + '\n\tpublic void PortAddOrbs(int earned) => expOrbs = Dishwasher.DesktopCheats.EarnedOrbs(expOrbs, earned);')
        text = re.sub(r'FileStream fileStream = File.Open\(path, FileMode.Create\);\s*'
                      r'(XmlSerializer xmlSerializer = new XmlSerializer\(typeof\(Save\)\);)\s*'
                      r'xmlSerializer.Serialize\(fileStream, save\);\s*fileStream.Close\(\);',
                      r'\1\n\t\t\tDishwasher.AtomicSave.Write(path, xmlSerializer, save); // PORT: atomic, same XML', text)
        text = re.sub(r'catch \(Exception\)\n(\s*)\{', r'catch (Exception portError)\n\1{\n\1\tDishwasher.Log.Exception("save/read", portError);', text)
    if relative.as_posix() == 'projectDish/Game1.cs':
        text = text.replace('private Character[] character;', 'protected Character[] character;')
        text = text.replace('private Vector2 scroll;', 'protected Vector2 scroll;')
        text = text.replace('Globals.needsRender--;', 'if (Dishwasher.DesktopRenderContext.NewSimulationFrame) Globals.needsRender--;')
        text = text.replace('if (camSplatCatchup <= 0)', 'if (camSplatCatchup <= 0 && Dishwasher.DesktopRenderContext.NewSimulationFrame)')
        text = text.replace('camSplatCatchup--;', 'if (Dishwasher.DesktopRenderContext.NewSimulationFrame) camSplatCatchup--;')
        scene_draw = 'map.Draw(sprite, ref mapTex, ref mapbTex, scroll, 0, spriteTex, zoom);\n\t\t\t\tsprite.End();'
        if text.count(scene_draw) != 1:
            raise ValueError('Review background filtering hook: expected one real background pass')
        text = text.replace(scene_draw, scene_draw + '\n\t\t\t\tDishwasher.DesktopSceneFilter.Apply(graphics.GraphicsDevice, rTarg[curRTarg]); // PORT: fixed edge smoothing')
        text = text.replace('private void TraceWriteLine(string s)\n\t{\n\t}',
                            'private void TraceWriteLine(string s)\n\t{\n\t\tDishwasher.Log.Info("[game] " + s);\n\t}')
        text = text.replace('TraceWriteLine("Loading Graphics content");',
            'TraceWriteLine("Loading Graphics content");\n' +
            '\t\tbase.Content = new Dishwasher.Content.DishwasherContentManager(Services, graphics.GraphicsDevice, "."); // PORT: straight-alpha')
        # Original loader catch was hidden by the console Guide service.
        text = re.sub(r'catch \(Exception (\w+)\)\n\s*\{',
                      lambda m: m.group(0) + '\n\t\tDishwasher.Log.Exception("Game1 phase=" + loadPhase, ' + m[1] + ');', text)
    if relative.as_posix() == 'projectDish/Character.cs':
        for field, backing, option, value in (
            ('ammo', '_portAmmo', 'InfiniteAmmo', 'AmmoValue'),
            ('skulls', '_portSkulls', 'InfiniteMagic', 'MagicValue'),
            ('MP', '_portEnergy', 'InfiniteEnergy', None)):
            marker = 'public int ' + field + ';'
            if text.count(marker) != 1: raise ValueError('Review cheat resource hook: ' + field)
            target = 'Math.Max(1, mMP)' if value is None else 'Dishwasher.DesktopCheats.Current.' + value
            condition = 'PortCheatPlayer && Dishwasher.DesktopCheats.Current.' + option
            prop = ('private int ' + backing + ';\n\tpublic int ' + field + '\n\t{\n' +
                    '\t\tget => ' + condition + ' ? ' + target + ' : ' + backing + ';\n' +
                    '\t\tset { if (!(' + condition + ')) ' + backing + ' = value; }\n\t}')
            text = text.replace(marker, prop)
        marker = 'private float speed;'
        if text.count(marker) != 1: raise ValueError('Review movement speed hook')
        text = text.replace(marker, '''private float _portSpeed;
    private float speed
    {
        get => _portSpeed * (PortCheatPlayer ? (float)Dishwasher.DesktopCheats.Current.MovementSpeed : 1f);
        set => _portSpeed = value;
    }
    private bool _portCheatsReady;
    private double _portDamageRemainder;
    private bool PortCheatPlayer => _portCheatsReady && exists && team == 0 && !iNPC && Dishwasher.DesktopCheats.Active;
    public void PortTakeDamage(int amount)
    {
        int damage = Dishwasher.DesktopCheats.Damage(amount,
            _portCheatsReady && exists && team == 0 && !iNPC,
            _portCheatsReady && exists && team == 1 && !iNPC, ref _portDamageRemainder);
        HP = (int)Math.Clamp((long)HP - damage, int.MinValue, int.MaxValue);
    }''')
        marker = 'public void Init(Vector2 newLoc, int newDef, int newID, int newAI, int newTeam, string newTag, Map map)'
        if text.count(marker) != 1: raise ValueError('Review cheat initialization boundary')
        start = text.index('{', text.index(marker)); end = start + 1; depth = 1
        while depth:
            if text[end] == '{': depth += 1
            elif text[end] == '}': depth -= 1
            end += 1
        text = (text[:start + 1] + '\n\t\t_portCheatsReady = false; _portDamageRemainder = 0;\n\t\ttry {\n' +
                text[start + 1:end - 1] + '\n\t\t} finally { _portCheatsReady = true; }\n\t' + text[end - 1:])
        marker = '\tpublic Vector2 getLoc()\n\t{'
        if text.count(marker) != 1: raise ValueError('Review character interpolation hook')
        text = text.replace(marker, '\tinternal Vector2 PortVisualLocation { get => loc; set => loc = value; }\n\n' + marker)
    if relative.as_posix() == 'projectDish/GameRender.cs':
        binding = 'graphics.GraphicsDevice.Textures[1] = rTarg;'
        if binding in text:
            text = text.replace('\t\t' + binding + '\n', '')
            text = text.replace('effectPass.Begin();', 'effectPass.Begin();\n\t\t' + binding + ' // PORT: bind after Apply')
    return text


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    args = parser.parse_args()
    if args.destination.exists(): raise SystemExit('Destination exists; preserve/review it before regenerating.')
    if not (args.source / 'projectDish/Game1.cs').is_file(): raise SystemExit('Missing real projectDish/Game1.cs')
    changes = []
    for source in args.source.rglob('*.cs'):
        rel = source.relative_to(args.source)
        if source.name in ('Program.cs', 'AssemblyInfo.cs'): continue
        original = source.read_text(encoding='utf-8-sig')
        output = integrate(rel, port(original))
        dest = args.destination / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text(output, encoding='utf-8')
        changes.append({'path': rel.as_posix(), 'input_sha256': hashlib.sha256(original.encode()).hexdigest(),
                        'modified': original != output})
    (args.destination / 'port-report.json').write_text(json.dumps(changes, indent=2))
    print(f'Prepared {len(changes)} files. Compile and review remaining API/behaviour differences.')


if __name__ == '__main__': main()
