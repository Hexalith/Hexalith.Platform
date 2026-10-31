"""Fetch the public MCR base and write one local SDK archive; no publication."""
from pathlib import Path
import hashlib
import http.server
import json
import os
import select
import socket
import socketserver
import subprocess
import threading
import time
import urllib.request
from urllib.parse import urlsplit

ROOT = Path('/home/administrator/projects/hexalith/platform')
OUT = Path(__file__).resolve().parent
WORK = Path('/tmp/mcpcli-417-mapped-archive-20261030')
PROJECT = Path('/tmp/mcpcli-417-builds-mapped-source/Hexalith.Builds.Module.EventStoreHost.csproj')
HOST = Path('/tmp/mcpcli-417-builds-mapped-source/publish/Hexalith.Builds.Module.EventStoreHost.dll')
SHA = lambda data: hashlib.sha256(data).hexdigest()
ACCESS = []


def allowed(host):
    return host == 'mcr.microsoft.com' or host.endswith('.data.mcr.microsoft.com') or host.endswith('.mcr-msedge.net')


class Proxy(http.server.BaseHTTPRequestHandler):
    def log_message(self, *unused): pass

    def do_CONNECT(self):
        parsed = urlsplit('https://' + self.path)
        permitted = parsed.port == 443 and parsed.hostname and allowed(parsed.hostname)
        ACCESS.append({'host': parsed.hostname, 'port': parsed.port, 'allowed': bool(permitted)})
        if not permitted:
            self.send_error(403, 'Only public Microsoft Container Registry build inputs are allowed')
            return
        upstream = socket.create_connection((parsed.hostname, parsed.port), timeout=30)
        self.send_response(200, 'Connection Established'); self.end_headers()
        try:
            while True:
                ready, _, _ = select.select([self.connection, upstream], [], [], 60)
                if not ready: break
                for source in ready:
                    data = source.recv(65536)
                    if not data: return
                    (upstream if source is self.connection else self.connection).sendall(data)
        finally:
            upstream.close()


class Server(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True


class Redirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, file, code, message, headers, newurl):
        parsed = urlsplit(newurl)
        if parsed.scheme != 'https' or not allowed(parsed.hostname or ''):
            raise ValueError('Unrelated base-image redirect refused')
        return super().redirect_request(request, file, code, message, headers, newurl)


def main():
    WORK.mkdir()
    docker_config = WORK / 'anonymous-docker'; docker_config.mkdir()
    (docker_config / 'config.json').write_text('{"auths":{}}\n')
    server = Server(('127.0.0.1', 0), Proxy)
    thread = threading.Thread(target=server.serve_forever, daemon=True); thread.start()
    proxy = 'http://127.0.0.1:' + str(server.server_port)
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({'https': proxy}), Redirect())
    try:
        def fetch(name, reference):
            request = urllib.request.Request('https://mcr.microsoft.com/v2/dotnet/aspnet/manifests/' + reference,
                headers={'Accept': 'application/vnd.oci.image.index.v1+json, application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.oci.image.manifest.v1+json, application/vnd.docker.distribution.manifest.v2+json'})
            with opener.open(request, timeout=45) as response:
                raw = response.read(4 * 1024 * 1024)
                digest = response.headers.get('Docker-Content-Digest')
            actual = 'sha256:' + SHA(raw)
            if digest and digest != actual: raise ValueError('MCR content digest mismatch')
            (OUT / name).write_bytes(raw)
            return json.loads(raw), actual
        index, index_digest = fetch('base-index.json', '10.0')
        candidates = [d for d in index.get('manifests', []) if d.get('platform', {}).get('os') == 'linux' and d.get('platform', {}).get('architecture') == 'amd64']
        if len(candidates) != 1: raise ValueError('One linux/amd64 base manifest is required')
        manifest, selected_digest = fetch('base-manifest.json', candidates[0]['digest'])
        if selected_digest != candidates[0]['digest']: raise ValueError('Selected base descriptor mismatch')
        binding = {'repository': 'mcr.microsoft.com/dotnet/aspnet', 'tagObserved': '10.0', 'indexDigest': index_digest,
                   'linuxAmd64Digest': selected_digest, 'configDigest': manifest['config']['digest'],
                   'layerDigests': [layer['digest'] for layer in manifest['layers']]}
        (OUT / 'base-binding.json').write_text(json.dumps(binding, indent=2) + '\n')
        archive = WORK / 'eventstore.tar.gz'
        props = ['-p:Configuration=Debug', '-p:HexalithEventStoreRoot=' + str(ROOT / 'references/Hexalith.EventStore'),
                 '-p:UseHexalithProjectReferences=false', '-p:UseNuGetDeps=true', '-p:NuGetAudit=false',
                 '-p:UseArtifactsOutput=true', '-p:ArtifactsPath=/tmp/mcpcli-417-builds-mapped-output',
                 '-p:GeneratePackageOnBuild=false', '-p:UseAppHost=false']
        command = ['timeout', '300', 'dotnet', 'publish', str(PROJECT), '--no-restore', '--no-build',
                   '--configuration', 'Debug', '-m:1', '-t:PublishContainer', *props,
                   '-p:ContainerRegistry=', '-p:ContainerRepository=mcpcli-417-builds-eventstore',
                   '-p:ContainerImageTag=operator-setup', '-p:ContainerArchiveOutputPath=' + str(archive),
                   '-p:ContainerBaseImage=mcr.microsoft.com/dotnet/aspnet@' + selected_digest,
                   '-p:ContainerGenerateLabelsImageBaseDigest=true', '--output', str(HOST.parent)]
        env = {key: value for key, value in os.environ.items() if key not in ('DirectoryPackagesPropsPath', 'CustomAfterMicrosoftCommonProps')
               and not key.startswith(('DOTNET_CONTAINER_', 'SDK_CONTAINER_', 'DOCKER_', 'REGISTRY_', 'HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY', 'NO_PROXY', 'http_proxy', 'https_proxy', 'all_proxy', 'no_proxy'))}
        env.update(NUGET_PACKAGES='/tmp/mcpcli-417-builds-mapped-cache', DOCKER_CONFIG=str(docker_config),
                   HTTP_PROXY=proxy, HTTPS_PROXY=proxy, ALL_PROXY=proxy, NO_PROXY='127.0.0.1,localhost',
                   DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true')
        (OUT / 'build.command.json').write_text(json.dumps({'cwd': str(ROOT), 'argv': command,
            'environment': {key: env[key] for key in ('NUGET_PACKAGES', 'DOCKER_CONFIG', 'HTTP_PROXY', 'HTTPS_PROXY', 'NO_PROXY', 'DOTNET_CLI_TELEMETRY_OPTOUT')},
            'network': 'Public MCR and its Microsoft CDN endpoints only, via an allowlisted CONNECT proxy; anonymous auths; no restore/build/push'}, indent=2) + '\n')
        host_before = SHA(HOST.read_bytes())
        started = time.monotonic()
        with (OUT / 'build.stdout.txt').open('w') as stdout, (OUT / 'build.stderr.txt').open('w') as stderr:
            result = subprocess.run(command, cwd=ROOT, env=env, stdout=stdout, stderr=stderr)
        if SHA(HOST.read_bytes()) != host_before: raise ValueError('Compiled host changed during archive packaging')
        receipt = {'status': 'archive-created' if result.returncode == 0 and archive.is_file() else 'failed',
                   'exitCode': result.returncode, 'elapsedSeconds': time.monotonic() - started,
                   'archive': str(archive), 'archiveExists': archive.is_file(), 'compiledHostSha256': host_before,
                   'base': binding, 'publication': 'none; archive output only and ContainerRegistry empty'}
        if archive.is_file():
            digest = hashlib.sha256()
            with archive.open('rb') as file:
                for block in iter(lambda: file.read(1024 * 1024), b''): digest.update(block)
            receipt.update(archiveSha256=digest.hexdigest(), archiveBytes=archive.stat().st_size)
        (OUT / 'build-result.json').write_text(json.dumps(receipt, indent=2) + '\n')
        print(json.dumps(receipt, indent=2))
        return result.returncode
    finally:
        (OUT / 'network-access.json').write_text(json.dumps(ACCESS, indent=2) + '\n')
        server.shutdown(); server.server_close(); thread.join(timeout=5)


if __name__ == '__main__':
    raise SystemExit(main())
