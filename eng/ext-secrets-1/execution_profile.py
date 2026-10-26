"""Closed shipped .NET phase entrypoint and externally enrolled complete file census.

This validates supplied immutable installation evidence. It neither creates that evidence
nor proves an installed filesystem/image is immutable while another principal changes it.
"""
import hashlib
import json
from pathlib import Path
import re

ENTRY = 'Hexalith.Platform.Secrets.Qualification.Executor'
MAX_FILE = 1024 * 1024 * 1024


def file_hash(path):
    path = Path(path)
    if not path.is_absolute() or path.is_symlink() or not path.is_file():
        raise ValueError('ExactInstalledExecutionFile')
    if not 0 < path.stat().st_size <= MAX_FILE:
        raise ValueError('BoundedInstalledExecutionFile')
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def validate_execution_inventory(profile):
    """Return named blockers; accepted descriptor pins are checked by its caller too."""
    try:
        command = profile['command']
        if not isinstance(command, list) or profile['recoveryCommand'] != [
                '--phase-finalize' if arg == '--phase-command' else arg for arg in command]:
            raise ValueError()
        closure = profile['dependencyClosure']
        if closure['schema'] != 'Platform.InstalledDotnetExecutionClosure.v1':
            raise ValueError()
        entry = Path(closure['entryAssembly'])
        if entry.name != ENTRY + '.dll' or not entry.is_absolute():
            raise ValueError()
        if command not in ([str(entry.with_suffix('')), '--phase-command'],
                            [command[0], str(entry), '--phase-command']):
            raise ValueError()
        launcher = Path(command[0])
        if len(command) == 3 and launcher.name != 'dotnet':
            raise ValueError()
        with launcher.open('rb') as stream:
            magic=stream.read(4)
        if magic[:2]!=b'MZ' and magic!=b'\x7fELF': raise ValueError()
        with entry.open('rb') as stream:
            if stream.read(2)!=b'MZ': raise ValueError()
        artifacts = profile['artifacts']
        pins = {item['path']: item['sha256'] for item in artifacts}
        if len(pins) != len(artifacts) or any(not re.fullmatch('[a-f0-9]{64}', value) for value in pins.values()):
            raise ValueError()
        roots = closure['directories']
        if not isinstance(roots, list) or not roots or len(set(roots)) != len(roots):
            raise ValueError()
        census = set()
        for raw in roots:
            root = Path(raw)
            if not root.is_absolute() or root.is_symlink() or not root.is_dir():
                raise ValueError()
            for item in root.rglob('*'):
                if item.is_symlink():
                    raise ValueError()
                if item.is_file():
                    census.add(str(item))
                if len(census) > 10000:
                    raise ValueError()
        # Full app/runtime directories are closed; extra, missing and replaced dependencies reject.
        if str(entry.parent) not in roots or str(launcher.parent) not in roots or not census.issubset(pins):
            raise ValueError()
        required = {str(entry), str(launcher), str(entry.with_suffix('.deps.json')),
                    str(entry.with_suffix('.runtimeconfig.json')), closure['installationFile']}
        environment = profile['environment']
        allowed = {'PLATFORM_JOINT_PHASE_COMMAND_INSTALLATION', 'DOTNET_ROOT',
                   'DOTNET_EnableDiagnostics', 'DOTNET_MULTILEVEL_LOOKUP'}
        if not isinstance(environment, dict) or not set(environment).issubset(allowed):
            raise ValueError()
        if environment.get('PLATFORM_JOINT_PHASE_COMMAND_INSTALLATION') != closure['installationFile']:
            raise ValueError()
        if 'DOTNET_ROOT' in environment and environment['DOTNET_ROOT'] not in roots:
            raise ValueError()
        if environment.get('DOTNET_EnableDiagnostics', '0') != '0' or environment.get('DOTNET_MULTILEVEL_LOOKUP', '0') != '0':
            raise ValueError()
        if not required.issubset(pins) or not set(closure['configurationFiles']).issubset(pins):
            raise ValueError()
        if closure['installationFile'] not in closure['configurationFiles']:
            raise ValueError()
        # Every public file reference loaded by the installation graph is independently pinned.
        private_keys={'ownersigningkeyfile','signingkeyfile','credentialfile','credentialreference','appchanneltokenreference','bearerreference','privatesigningkeyfile','sourcesigningkeyfile','rsapkcs8base64file','assertionfiles'}
        private_paths=set()
        for raw in closure['configurationFiles']:
            data = Path(raw).read_bytes()
            if len(data) > 1024 * 1024:
                raise ValueError()
            import qualify as q
            document = q.decode_json(data)
            def inspect(value, key=''):
                if isinstance(value, dict):
                    for name, child in value.items(): inspect(child, key if key.lower()=='assertionfiles' else name)
                elif isinstance(value, list):
                    for child in value: inspect(child, key)
                elif isinstance(value,str):
                    if key.lower() in private_keys:
                        path=value.removeprefix('file:')
                        if not Path(path).is_absolute(): raise ValueError()
                        private_paths.add(path)
                    elif (value.startswith('/') or value.startswith('file:/')) and value.removeprefix('file:') not in pins and value.removeprefix('file:') not in roots:
                        if key.lower() not in {'evidencedirectory','outputdirectory','owneroriginaldirectory','originalsdirectory','originalarchivedirectory'}:
                            raise ValueError()
            inspect(document)
        if private_paths.intersection(pins): raise ValueError()
        if any(file_hash(path) != expected for path, expected in pins.items()):
            return ['ExactInstalledExecutionDependencyBytes']
        return []
    except (OSError, ValueError, TypeError, KeyError, IndexError, AttributeError):
        return ['CompleteInstalledExecutionDependencyClosure']


def installed_image_blockers(profile):
    """No independently selected whole-image/native-loader measurement contract was supplied.

    An owner boolean or self-declared file graph cannot authenticate that wider surface.
    Private credential mounts remain governed by their separate current purpose-bound ports.
    """
    return ['External.IndependentlyVerifiedInstalledImageAndNativeLoaderMeasurementProtocol']


def validate_execution_profile(profile):
    return validate_execution_inventory(profile) + installed_image_blockers(profile)
