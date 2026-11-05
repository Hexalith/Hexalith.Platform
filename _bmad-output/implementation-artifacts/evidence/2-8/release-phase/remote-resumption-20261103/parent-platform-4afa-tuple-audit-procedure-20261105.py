import datetime, hashlib, io, json, re, subprocess, xml.etree.ElementTree as ET, zipfile
from pathlib import Path
source = '4afa0421794f7e6f3fcbd5d84664eae206e9daa5'
root = Path('/home/administrator/projects/hexalith/platform')
ev = Path(__file__).resolve().parent
base = Path('/home/administrator/.cache/hexalith/story-2-8/platform-4afa-hosted-20261105')
metadata=json.loads((ev/'parent-platform-4afa-tuple-artifact-list-20261105.json').read_bytes())['artifacts'];assert len(metadata)==1;metadata=metadata[0]
assert metadata['workflow_run']['head_sha']==source and metadata['workflow_run']['id']==42030769896 and not metadata['expired']
archive = base / 'platform-tuple-42030769896-1.zip'
sha = lambda b: hashlib.sha256(b).hexdigest()
assert sha(archive.read_bytes()) == metadata['digest'].removeprefix('sha256:')
directory = base / 'platform-tuple-42030769896-1'
result_path = next(directory.rglob('tuple-ci-result.json'))
r = json.loads(result_path.read_bytes())
bootstrap = json.loads(next(directory.rglob('bootstrap.json')).read_bytes())
assert bootstrap['context']['PLATFORM_CANDIDATE_SHA'] == source
assert bootstrap['context']['GITHUB_RUN_ID'] == '42030769896' and bootstrap['context']['GITHUB_RUN_ATTEMPT'] == '1'
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
assert r['requiredProfile']==json.loads(subprocess.check_output(['git','-C',str(root),'show',source+':eng/tuple-ci-suites.json']))
groups = []; unique=[]
for i, expected in enumerate(group['requiredCount'] for group in r['requiredProfile']['groups']):
    p = result_path.parent / ('required-' + str(i) + '.xml')
    xml = ET.parse(p).getroot(); assembly = xml.find('assembly'); counts = assembly.attrib
    assert int(counts['passed']) == int(counts['total']) == expected
    assert all(int(counts[k]) == 0 for k in ['errors', 'failed', 'skipped', 'not-run'])
    tests = xml.findall('.//test'); assert len(tests) == expected and all(t.attrib['result'] == 'Pass' for t in tests)
    assert r['tests'][i]['counts']['passed'] == str(expected)
    unique.extend(t.attrib['name'] for t in tests)
    groups.append({'file': p.name, 'passed': expected, 'sha256': sha(p.read_bytes())})
assert len(unique)==len(set(unique))==347
old_directory = Path('/home/administrator/.cache/hexalith/story-2-8/platform-newer-main-hosted-20261105/platform-tuple-42014821187-1')
old_result = next(old_directory.rglob('tuple-ci-result.json'))
old_names = {test.attrib['name'] for i in range(15) for test in ET.parse(old_result.parent / ('required-' + str(i) + '.xml')).getroot().findall('.//test')}
assert len(old_names) == 342 and old_names.issubset(set(unique))

reader_xml = ET.parse(result_path.parent / 'required-6.xml').getroot()
reader_cases = []
public=json.loads((base/'catalog-builder-consumer-binding-4.1.0/catalog-public-owner-evidence/result.json').read_bytes())['executedArtifacts']['files']
expected_hashes={n[:-4]:public[n] for n in ['Hexalith.EventStore.Contracts.dll','Hexalith.EventStore.Client.dll','Hexalith.EventStore.DomainService.dll','Hexalith.EventStore.ServiceDefaults.dll','Hexalith.Tenants.Contracts.dll','Hexalith.Tenants.Server.dll']}

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
        assert 'HXW012' in output and 'requires 4.1.0' in output
    elif method == 'OrdinaryConsumerStillRejectsMixedActiveSdkSourceAndPackagesAsync':
        assert 'HXW005' in output and 'Build FAILED.' in output
    else: raise AssertionError(method)
    reader_cases.append({'name': test.attrib['name'], 'result': test.attrib['result'], 'outputSha256': sha(output.encode())})
full_reader_xml = ET.parse(result_path.parent / 'required-11.xml').getroot()
cohort_source = subprocess.check_output(['git', '-C', str(root), 'show', source + ':src/Hexalith.Platform.CatalogBuilder/PublishedCatalogArtifactCohort.cs']).decode()
names=['Hexalith.EventStore.Contracts','Hexalith.EventStore.Client','Hexalith.EventStore.DomainService','Hexalith.EventStore.ServiceDefaults','Hexalith.Tenants.Contracts','Hexalith.Tenants.Server','Hexalith.Memories.Contracts','Hexalith.Memories.EventStore','Hexalith.Memories.EventStoreExtensions','Hexalith.Parties.Contracts','Hexalith.Parties.EventStoreExtensions','Hexalith.McpCli.Abstractions']
full_hashes={n:public[n+'.dll'] for n in names}
assert len(full_hashes)==12 and all(('["'+n+'"] = "'+h+'"') in cohort_source for n,h in full_hashes.items())

full_cases = []
full_positive = 0
for test in full_reader_xml.findall('.//test'):
    output = test.findtext('output') or ''
    if test.attrib['method'] == 'OrdinaryConsumerRetainsActivePartiesSourceGuardAsync':
        assert 'HXW005' in output and "Source 'Hexalith.Parties'" in output and 'Build FAILED.' in output
    elif test.attrib['method'] == 'RuntimeMaterializationRetainsVerifiedReaderEnrichmentAsync':
        # These two actual cases invoke the real reader but emit no body identity output.
        # Count their executed success separately; do not fabricate a public-output observation.
        assert test.attrib['result'] == 'Pass' and output == ''
    else:
        full_positive += 1
        assert re.search(r'full-reader catalog sha256=[0-9a-f]{64} root=[0-9a-f]{64} mapping=[0-9a-f]{64} public-bodies=12 routes=70 policies=46', output)
        for name,digest in full_hashes.items(): assert name + ' sha256=' + digest in output
        if test.attrib['method'] == 'FullReaderRejectsChangedOwnerVersionOrAssetsAsync':
            assert 'HXW012' in output and 'requires 3.0.23' in output
    full_cases.append({'name': test.attrib['name'], 'result': test.attrib['result'], 'outputSha256': sha(output.encode())})
assert full_positive == 8 and len(full_cases) == 11
assert r['tupleBinding'] == json.loads((ev / 'parent-builds-4443-qualified-tuple-binding-20261105.json').read_text())['binding']
outputs = r['compositionTestOutputs']; assert outputs['unchanged'] is True and outputs['before'] == outputs['after']
for name, entry in outputs['before']['dependencies'].items(): assert entry['sha256'] == expected_hashes[name[:-4]]
identity_path = next(directory.rglob('artifact-identity.json')); ident = json.loads(identity_path.read_bytes())
assert ident['platformRevision'] == source and ident['platformSourceState'] == r['platformSourceState']
assert ident['version'] == '4.44.3' and ident['buildsRevision'] == 'fc2122240a39f03628c04fa8433e5eece90b8b4c'
package_path = next(identity_path.parent.glob('*.nupkg')); assert sha(package_path.read_bytes()) == '32c2f517791bfce975bf775e73bbbf626cc710da83c68b89c5e305ef55acea32'
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
    'at': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'source': source, 'runId': 42030769896, 'attempt': 1,
    'artifactId': metadata['id'], 'artifactSha256': sha(archive.read_bytes()), 'sourceStateClean': True,
    'original342ExecutedNamesPreserved': True, 'addedExecutedNames': sorted(set(unique)-old_names), 'allSourceInputHashesComparedToImmutableGit': len(hashes), 'actualXmlGroups': groups, 'passedTotal': 347,
    'actualExecutedReaderCases': reader_cases, 'actualExecutedFullReaderCases': full_cases, 'fullReaderPositiveBaselines': full_positive, 'fullReaderPublicHashes': full_hashes, 'readerPositiveOutputPublicHashes': expected_hashes,
    'readerPositiveCatalogRoot': '1756c78fa0cdca5eaf1a712265a54ca9cd7a489afb49d8f0edfb6f84b74588cb',
    'allCommandsExit0': len(r['commands']), 'recordedTuple': r['tupleBinding'],
    'allSignedArchiveMemberHashesVerified': len(ident['files']), 'retainedExtractedMembersVerified': len(retained),
    'signedArchiveOnlyMembers': absent, 'beforeAfterCompositionOutputHashObservationsUnchanged': True,
    'nativeAcceptance': False, 'candidateAcceptance': False,
    'limits': ['The reader tests use synthetic active-source entry metadata and real public SDK/catalog inputs; no services started.',
        'Positive output proves real reader execution, not a conditional early return. Reader DLL bodies are not retained; their hashes/MVIDs are observations in test output.',
        'The exact same public signed CLI archive was independently repository-signature verified offline in the 4.44.3 official audit; no online revocation claim.',
        'Two added reader snapshot cases emitted no public-body diagnostics and stop before ordinary Generate; their execution does not prove the complete two-phase startup path.',
        'Full native services, cross-version wire behavior and Story2.8 lifecycle qualification remain incomplete.'],
}
p = ev / 'parent-platform-4afa-hosted-tuple-audit-20261105.json'; assert not p.exists(); p.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps({'output': str(p), 'sourceFiles': len(hashes), 'passed': 347, 'readerCasesActuallyExecuted': len(reader_cases), 'signedPackageMembers': len(ident['files']), 'retainedPackageMembers': len(retained)}))
