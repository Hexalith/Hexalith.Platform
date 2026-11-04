import datetime, hashlib, io, json, re, subprocess, xml.etree.ElementTree as ET, zipfile
from pathlib import Path
source = '338a6afc31850761f49c0de7d399266dd15cfe84'
root = Path('/home/administrator/.cache/hexalith/story-2-8/platform-full-parties-20261104/Hexalith.Platform')
ev = Path(__file__).resolve().parent
base = Path('/home/administrator/.cache/hexalith/story-2-8/platform-338a-hosted-20261104')
archive = base / 'platform-tuple-41817311075-1.zip'
sha = lambda b: hashlib.sha256(b).hexdigest()
assert sha(archive.read_bytes()) == '28770f3e70b2bbae86892b30a0e7e4a9d0992c1f5e692ce0e82209dad6d59572'
directory = base / 'platform-tuple-41817311075-1'
result_path = next(directory.rglob('tuple-ci-result.json'))
r = json.loads(result_path.read_bytes())
bootstrap = json.loads(next(directory.rglob('bootstrap.json')).read_bytes())
assert bootstrap['context']['PLATFORM_CANDIDATE_SHA'] == source
assert bootstrap['context']['GITHUB_RUN_ID'] == '41817311075' and bootstrap['context']['GITHUB_RUN_ATTEMPT'] == '1'
assert all(v == 'success' for v in bootstrap['outcomes'].values())
assert r['platformRevision'] == source and r['status'] == 'passed' and r['candidateAcceptance'] is False
assert r['platformSourceState']['dirty'] is False
assert all(c['exitCode'] == 0 for c in r['commands'])
hashes = r['platformSourceState']['inputHashes']
requests = ''.join(source + ':' + p + '\n' for p in hashes)
raw = subprocess.check_output(['git', '-C', str(root), 'cat-file', '--batch'], input=requests.encode())
stream = io.BytesIO(raw)
for path, expected in hashes.items():
    header = stream.readline().decode().strip().split()
    assert len(header) == 3 and header[1] == 'blob', (path, header)
    size = int(header[2]); body = stream.read(size); assert stream.read(1) == b'\n'
    assert sha(body) == expected, path
groups = []
for i, expected in enumerate([83, 2, 24, 44, 7, 28, 9, 4, 37, 3, 21, 9]):
    p = result_path.parent / ('required-' + str(i) + '.xml')
    xml = ET.parse(p).getroot(); assembly = xml.find('assembly'); counts = assembly.attrib
    assert int(counts['passed']) == int(counts['total']) == expected
    assert all(int(counts[k]) == 0 for k in ['errors', 'failed', 'skipped', 'not-run'])
    tests = xml.findall('.//test'); assert len(tests) == expected and all(t.attrib['result'] == 'Pass' for t in tests)
    assert r['tests'][i]['counts']['passed'] == str(expected)
    groups.append({'file': p.name, 'passed': expected, 'sha256': sha(p.read_bytes())})
reader_xml = ET.parse(result_path.parent / 'required-6.xml').getroot()
reader_cases = []
expected_hashes = {
    'Hexalith.EventStore.Contracts': '5680933ce8003475a3c323ddd48f299be0eeac52810424814ca6e39bab763d53',
    'Hexalith.EventStore.Client': '0bca680b2fc475720a113e05a73d328f01f7a7a37e98fb2babc94a35532e0e9c',
    'Hexalith.EventStore.DomainService': 'f35786f3df58263d34e60ddfa83844cb43b2e7ac345420cb901f9ef49b7e3eb8',
    'Hexalith.EventStore.ServiceDefaults': '860b843eb81e5e1b1e6176b8865e79a116b62182c28cf5a39230f00bfac2115d',
    'Hexalith.Tenants.Contracts': '5676f656752395e149296ffbe9bc9d5e5e699e4bd6d4265df7de0f3442f05a40',
    'Hexalith.Tenants.Server': '4c03251c6378d131287a02b5b3871387874afcfff6f059f8bd52f9b640e15c93',
}
for test in reader_xml.findall('.//test'):
    method = test.attrib['method']; output = test.findtext('output') or ''
    if method == 'AuthenticatedPackageReaderBuildsAndExecutesWithPublicArtifactsAsync':
        assert 'catalog-root=1756c78fa0cdca5eaf1a712265a54ca9cd7a489afb49d8f0edfb6f84b74588cb' in output
        assert 'Build succeeded.' in output and '0 Error(s)' in output
        for name, digest in expected_hashes.items(): assert name + ' sha256=' + digest in output
    elif method == 'QualifiedTenantsModelForwardsDistinctReceiverTokensAsync':
        assert 'qualified-tenant-channel-forwarding=domain-and-api; no resources started' in output
        assert 'catalog-root=1756c78fa0cdca5eaf1a712265a54ca9cd7a489afb49d8f0edfb6f84b74588cb' in output
        for name, digest in expected_hashes.items(): assert name + ' sha256=' + digest in output
    elif method == 'ChangedRunAuthorityRefusesBeforeReaderBuildAsync':
        assert 'authority-refusal=' in output
        assert 'differs from the command-resolved hash' in output or 'HXW012' in output
    elif method == 'ReaderChecksEffectiveVersionsAndRestoredAssetsAsync':
        assert 'HXW012' in output and 'requires 4.0.4' in output
    elif method == 'OrdinaryConsumerStillRejectsMixedActiveSdkSourceAndPackagesAsync':
        assert 'HXW005' in output and 'Build FAILED.' in output
    else: raise AssertionError(method)
    reader_cases.append({'name': test.attrib['name'], 'result': test.attrib['result'], 'outputSha256': sha(output.encode())})
full_reader_xml = ET.parse(result_path.parent / 'required-11.xml').getroot()
cohort_source = subprocess.check_output(['git', '-C', str(root), 'show', source + ':src/Hexalith.Platform.CatalogBuilder/PublishedCatalogArtifactCohort.cs']).decode()
full_hashes = dict(re.findall(r'\["([^"\n]+)"\] = "([0-9a-f]{64})"', cohort_source))
assert len(full_hashes) == 12 and all(full_hashes[n] == h for n,h in expected_hashes.items())
full_cases = []
full_positive = 0
for test in full_reader_xml.findall('.//test'):
    output = test.findtext('output') or ''
    if test.attrib['method'] == 'OrdinaryConsumerRetainsActivePartiesSourceGuardAsync':
        assert 'HXW005' in output and "Source 'Hexalith.Parties'" in output and 'Build FAILED.' in output
    else:
        full_positive += 1
        assert re.search(r'full-reader catalog sha256=[0-9a-f]{64} root=[0-9a-f]{64} mapping=[0-9a-f]{64} public-bodies=12 routes=70 policies=46', output)
        for name,digest in full_hashes.items(): assert name + ' sha256=' + digest in output
        if test.attrib['method'] == 'FullReaderRejectsChangedOwnerVersionOrAssetsAsync':
            assert 'HXW012' in output and 'requires 3.0.23' in output
    full_cases.append({'name': test.attrib['name'], 'result': test.attrib['result'], 'outputSha256': sha(output.encode())})
assert full_positive == 8 and len(full_cases) == 9
assert r['tupleBinding'] == json.loads((ev / 'parent-builds-4442-qualified-tuple-binding.json').read_text())['binding']
outputs = r['compositionTestOutputs']; assert outputs['unchanged'] is True and outputs['before'] == outputs['after']
for name, entry in outputs['before']['dependencies'].items(): assert entry['sha256'] == expected_hashes[name[:-4]]
identity_path = next(directory.rglob('artifact-identity.json')); ident = json.loads(identity_path.read_bytes())
assert ident['platformRevision'] == source and ident['platformSourceState'] == r['platformSourceState']
assert ident['version'] == '4.44.2' and ident['buildsRevision'] == 'ae68c59bd2f4145a23ccf68c3711f98eaf62195f'
package_path = next(identity_path.parent.glob('*.nupkg')); assert sha(package_path.read_bytes()) == 'eded95db31ca23e41fe7ba630c823224833755e1970e26ff4cd8f3482ba69dbf'
retained = {}; absent = []
with zipfile.ZipFile(package_path) as z:
    for path, digest in ident['files'].items():
        assert sha(z.read(path)) == digest
        p = identity_path.parent / 'published' / path
        if p.exists(): assert sha(p.read_bytes()) == digest; retained[path] = digest
        else: absent.append(path)
assert len(retained) == 315 and len(ident['files']) == 317
observed_version = (result_path.parent / 'version.stdout.txt')
versions = [p for p in result_path.parent.glob('*version*')]
record = {
    'at': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'source': source, 'runId': 41817311075, 'attempt': 1,
    'artifactId': 13315413304, 'artifactSha256': sha(archive.read_bytes()), 'sourceStateClean': True,
    'allSourceInputHashesComparedToImmutableGit': len(hashes), 'actualXmlGroups': groups, 'passedTotal': 271,
    'actualExecutedReaderCases': reader_cases, 'actualExecutedFullReaderCases': full_cases, 'fullReaderPositiveBaselines': full_positive, 'fullReaderPublicHashes': full_hashes, 'readerPositiveOutputPublicHashes': expected_hashes,
    'readerPositiveCatalogRoot': '1756c78fa0cdca5eaf1a712265a54ca9cd7a489afb49d8f0edfb6f84b74588cb',
    'allCommandsExit0': len(r['commands']), 'recordedTuple': r['tupleBinding'],
    'allSignedArchiveMemberHashesVerified': len(ident['files']), 'retainedExtractedMembersVerified': len(retained),
    'signedArchiveOnlyMembers': absent, 'beforeAfterCompositionOutputHashObservationsUnchanged': True,
    'nativeAcceptance': False, 'candidateAcceptance': False,
    'limits': ['The reader tests use synthetic active-source entry metadata and real public SDK/catalog inputs; no services started.',
        'Positive output proves real reader execution, not a conditional early return. Reader DLL bodies are not retained; their hashes/MVIDs are observations in test output.',
        'The exact same public signed CLI archive was independently repository-signature verified offline in the 4.44.2 official audit; no online revocation claim.',
        'Full native services, cross-version wire behavior and Story2.8 lifecycle qualification remain incomplete.'],
}
p = ev / 'parent-platform-338a-hosted-tuple-audit.json'; assert not p.exists(); p.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'output': str(p), 'sourceFiles': len(hashes), 'passed': 271, 'readerCasesActuallyExecuted': len(reader_cases), 'signedPackageMembers': len(ident['files']), 'retainedPackageMembers': len(retained)}))
