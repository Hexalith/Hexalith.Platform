"""Install checksum-verified upstream tools without changing ambient Dapr."""
from datetime import datetime, timezone
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import tarfile
import urllib.request
import xml.etree.ElementTree as ET

out = Path(__file__).resolve().parent
root = out.parents[4]
catalog = root / 'references/Hexalith.Builds/Props/Directory.Packages.props'
properties = ET.parse(catalog).getroot()
cli = properties.findtext('.//HexalithDaprCliVersion')
runtime = properties.findtext('.//HexalithDaprRuntimeVersion')
assert cli and runtime
home = Path.home() / '.hexalith/dapr' / f'cli-{cli}-runtime-{runtime}'
cache = Path.home() / '.cache/hexalith-dapr'
cache.mkdir(parents=True, exist_ok=True)
assert not home.exists(), f'Refusing to replace installation: {home}'
home.mkdir(parents=True)
records = []
for repo, version, names, destination in [
    ('dapr/cli', cli, ['dapr'], home / 'tools'),
    ('dapr/dapr', runtime, ['daprd', 'placement', 'scheduler'], home / '.dapr/bin'),
]:
    url = f'https://api.github.com/repos/{repo}/releases/tags/v{version}'
    request = urllib.request.Request(url, headers={'User-Agent': 'hexalith-local-qualification'})
    with urllib.request.urlopen(request, timeout=60) as response:
        release = json.load(response)
    assets = {asset['name']: asset for asset in release['assets']}
    destination.mkdir(parents=True)
    for name in names:
        archive_name = f'{name}_linux_amd64.tar.gz'
        asset = assets[archive_name]
        archive = cache / f'{repo.replace("/", "-")}-{version}-{archive_name}'
        print(f'Downloading {repo} {version} {name}', flush=True)
        with urllib.request.urlopen(asset['browser_download_url'], timeout=120) as response, archive.open('wb') as stream:
            shutil.copyfileobj(response, stream)
        digest = hashlib.sha256(archive.read_bytes()).hexdigest()
        assert asset['digest'] == 'sha256:' + digest, archive_name
        with urllib.request.urlopen(assets[archive_name + '.sha256']['browser_download_url'], timeout=60) as response:
            upstream_checksum = response.read().decode().strip()
        assert upstream_checksum.split()[0] == digest, archive_name
        with tarfile.open(archive) as bundle:
            member = next(member for member in bundle.getmembers() if member.name.removeprefix('./') == name)
            assert member.isfile()
            binary = destination / name
            with bundle.extractfile(member) as source, binary.open('wb') as target:
                shutil.copyfileobj(source, target)
            binary.chmod(0o755)
        records.append({
            'repository': repo, 'version': version,
            'releaseUrl': release['html_url'], 'downloadUrl': asset['browser_download_url'],
            'archiveSha256': digest, 'upstreamChecksum': upstream_checksum,
            'binaryPath': str(binary),
            'binarySha256': hashlib.sha256(binary.read_bytes()).hexdigest(),
        })
environment = dict(os.environ, DAPR_RUNTIME_PATH=str(home))
version_output = subprocess.check_output([str(home / 'tools/dapr'), '--version'], env=environment, text=True)
assert f'CLI version: {cli}' in version_output and f'Runtime version: {runtime}' in version_output
assert subprocess.check_output([str(home / '.dapr/bin/daprd'), '--version'], text=True).strip() == runtime
backup_root = home / 'shell-backups'
backup_root.mkdir(mode=0o700)
shell_changes = []
block = f'# Hexalith isolated Dapr (CLI {cli}, runtime {runtime})\nexport HEXALITH_DAPR_HOME={home}\n'
for name in ['.profile', '.bashrc']:
    path = Path.home() / name
    original = path.read_bytes() if path.exists() else b''
    backup = backup_root / (name + '.before')
    backup.write_bytes(original)
    backup.chmod(0o600)
    path.write_bytes(block.encode() + original)
    shell_changes.append({'path': str(path), 'beforeSha256': hashlib.sha256(original).hexdigest(), 'afterSha256': hashlib.sha256(path.read_bytes()).hexdigest(), 'backup': str(backup)})
receipt = {
    'configuredAtUtc': datetime.now(timezone.utc).isoformat(),
    'catalog': str(catalog.relative_to(root)),
    'catalogSha256': hashlib.sha256(catalog.read_bytes()).hexdigest(),
    'cliVersion': cli, 'runtimeVersion': runtime, 'daprHome': str(home),
    'reportedVersions': version_output.strip().splitlines(),
    'binaries': records, 'shellConfiguration': shell_changes,
}
(out / 'dapr-installation.json').write_text(json.dumps(receipt, indent=2) + '\n')
print(version_output, flush=True)
print('Configured HEXALITH_DAPR_HOME=' + str(home), flush=True)
