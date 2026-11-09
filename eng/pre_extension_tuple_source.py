"""Bind all 412 SDK4.1 controls to their real same-code committed pre-extension tuple."""
from __future__ import annotations
import argparse
import hashlib
import json
import copy
import re
from pathlib import Path
from legacy_tuple_source import commit, decode, git, parties_source, CURRENT_PARTIES_SOURCE

ROOT = Path(__file__).resolve().parents[1]
DESCRIPTOR = 'eng/pre-extension-tuple-source.json'
BUILDS = '27145e52ffab5623198ba5efdab5cdb71aa42d28'
EVENTSTORE = 'cab8363c2b6e2c0588efc8ca9c2e09fab0006841'
SDK42_SOURCE = 'aa16227bd07f0564a3026d619a5abd973e02656f'
PROFILE_HASH = '3643ab2f60f255bd0d0b9a91452e1f7060b5f5b69bf0fef0a51cf8336a526c6c'
PROVENANCE = 'src/Hexalith.Platform.Composition/Declarations/provenance.json'
TOOLS_HASH = 'f8cf363dd259788d71047ed7e3b14e5917f7531c43b06a2f83f438359dc23b6c'
IDENTITY_HASH = '6b6894855b603881d19c44dd8787ed2840176c0190aec5fecaa64f1a93f4ee9b'
BINDING_HASH = '9dbcfc55c112c166e0374aa1a96d8907c733938d86725a0e86034657475be4df'
PROVENANCE_HASH = '3931a6ff47adf0edd4b6d19430294f6ebed2c334b50655528e751eee049fc846'
ALLOWED = {DESCRIPTOR, 'eng/legacy-tuple-source.json', PROVENANCE, 'eng/builds-tuple.json',
           '.config/dotnet-tools.json', 'hexalith-platform.json', 'references/Hexalith.Builds',
           'references/Hexalith.EventStore'}


def resolve(root: Path, current: str, requested: str | None = None) -> str:
    current = commit(current)
    raw = git(root, 'show', current + ':' + DESCRIPTOR)
    if len(raw) > 4096:
        raise ValueError('Pre-extension source descriptor exceeds its fixed bound.')
    document = decode(raw)
    if set(document) != {'schema', 'purpose', 'sourceRevision', 'buildsSource', 'eventStoreSource', 'toolVersion', 'profileSha256', 'requiredCount', 'qualificationCredit'}:
        raise ValueError('Unsupported pre-extension source descriptor fields.')
    if document['schema'] != 'hexalith.platform.pre-extension-tuple-source.v1' or document['purpose'] != 'same-code-sdk41-tuple-regression' \
            or document['buildsSource'] != BUILDS or document['eventStoreSource'] != EVENTSTORE \
            or document['toolVersion'] != '4.45.0' or document['profileSha256'] != PROFILE_HASH \
            or document['requiredCount'] != 412 or document['qualificationCredit'] is not False:
        raise ValueError('Pre-extension descriptor changed its finite historical obligations.')
    historical = commit(document['sourceRevision'])
    if requested and commit(requested) != historical:
        raise ValueError('Dispatch cannot substitute another pre-extension source.')
    return historical



def verify_tuple_metadata(root: Path, current: str, historical: str):
    """Permit only producer/publication identities and their exact tool/range transformations."""
    previous_raw = git(root, 'show', historical + ':eng/builds-tuple.json')
    if hashlib.sha256(previous_raw).hexdigest() != BINDING_HASH:
        raise ValueError('Historical tuple metadata differs from the exact published4.45 binding.')
    previous = decode(previous_raw)
    selected = decode(git(root, 'show', current + ':eng/builds-tuple.json'))
    if set(selected) != set(previous) or {key: value for key, value in selected.items() if key not in ['producer', 'publication']} \
            != {key: value for key, value in previous.items() if key not in ['producer', 'publication']}:
        raise ValueError('Non-tuple binding fields changed.')
    producer, publication = selected['producer'], selected['publication']
    if set(producer) != set(previous['producer']) or set(producer['catalog']) != set(previous['producer']['catalog']) \
            or set(publication) != set(previous['publication']) or producer['repository'] != previous['producer']['repository'] \
            or producer['catalog']['path'] != previous['producer']['catalog']['path']:
        raise ValueError('Unapproved tuple producer/publication fields changed.')
    for value in [producer['sourceRevision'], producer['catalog']['blob']]:
        commit(value)
    for value in [producer['catalog']['sha256'], publication['catalogSha256'], publication['archiveSha256'], publication['toolingSha256'], publication['inventorySha256']]:
        if not isinstance(value, str) or re.fullmatch(r'[a-f0-9]{64}', value) is None:
            raise ValueError('Tuple identity requires an exact SHA256.')
    version = publication['version']
    if not isinstance(version, str) or re.fullmatch(r'4\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', version) is None \
            or publication['releaseTag'] != 'v' + version or publication['sourceRevision'] != producer['sourceRevision'] \
            or publication['catalogSha256'] != producer['catalog']['sha256'] \
            or publication['inventoryUrl'] != 'https://github.com/Hexalith/Hexalith.Builds/releases/download/v' + version + '/g4-tool-package-inventory.json' \
            or git(root, 'rev-parse', current + ':references/Hexalith.Builds').decode().strip() != producer['sourceRevision']:
        raise ValueError('Tuple source/version/catalog/publication identities are inconsistent.')
    old_tools_raw = git(root, 'show', historical + ':.config/dotnet-tools.json')
    if hashlib.sha256(old_tools_raw).hexdigest() != TOOLS_HASH:
        raise ValueError('Historical tool manifest differs from the exact published4.45 controls.')
    old_tools = decode(old_tools_raw)
    expected_tools = copy.deepcopy(old_tools)
    expected_tools['tools']['hexalith.builds.module.cli']['version'] = version
    if decode(git(root, 'show', current + ':.config/dotnet-tools.json')) != expected_tools:
        raise ValueError('Tool manifest changed more than the exact bound CLI version.')
    old_identity_raw = git(root, 'show', historical + ':hexalith-platform.json')
    if hashlib.sha256(old_identity_raw).hexdigest() != IDENTITY_HASH:
        raise ValueError('Historical Platform identity differs from the exact published4.45 controls.')
    old_identity = decode(old_identity_raw)
    if old_identity.get('toolVersionRange') != '[4.45.0, 5.0.0-0)':
        raise ValueError('Historical range floor differs from the exact4.45 tuple.')
    expected_identity = copy.deepcopy(old_identity)
    expected_identity['toolVersionRange'] = '[' + version + ', 5.0.0-0)'
    if decode(git(root, 'show', current + ':hexalith-platform.json')) != expected_identity:
        raise ValueError('Platform identity changed more than the exact bound range floor.')


def verify(root: Path, current: str, historical: str) -> dict:
    current, historical = commit(current), commit(historical)
    if resolve(root, current, historical) != historical or git(root, 'rev-parse', 'HEAD').decode().strip() != historical:
        raise ValueError('Pre-extension job must execute its actual committed source.')
    changed = [path for path in git(root, 'diff', '--name-only', '-z', current, historical).decode().split('\0') if path]
    if any(path not in ALLOWED for path in changed):
        raise ValueError('Production, controls, helpers, build instructions or unapproved metadata differ between sources.')
    for path, expected in [('references/Hexalith.Builds', BUILDS), ('references/Hexalith.EventStore', EVENTSTORE)]:
        if git(root, 'rev-parse', historical + ':' + path).decode().strip() != expected:
            raise ValueError('Pre-extension source changed its exact owner gitlink: ' + path)
    if hashlib.sha256(git(root, 'show', historical + ':eng/tuple-ci-suites.json')).hexdigest() != PROFILE_HASH:
        raise ValueError('The original 412 controls or their positive counts changed.')
    raw = git(root, 'show', historical + ':' + PROVENANCE)
    if hashlib.sha256(raw).hexdigest() != PROVENANCE_HASH:
        raise ValueError('Pre-extension provenance differs from its authenticated historical Git body.')
    original = decode(raw)
    expected = decode(raw)
    expected['eventstore'].update(version='4.2.0', commit=SDK42_SOURCE)
    selected = decode(git(root, 'show', current + ':' + PROVENANCE))
    if selected != original and selected != expected:
        raise ValueError('Current provenance changed more than the measured SDK4.2 version and source.')
    if 'eng/legacy-tuple-source.json' in changed:
        from legacy_tuple_source import resolve as legacy_source
        for revision in [current, historical]:
            recorded = legacy_source(root, revision)
            git(root, 'cat-file', '-e', recorded + '^{commit}')
    if ('references/Hexalith.EventStore' in changed or selected == expected) and (selected != expected or git(root, 'rev-parse', current + ':references/Hexalith.EventStore').decode().strip() != SDK42_SOURCE):
        raise ValueError('Changed SDK gitlink must match the exact published source and provenance.')
    if selected == expected and (parties_source(root, historical) != CURRENT_PARTIES_SOURCE
            or parties_source(root, current) != CURRENT_PARTIES_SOURCE):
        raise ValueError('The pre-extension/current SDK4.2 replay requires unchanged exact EBE Parties sources.')
    verify_tuple_metadata(root, current, historical)
    tool = decode(git(root, 'show', historical + ':.config/dotnet-tools.json'))['tools']['hexalith.builds.module.cli']
    if tool['version'] != '4.45.0' or tool['rollForward'] is not False:
        raise ValueError('Pre-extension source changed its exact published tool identity.')
    return {'historicalSource': historical, 'currentSource': current, 'sourceEquivalence': 'verified',
            'purpose': 'same-code-sdk41-tuple-regression', 'qualificationCredit': False,
            'binaryEquivalenceClaimed': False, 'requiredCount': 412, 'allowedMetadataDifferences': sorted(changed)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--current', required=True)
    parser.add_argument('--requested')
    parser.add_argument('--verify', action='store_true')
    args = parser.parse_args()
    historical = resolve(ROOT, args.current, args.requested)
    print(json.dumps(verify(ROOT, args.current, historical), sort_keys=True) if args.verify else historical)


if __name__ == '__main__':
    main()
