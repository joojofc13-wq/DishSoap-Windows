"""Build a private 2x texture pack from an imported owner copy. Stdlib only.
Image operations run in TextureTools; neural inference runs in NCNN/Vulkan.
No original/derived textures or downloaded models belong in the source kit.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import time


def dimensions(path):
    with path.open('rb') as f:
        header = f.read(24)
    if header[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError(f'Not a PNG: {path}')
    return struct.unpack('>II', header[16:24])


def run(args, log=None):
    subprocess.run([str(v) for v in args], check=True, stdout=log, stderr=log)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source', required=True, type=Path)
    p.add_argument('--destination', required=True, type=Path)
    p.add_argument('--engine', required=True, type=Path)
    p.add_argument('--texture-tools', required=True, type=Path)
    p.add_argument('--dotnet', required=True, type=Path)
    p.add_argument('--work', required=True, type=Path)
    p.add_argument('--only', help='Optional relative PNG for a sample')
    p.add_argument('--backgrounds', action='store_true', help='Restore the eight scene sheets with x4plus and downsample to 2x')
    a = p.parse_args()
    root = a.source.resolve(); output = a.destination.resolve()
    if root == output or root in output.parents and output.name == 'gfx':
        raise ValueError('Output must be a separate pack directory.')
    a.work.mkdir(parents=True, exist_ok=True); output.mkdir(parents=True, exist_ok=True)
    files = sorted((root / 'gfx').rglob('*.png'))
    if a.backgrounds:
        files = sorted((root / 'gfx/maps').glob('*.png'))
    if a.only:
        files = [root / a.only]
    entries = []; start = time.monotonic()
    for index, source in enumerate(files, 1):
        relative = source.relative_to(root).as_posix(); w, h = dimensions(source)
        output_relative = ('scenes/' if a.backgrounds else '') + relative
        target = output / output_relative; target.parent.mkdir(parents=True, exist_ok=True)
        name = source.stem.lower()
        # Font sheets use predictable interpolation to avoid neural letter changes.
        font = name.endswith('_atlas') or name in {'arial', 'text', 'jtext', 'comictext'}
        fingerprint = hashlib.sha256(source.read_bytes()).hexdigest()
        stamp = target.with_suffix('.upscale.json')
        recipe = 'scene-x4plus-original-chroma-v2' if a.backgrounds else 'hd2x-original-chroma-v2'
        method = 'x4plus-luma90-original-chroma' if a.backgrounds else 'bicubic-font' if font else 'animevideov3-luma80-original-chroma'
        expected = {'source_sha256': fingerprint, 'recipe': recipe, 'method': method}
        cached = target.exists() and stamp.exists() and json.loads(stamp.read_text()) == expected and dimensions(target) == (w * 2, h * 2)
        print(f'[{index}/{len(files)}] {relative} {w}x{h} -> {w*2}x{h*2}' + (' (cache)' if cached else ''), flush=True)
        if not cached:
            temporary = target.with_name(target.stem + '.pending.png')
            if font:
                run([a.dotnet, a.texture_tools, 'resize', source, temporary])
            else:
                rgb = a.work / 'rgb.png'; ai = a.work / 'ai.png'
                run([a.dotnet, a.texture_tools, 'prepare', source, rgb])
                with (a.work / 'ncnn-last.log').open('w') as log:
                    run([a.engine / 'realesrgan-ncnn-vulkan.exe', '-i', rgb, '-o', ai,
                         '-m', a.engine / 'models', '-n', 'realesrgan-x4plus' if a.backgrounds else 'realesr-animevideov3', '-s', '4' if a.backgrounds else '2',
                         '-t', '128', '-j', '1:1:1'], log)
                run([a.dotnet, a.texture_tools, 'finish4' if a.backgrounds else 'finish', source, ai, temporary])
            if dimensions(temporary) != (w * 2, h * 2):
                raise ValueError(f'Output dimensions incorrect: {relative}')
            temporary.replace(target); stamp.write_text(json.dumps(expected, indent=2))
        entries.append({'Asset': relative[:-4], 'File': output_relative, 'Width': w, 'Height': h,
                        'Scale': 2, 'SourceSha256': fingerprint,
                        'Sha256': hashlib.sha256(target.read_bytes()).hexdigest(), 'Method': expected['method']})
    manifest = {'Version': 1, 'Recipe': 'scene-x4plus-original-chroma-v2' if a.backgrounds else 'hd2x-original-chroma-v2', 'TextureCount': len(entries),
                'ElapsedSeconds': round(time.monotonic() - start, 2), 'Textures': entries}
    # Only a complete pack is activated by the runtime; samples never activate.
    name = 'sample.json' if a.only else 'scenes.json' if a.backgrounds else 'manifest.json'
    temporary = output / (name + '.tmp')
    temporary.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    temporary.replace(output / name)
    print(f'Complete: {len(entries)} textures, {manifest["ElapsedSeconds"]} seconds.', flush=True)


if __name__ == '__main__':
    main()
