from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, os, subprocess, tarfile, time, urllib.request
import xml.etree.ElementTree as ET
base=Path('/tmp/hexalith-story-2-10-20261110')
repo=base/'parties'
out=Path('/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-10/campaign-20261110')
props=repo/'references/Hexalith.Builds/Props'
catalog=props/'Directory.Packages.props'
catalog_blob=subprocess.check_output(['git','show','HEAD:Props/Directory.Packages.props'],cwd=repo/'references/Hexalith.Builds')
assert hashlib.sha256(catalog_blob).hexdigest()=='957818ed7df1b29b2299644afb06987393f617c5b87ff8551b9b95b52a23034d'
assert catalog.read_bytes().replace(b'\r\n',b'\n')==catalog_blob
assert not subprocess.check_output(['git','status','--porcelain','--','Props/Directory.Packages.props'],cwd=repo/'references/Hexalith.Builds').strip()
xml=ET.parse(catalog).getroot()
cli=xml.findtext('.//HexalithDaprCliVersion'); runtime=xml.findtext('.//HexalithDaprRuntimeVersion')
assert cli==runtime=='1.19.1'
digests=json.loads((props/'dapr-toolchain-sha256.json').read_text())
assert digests['schema']=='hexalith.builds.dapr-toolchain-sha256.v1'
home=base/'dapr-1.19.1'
home.mkdir(mode=0o700,exist_ok=True)
cache=Path.home()/'.cache/hexalith-lifecycle/dapr'
records=[]
for kind, repository, version, names, directory in [('cli','dapr/cli',cli,['dapr'],home/'tools'),('runtime','dapr/dapr',runtime,['daprd','placement','scheduler'],home/'.dapr/bin')]:
    directory.mkdir(mode=0o700,parents=True,exist_ok=True)
    for name in names:
        started=time.monotonic()
        filename=f'{name}_linux_amd64.tar.gz'
        expected=digests[kind][version][filename]
        target=cache/kind/version/expected/filename
        target.parent.mkdir(mode=0o700,parents=True,exist_ok=True)
        reused=target.is_file() and not target.is_symlink() and hashlib.sha256(target.read_bytes()).hexdigest()==expected
        if not reused:
            url=f'https://github.com/{repository}/releases/download/v{version}/{filename}'
            temporary=target.with_suffix('.download')
            request=urllib.request.Request(url,headers={'User-Agent':'Hexalith-Story-2-10'})
            with urllib.request.urlopen(request,timeout=45) as response, temporary.open('wb') as writer:
                total=0
                while chunk:=response.read(1024*1024):
                    total+=len(chunk)
                    assert total<=268435456,'Archive exceeds finite bound'
                    writer.write(chunk)
            assert hashlib.sha256(temporary.read_bytes()).hexdigest()==expected,'Archive digest mismatch'
            temporary.replace(target)
        assert hashlib.sha256(target.read_bytes()).hexdigest()==expected
        with tarfile.open(target) as archive:
            matches=[m for m in archive.getmembers() if m.name.removeprefix('./')==name]
            assert len(matches)==1 and matches[0].isfile() and 0<matches[0].size<=268435456
            data=archive.extractfile(matches[0]).read(268435457)
            assert len(data)==matches[0].size
        binary=directory/name
        assert not binary.is_symlink()
        binary.write_bytes(data);binary.chmod(0o700)
        records.append({'name':name,'repository':repository,'version':version,'archiveSha256':expected,'binarySha256':hashlib.sha256(data).hexdigest(),'reusedVerifiedArchive':reused,'durationSeconds':round(time.monotonic()-started,3)})
        print(json.dumps({'preparedBinary':name,'archiveVerified':True,'reused':reused}),flush=True)
assert subprocess.check_output([str(home/'.dapr/bin/daprd'),'--version'],text=True).strip()==runtime
record={'schema':'hexalith.story-2-10.runtime-preparation.v1','recordedAtUtc':datetime.now(timezone.utc).isoformat(),'catalogSha256':hashlib.sha256(catalog_blob).hexdigest(),'catalogCheckoutSha256':hashlib.sha256(catalog.read_bytes()).hexdigest(),'checkoutFormatting':'clean CRLF checkout; normalized bytes equal committed catalog','catalogSource':'5ec1779040606be89306129a7d2b245258c9acd2','daprHome':str(home),'cliVersion':cli,'runtimeVersion':runtime,'binaries':records,'servicesStarted':False,'shellConfigurationChanged':False,'qualificationCredit':False}
(out/'runtime-preparation.json').write_text(json.dumps(record,indent=2)+'\n')
print('Verified isolated Dapr runtime prepared.',flush=True)
