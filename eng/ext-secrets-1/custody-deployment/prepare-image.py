#!/usr/bin/env python3
"""Create a new offline image context from a reviewed, hash-bound publish output. Never build/push/apply/run."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import re

def strict_json(text):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result: raise ValueError("conflicting duplicate JSON property")
            result[key] = value
        return result
    return json.loads(text, object_pairs_hook=unique)



def prepare(publish, inventory, expected, output, runtime_image=None):
    raw = inventory.read_bytes()
    if hashlib.sha256(raw).hexdigest() != expected: raise ValueError('publish inventory digest mismatch')
    packet = strict_json(raw)
    if not isinstance(runtime_image, str) or not re.fullmatch(r"[a-z0-9][a-z0-9./:_-]*@sha256:[a-f0-9]{64}", runtime_image):
        raise ValueError('reviewed digest-pinned runtime image input missing or invalid')
    if not isinstance(packet, dict) or set(packet) != {'runtimeImage', 'files'} or packet['runtimeImage'] != runtime_image:
        raise ValueError('runtime image differs from the reviewed hash-bound inventory')
    entries = packet['files']
    if not isinstance(entries, dict) or not entries: raise ValueError('nonempty publish inventory required')
    files = {str(path.relative_to(publish)): path for path in publish.rglob('*') if path.is_file()}
    if files.keys() != entries.keys() or 'Hexalith.Platform.Custody.Dapr.dll' not in files:
        raise ValueError('complete exact component publish inventory required')
    for name, source in files.items():
        if source.is_symlink() or source.resolve().is_relative_to(publish.resolve()) is False:
            raise ValueError('publish paths must stay inside reviewed directory')
        if source.suffix not in {'.dll', '.json', '.pdb', '.so', '.xml'} and source.name not in {'Hexalith.Platform.Custody.Dapr', 'web.config'}:
            raise ValueError('unrecognized publish asset')
        if hashlib.sha256(source.read_bytes()).hexdigest() != entries[name]: raise ValueError('publish asset digest mismatch')
    recipe = (Path(__file__).parent / 'Dockerfile').read_text()
    if recipe.count('@@REVIEWED_RUNTIME_IMAGE@@') != 1 or 'ARG ' in recipe: raise ValueError('unbound runtime recipe')
    recipe = recipe.replace('@@REVIEWED_RUNTIME_IMAGE@@', runtime_image)
    output.mkdir()  # Exclusive; retained contexts are never replaced.
    destination = output / 'reviewed-publish'; destination.mkdir()
    for name, source in files.items():
        target = destination / name; target.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(source, target)
        if hashlib.sha256(target.read_bytes()).hexdigest() != entries[name]: raise ValueError('copy digest mismatch')
    (output / 'Dockerfile').write_text(recipe)
    (output / 'publish-inventory.json').write_bytes(raw)
    return {'status': 'OfflineContextPrepared', 'publishInventorySha256': expected, 'runtimeImage': runtime_image, 'recipeSha256': hashlib.sha256(recipe.encode()).hexdigest(), 'files': len(files),
            'imageBuilt': False, 'installedProvider': False, 'physicalQualification': False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--publish', type=Path, required=True)
    parser.add_argument('--inventory', type=Path, required=True)
    parser.add_argument('--inventory-sha256', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--runtime-image')
    args = parser.parse_args()
    try: print(json.dumps(prepare(args.publish, args.inventory, args.inventory_sha256, args.output, args.runtime_image), indent=2))
    except (OSError, ValueError, TypeError) as error:
        print(json.dumps({'status': 'Blocked', 'reason': str(error), 'imageBuilt': False})); return 1
    return 0

if __name__ == '__main__': raise SystemExit(main())
