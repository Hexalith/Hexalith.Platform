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


import bounded_file_io as selected_io


def file_hash(path):
    return selected_io.file_hash(path, MAX_FILE)


def validate_execution_inventory(profile):
    try:
        return selected_io.run(lambda: _validate_execution_inventory(profile))
    except (OSError, ValueError, TimeoutError):
        return ['BoundedInstalledExecutionInputAcquisition']


def _validate_execution_inventory(profile):
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
        artifacts = profile['artifacts']
        pins = {item['path']: item['sha256'] for item in artifacts}
        if len(pins) != len(artifacts) or any(not re.fullmatch('[a-f0-9]{64}', value) for value in pins.values()):
            raise ValueError()
        roots = closure['directories']
        if not isinstance(roots, list) or not roots or len(set(roots)) != len(roots):
            raise ValueError()
        census = set()
        for raw in roots:
            census.update(selected_io.census(raw,10000-len(census)))
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
        private_keys={'ownersigningkeyfile','signingkeyfile','credentialfile','credentialreference','appchanneltokenreference','bearerreference','privatesigningkeyfile','sourcesigningkeyfile','rolesigningkeyfile','componentsigningkeypath','appchanneltokenfile','rsapkcs8base64file','assertionfiles'}
        private_paths=set()
        for raw in closure['configurationFiles']:
            data = selected_io.read(raw,1024*1024)
            if len(data) > 1024 * 1024:
                raise ValueError()
            import qualify as q
            document = q.decode_json(data)
            if raw==closure['installationFile'] and 'Scope' in document:
                if (document.get('BindingSchema')!='Platform.JointPhaseInstallation.v2'
                        or document['Scope'].get('BindingDigest')!=''
                        or document.get('CohortAttempt',{}).get('BindingDigest')!=''):
                    return ['UnsupportedCyclicJointPhaseInstallationBinding']
                if type(document.get('CompletionTimeoutSeconds')) is not int or not 1<=document['CompletionTimeoutSeconds']<=86400 or document['CompletionTimeoutSeconds']!=profile.get('completionTimeoutSeconds'):
                    return ['ExactPinnedWholePhaseCompletionTimeout']
                if document['Scope'].get('Phase')!=profile['phase'] or document['Scope'].get('Cases')!=profile['cases']:
                    return ['ExactAcyclicInstalledPhaseScope']
            def inspect(value, key=''):
                if isinstance(value, dict):
                    for name, child in value.items(): inspect(child, key if key.lower()=='assertionfiles' else name)
                elif isinstance(value, list):
                    for child in value: inspect(child, key)
                elif isinstance(value,str):
                    if key.lower() in private_keys or key.lower().endswith('credentialreference'):
                        path=value.removeprefix('file:')
                        if not Path(path).is_absolute(): raise ValueError()
                        private_paths.add(path)
                    elif (value.startswith('/') or value.startswith('file:/')) and value.removeprefix('file:') not in pins and value.removeprefix('file:') not in roots:
                        if key.lower() not in {'evidencedirectory','outputdirectory','owneroriginaldirectory','originalsdirectory','originalarchivedirectory'}:
                            raise ValueError()
            inspect(document)
        if private_paths.intersection(pins): raise ValueError()
        magic=selected_io.magic(launcher,MAX_FILE)
        if magic[:2]!=b'MZ' and magic!=b'\x7fELF': raise ValueError()
        if selected_io.magic(entry,MAX_FILE)[:2]!=b'MZ': raise ValueError()
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
