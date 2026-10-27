"""Isolated cross-language verifier invoked by actual C# publisher/archive component tests.

It mutates only the caller's disposable fixture, never an installation or cohort.
"""
import hashlib
import json
import pathlib
import sys

from joint_authority_service import PersistedPhaseObserver
from joint_live_phase_runner import Blocked
from protected_logical_archive import read


def verify_fixture(root):
    root=pathlib.Path(root);inputs=json.loads((root/'cross-language-input.json').read_bytes());config=inputs['allocation'];request=inputs['request'];binding=inputs['binding']
    case=request['cases'][0];name=hashlib.sha256(case.encode()).hexdigest()+'.json';owner=pathlib.Path(inputs['ownerDirectory'])/request['requestId'];source=pathlib.Path(inputs['sourceDirectory'])/request['requestId']/name
    pin=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
    profile={'ownerOriginalAnchorFile':str(root/'owner.pem'),'ownerOriginalAnchorSha256':pin(root/'owner.pem'),'evidenceDirectory':inputs['ownerDirectory'],
             'protectedArchiveAllocations':[config],'sourceEvidence':{case:{'anchorFile':str(root/'source.pem'),'anchorSha256':pin(root/'source.pem'),'directory':inputs['sourceDirectory'],
             'invariants':dict.fromkeys(['CurrentMigrationEpoch','RepairFenceAtomicity','ExactBridgeCohort','SuccessorPreservesAllOwnerFacts'],{})}}}
    class Authority:
        def call(self,method,value):return {'current':True} if method=='current' else {'exact':True}
    observer=PersistedPhaseObserver(profile,binding,Authority())
    def observe():return observer.dispatch('lookup-phase-original',{'request':request,'binding':binding})
    paths=[owner/'owner-original.json',owner/name,source]
    originals={str(path):read(path,64*1024*1024,[config]) for path in paths}
    assert all(not path.exists() for path in paths)
    try:observe()
    except Blocked as error:assert 'ClosedNativePhaseSemanticsUnavailable' in error.missing,error.missing
    else:raise AssertionError('External complete native protocols still block phase qualification')
    # The actual observer reached closed invariant evaluation only after exact owner/case/source validation.
    for index,path in enumerate(paths):
        legacy=root/f'legacy-{index}';legacy.write_bytes(originals[str(path)]);assert read(legacy,64*1024*1024)==originals[str(path)]
    for fault in ('missing-trust','marker','metadata','body','locator'):
        if fault=='missing-trust':
            profile['protectedArchiveAllocations']=[];assert observe()['state']=='Unknown';profile['protectedArchiveAllocations']=[config];continue
        target=root/('.archive-allocation.json' if fault=='marker' else 'allocation-metadata-0.bin' if fault=='metadata' else 'allocation-data.bin')
        original=target.read_bytes()
        try:
            if fault=='marker':target.write_text('{}')
            elif fault=='metadata':
                with target.open('r+b') as output:output.write(bytes(8192));output.flush()
            else:
                import struct
                data=bytearray(original);stride=2*8192+config['ChunkBytes'];digest=hashlib.sha256(str(source).encode()).hexdigest();found=None
                for slot in range(config['ChunkCount']):
                    for copy in range(2):
                        offset=8192+slot*stride+copy*8192;length=struct.unpack_from('<i',data,offset)[0];frame=json.loads(data[offset+40:offset+40+length])
                        if frame.get('Path')==digest and frame['State']=='Published':found=(slot,offset)
                assert found is not None
                slot,offset=found
                if fault=='body':data[8192+slot*stride+2*8192]^=1
                else:data[offset:offset+8192]=bytes(8192)
                target.write_bytes(data)
            assert observe()['state']=='Unknown',fault
        finally:target.write_bytes(original)
    assert all(read(path,64*1024*1024,[config])==originals[str(path)] for path in paths)
    print('Actual C# protected owner/case/source originals verified; legacy and damaged/substituted refusal paths passed. Fixture only; complete native protocols remain blocked.')


if __name__=='__main__':verify_fixture(sys.argv[1])
