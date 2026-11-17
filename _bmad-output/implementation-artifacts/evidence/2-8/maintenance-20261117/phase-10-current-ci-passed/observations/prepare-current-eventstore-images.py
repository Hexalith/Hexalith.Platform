"""Prepare the exact existing private EventStore purpose's current image bindings; no native execution."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path('/home/administrator/projects/hexalith/platform')
AREA = ROOT / 'artifacts/2-8-current-20261117'
OUT = AREA / 'platform-current-eventstore-images-01'
OUT.mkdir(exist_ok=False)
REPO = Path(os.environ['TMPDIR']) / 'Hexalith.Platform'
BUILDS = REPO.parent / 'Hexalith.Builds'
BASE = '408481da06128ae50ad274d5dddcd4234d8b677d'
SOURCE = '7afc4981e21bc2fcce687199a3fd2c61fe87336e'
BRANCH = 'fix/story-2-8-current-eventstore-images'
NODE = Path('/home/administrator/.nvm/versions/node/v24.22.1/bin/node')
ENV = dict(os.environ, PATH=str(NODE.parent) + ':' + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
RECORD = {'base': BASE, 'buildsVersion': '6.3.0', 'remoteMutations': False, 'nativeExecution': False, 'commands': []}


def run(label, argv, cwd=REPO, payload=None, timeout=300):
    result = subprocess.run(argv, cwd=cwd, env=ENV, input=payload, capture_output=True, text=True, timeout=timeout)
    (OUT / (label + '.log')).write_text(result.stdout + result.stderr)
    RECORD['commands'].append({'label': label, 'exitCode': result.returncode})
    print(label, result.returncode, flush=True)
    if result.returncode: raise RuntimeError(label)
    return result


def git(label, *args):
    return run(label, ['git', '-c', 'submodule.recurse=false', *args]).stdout.strip()


def replace(name, before, after):
    path = REPO / name
    raw = path.read_bytes()
    assert raw.count(before.encode()) == 1, name
    path.write_bytes(raw.replace(before.encode(), after.encode()))


try:
    run('clone', ['git', 'clone', '--shared', '--no-checkout', str(ROOT), str(REPO)], ROOT)
    git('recover', 'fetch', str(AREA / 'platform630-source-07/Platform.bundle'), 'refs/heads/fix/story-2-8-builds-6-3')
    git('checkout', 'checkout', '-b', BRANCH, BASE)
    binding = 'src/Hexalith.Platform.Composition/PlatformPackageCatalogQualification.cs'
    before = '            (_, "gateway", null) when server.ProjectPath == options.EventStoreSeedProject => server,\r\n'
    rows = ('            (PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName, "domain", "registry.hexalith.com/tenants:7.0.7") => server with { ImageDigest = "590bc04ecc3301daef22b318d029b486edd8b40b550a592a32461223966a9635" },\r\n'
            '            (PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName, "api", "registry.hexalith.com/tenants-api:7.0.7") => server with { ImageDigest = "2a72f0e36a0cfeef9b7e26731315049248fa6f2be164547de102b12481105b65" },\r\n')
    replace(binding, before, rows + before)
    replace(binding, 'or 4.2.0/Tenants 7.0.5 package-test purpose; other package cohorts remain unavailable.',
        'or 4.2.0/Tenants 7.0.5 or 7.0.7 package-test purpose; other package cohorts remain unavailable.')
    fixture_name = 'eng/fixtures/tenants-7.0.7-package-test-image-facts.json'
    (REPO / fixture_name).write_bytes((AREA / 'tenants-7.0.7-package-test-image-facts.json').read_bytes())
    test = 'tests/Hexalith.Platform.Composition.Tests/PlatformEventStorePackageTestCohortTests.cs'
    before = '        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, true));\r\n    }\r\n\r\n    private static CompositionRunPlan Sign'
    after = '''        var bound = PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, true);
        bound.Count.ShouldBe(3);
        using JsonDocument facts = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(PlatformTestRepository.Root,
            "eng", "fixtures", "tenants-7.0.7-package-test-image-facts.json")));
        facts.RootElement.GetProperty("source").GetString().ShouldBe("bcee4b4dbd886b7b718843cfb80419a92d33de15");
        facts.RootElement.GetProperty("images").GetArrayLength().ShouldBe(2);
        foreach (JsonElement image in facts.RootElement.GetProperty("images").EnumerateArray())
        {
            string manifest = image.GetProperty("manifestJson").GetString()!;
            string observed = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(manifest)));
            observed.ShouldBe(image.GetProperty("linuxAmd64ManifestDigest").GetString());
            image.GetProperty("labels").GetProperty("org.opencontainers.image.revision").GetString()
                .ShouldBe("bcee4b4dbd886b7b718843cfb80419a92d33de15");
            image.GetProperty("labels").GetProperty("org.opencontainers.image.version").GetString().ShouldBe("7.0.7");
            var selected = bound.Single(server => server.Image == image.GetProperty("reference").GetString());
            selected.ImageDigest.ShouldBe(observed["sha256:".Length..]);
        }
        var gateway = bound.Single(server => server.ServerId == "gateway");
        gateway.ImageDigest.ShouldBeNull();
        gateway.ProjectPath.ShouldBe(fixture.Options.EventStoreSeedProject);
        Directory.Exists(fixture.Plan.Workspace).ShouldBeFalse();
    }

    private static CompositionRunPlan Sign'''.replace('\n', '\r\n')
    replace(test, before, after)
    # The other38 refusal/body cases remain unchanged, including every incoming digest and ordinary-proof refusal.
    for owner in ('EventStore', 'Parties', 'Tenants', 'Memories'):
        name = 'Hexalith.' + owner
        selected = git('owner-pin-' + owner, 'rev-parse', 'HEAD:references/' + name)
        target = REPO / 'references' / name
        run('owner-clone-' + owner, ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references' / name), str(target)])
        run('owner-checkout-' + owner, ['git', 'checkout', '--detach', selected], target)
    run('clone-builds', ['git', 'clone', '--shared', '--no-checkout', str(ROOT / 'references/Hexalith.Builds'), str(BUILDS)])
    run('fetch-builds', ['git', 'fetch', '--no-recurse-submodules', 'https://github.com/Hexalith/Hexalith.Builds.git', SOURCE], BUILDS)
    run('checkout-builds', ['git', 'checkout', '--detach', SOURCE], BUILDS)
    run('clone-direct-builds', ['git', 'clone', '--shared', '--no-checkout', str(BUILDS), str(REPO / 'references/Hexalith.Builds')])
    run('checkout-direct-builds', ['git', 'checkout', '--detach', SOURCE], REPO / 'references/Hexalith.Builds')
    run('npm-ci', ['npm', 'ci', '--ignore-scripts', '--no-fund'])
    run('signatures', ['npm', 'audit', 'signatures'])
    message = 'fix(ci): pin current EventStore package test images\n'
    (OUT / 'commit-message.txt').write_text(message)
    run('commitlint', [str(NODE), str(REPO / 'node_modules/@commitlint/cli/cli.js'), '--config', str(REPO / 'commitlint.config.mjs'), '--color=false'], payload=message)
    git('stage', 'add', '--', binding, test, fixture_name)
    git('diff-check', 'diff', '--cached', '--check')
    git('commit', 'commit', '-F', str(OUT / 'commit-message.txt'))
    RECORD.update(source=git('source', 'rev-parse', 'HEAD'), tree=git('tree', 'rev-parse', 'HEAD^{tree}'), branch=BRANCH)
    git('source-diff', 'diff', '--binary', BASE, 'HEAD')
    git('bundle', 'bundle', 'create', str(OUT / 'Platform.bundle'), 'refs/heads/' + BRANCH, '^11719c5cf51c2b2b080b89e0e301c2ef8dbacc46')
    run('normal-published-artifact-preparation', ['python3', '-B', 'eng/prepare_builds_artifacts.py', '--output', str(REPO.parent / 'verified-publication'), '--require-recorded'])
    identity = json.loads((REPO.parent / 'verified-publication/artifact-identity.json').read_bytes())
    (OUT / 'artifact-identity.json').write_text(json.dumps(identity, indent=2) + '\n')
    tool = Path(identity['toolRoot'])
    properties = ['-p:Configuration=Release', '-p:UseHexalithProjectReferences=false', '-p:HexalithEventStoreVersion=4.2.0',
        '-p:HexalithTenantsVersion=7.0.7', '-p:HexalithMemoriesVersion=3.0.27', '-p:HexalithPartiesVersion=3.1.1',
        '-p:HexalithMcpCliVersion=2.0.1', '-p:HexalithBuildsRoot=' + str(BUILDS), '-p:HexalithBuildsPublishedToolRoot=' + str(tool)]
    project = 'tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj'
    run('build-guard-project', ['dotnet', 'build', project, '--configuration', 'Release', '--disable-build-servers', '-m:1', '-warnaserror', *properties], timeout=900)
    assembly = run('guard-assembly', ['dotnet', 'msbuild', project, '-nologo', *properties, '-getProperty:TargetPath']).stdout.strip()
    xml = OUT / 'eventstore-private-purpose-controls.xml'
    run('eventstore-private-purpose-controls', ['dotnet', assembly, '-class', 'Hexalith.Platform.Composition.Tests.PlatformEventStorePackageTestCohortTests', '-result-xml', str(xml)], timeout=300)
    counts = ET.parse(xml).getroot().find('assembly').attrib
    assert int(counts['passed']) == int(counts['total']) == 40 and not any(int(counts.get(key, '0')) for key in ('failed','errors','skipped','not-run')), counts
    RECORD.update(status='exact-private-purpose-image-proposal-passed; not pushed or natively qualified', currentGuardCases=40,
                  nativeQualificationCredit=False, candidateAcceptance=False)
except BaseException as error:
    RECORD.update(status='incomplete', reason=type(error).__name__ + ': ' + str(error))
    raise
finally:
    if REPO.exists():
        subprocess.run(['git', 'bundle', 'create', str(OUT / 'recoverable.bundle'), 'refs/heads/' + BRANCH, '^11719c5cf51c2b2b080b89e0e301c2ef8dbacc46'], cwd=REPO, capture_output=True)
    (OUT / 'result.json').write_text(json.dumps(RECORD, indent=2) + '\n')
