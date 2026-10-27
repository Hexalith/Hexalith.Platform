"""Disposable cross-language SQLite/signature fixture only; no live transport or installed inputs."""
import base64
import hashlib
import json
from pathlib import Path
import sqlite3
import sys
import time
import uuid

root, repository, mode = Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3]
if not root.name.startswith('story54-fixture-current-cohort-'):
    raise ValueError('DisposableFixtureDirectoryRequired')
sys.path.insert(0, str(repository/'eng/ext-secrets-1'))
from joint_authority_service import CohortAuthorityStore
from sqlite_storage_fixture import enrolled_storage
from contextlib import closing
from joint_live_phase_runner import canonical, digest, partitions, PHASES, PROTOCOL
from cryptography.hazmat.primitives import serialization, hashes
from cryptography.hazmat.primitives.asymmetric import ec
value=json.loads(sys.stdin.buffer.read())
database=root/'fixture.sqlite'
if mode=='describe':
    print(json.dumps({'partitions':partitions()}));sys.exit(0)
if mode=='construct-v2':
    from test_execution_profile import fixture_profile
    from execution_profile import validate_execution_inventory
    profiles={}
    for phase in range(4):
        profile=fixture_profile(root/('inventory-'+str(phase)),phase,partitions()[phase])
        installation=Path(value['installations'][str(phase)])
        profile['completionTimeoutSeconds']=json.loads(installation.read_bytes())['CompletionTimeoutSeconds']
        fixture=Path(profile['dependencyClosure']['installationFile']);fixture.unlink()
        profile['artifacts']=[item for item in profile['artifacts'] if item['path']!=str(fixture)]
        profile['dependencyClosure']['installationFile']=str(installation)
        profile['dependencyClosure']['configurationFiles']=[str(installation),str(root/'runtime.json')]
        profile['environment']['PLATFORM_JOINT_PHASE_COMMAND_INSTALLATION']=str(installation)
        for path in (installation,root/'runtime.json',root/'owner-public.pem'):
            profile['artifacts'].append({'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
        if validate_execution_inventory(profile):raise ValueError('ActualV2InstallationInventory')
        profiles[str(phase)]=profile
    binding={'platformCommit':value['platformCommit'],'phaseExecutionProfileSha256':{phase:digest(profile) for phase,profile in profiles.items()}}
    for profile in profiles.values():profile['bindingDigest']=digest(binding)
    print(json.dumps({'binding':binding,'profiles':profiles}));sys.exit(0)

if mode=='init':
    binding=value['binding']
    with closing(sqlite3.connect(database)) as db,db:
        db.executescript((repository/'eng/ext-secrets-1/joint-authority-schema.sql').read_text())
        db.execute('INSERT INTO accepted_sets VALUES(?,?,1)',(digest(binding),canonical(binding)))
        db.execute('INSERT INTO current_set VALUES(1,?)',(digest(binding),))
    storage=enrolled_storage(database)
    (root/'storage-enrollment.json').write_bytes(canonical(storage.enrollment))
    (root/'binding.json').write_bytes(canonical(binding))
    print(json.dumps({'bindingDigest':digest(binding)}))
else:
    binding=json.loads((root/'binding.json').read_bytes())
    from enrolled_sqlite import EnrolledSqliteStorage
    storage=EnrolledSqliteStorage(database,json.loads((root/'storage-enrollment.json').read_bytes()))
    store=CohortAuthorityStore(database,lambda phase,proof:proof.get('signature')=='isolated-fixture-role',storage=storage)
    if mode=='reserve':
        now=int(time.time())
        ready=[{'signature':'isolated-fixture-role','payload':{'bindingDigest':digest(binding),'phase':phase,'cases':partitions()[phase],
            'profileSha256':binding['phaseExecutionProfileSha256'][str(phase)],'observedAt':now,'validUntil':now+30}} for phase in range(4)]
        reserved=store.dispatch('reserve',{'binding':binding,'attemptId':str(uuid.uuid4()),'ownerReadiness':ready})
        attempt=reserved['attemptId']; previous=digest(binding)
        for phase in (0,1):
            request={'protocol':PROTOCOL,'attemptId':attempt,'bindingDigest':digest(binding),'phase':phase,'owner':PHASES[phase],
                'previousSha256':previous,'cases':partitions()[phase],'requestId':digest([attempt,digest(binding),phase,previous])}
            store.dispatch('retain',{'request':request})
            if phase==0:
                # Fixture control receipt only; no executable or physical phase is run.
                result={'signature':'isolated-fixture-role','payload':request|{'state':'Passed','passedCases':request['cases']}}
                store.dispatch('publish',{'request':request,'result':result});previous=digest(result)
        print(json.dumps(reserved|{"phaseRequest":request}))
    elif mode=='rpc':
        if value['method'] not in ('qualification-attempt','phase-admission'):raise ValueError('ReadOnlyFixtureProtocol')
        result=store.dispatch(value['method'],value['payload'])
        now=int(time.time())
        payload={'requestId':value['requestId'],'method':value['method'],'requestSha256':hashlib.sha256(canonical(value)).hexdigest(),
            'observedAt':now,'validUntil':now+30,'result':result}
        signer=serialization.load_pem_private_key((root/'fixture-key.pem').read_bytes(),None)
        print(json.dumps({'payload':payload,'signature':base64.b64encode(signer.sign(canonical(payload),ec.ECDSA(hashes.SHA256()))).decode()}))
    else:raise ValueError('FixtureMode')
