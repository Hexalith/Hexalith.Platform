"""Authenticate original4.0 policy+signature and their exact immutable versions.

No credential loading, policy publication, signature creation or source request
exists here. The qualified caller supplies the separate validator SDK client.
"""
import hashlib,json,os,re,subprocess,tempfile
from datetime import datetime,timezone,timedelta
from pathlib import Path
from telemetry_postgres_adapter import strict_json


def require(ok,why):
 if not ok:raise ValueError(why)

def serial_datetime(value):
 require(isinstance(value,datetime) and value.tzinfo is not None,'telemetry-policy-sdk-metadata-type-refused')
 return value.isoformat()

def verify_original(policy,signature,trust,trust_sha256,principal,ssh_keygen='/usr/bin/ssh-keygen'):
 require(all(type(v) is bytes and 0<len(v)<=2*1024*1024 for v in (policy,signature,trust))
         and hashlib.sha256(trust).hexdigest()==trust_sha256
         and isinstance(principal,str) and re.fullmatch(r'[A-Za-z0-9_.@+-]{1,128}',principal),
         'telemetry-policy-original-input-refused')
 with tempfile.TemporaryDirectory(prefix='s427-original-policy-verify-') as directory:
  root=Path(directory);root.chmod(0o700)
  for name,raw in [('trust',trust),('signature',signature)]:
   fd=os.open(root/name,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
   with os.fdopen(fd,'wb') as output:output.write(raw)
  result=subprocess.run([ssh_keygen,'-Y','verify','-f',str(root/'trust'),'-I',principal,
       '-n','hexalith-recovery','-s',str(root/'signature')],input=policy,capture_output=True,timeout=15,env={'LANG':'C.UTF-8'})
 require(result.returncode==0 and len(result.stdout)+len(result.stderr)<=16384,'telemetry-policy-original-signature-refused')
 return strict_json(policy)


def immutable_originals(policy,signature,trust,trust_sha256,principal,recovery_id,versions,
                        validator,record_private):
 """Read actual bytes/metadata for both original records before any source capture."""
 value=verify_original(policy,signature,trust,trust_sha256,principal)
 require(value.get('kind')=='hexalith.recovery-policy' and value.get('recoveryId')==recovery_id
         and type(versions) is dict and set(versions)=={'policy','signature'}
         and re.fullmatch(r'[0-9]{8}t[0-9]{6}z',recovery_id)
         and callable(record_private),'telemetry-policy-original-scope-refused')
 require(validator.meta.endpoint_url=='https://s3.fr-par.scw.cloud' and validator.meta.region_name=='fr-par'
         and validator.meta.config.retries.get('total_max_attempts')==1,'telemetry-policy-validator-profile-refused')
 prefix='evidence/4-0/'+recovery_id+'/policy/recovery-policy.json';observations={};retrieved={}
 for name,raw in [('policy',policy),('signature',signature)]:
  key=prefix+('.sig' if name=='signature' else '');version=versions[name]
  require(type(version) is str and 0<len(version)<=1024,'telemetry-policy-original-version-required')
  head=validator.head_object(Bucket='hexalith-recovery-points',Key=key,VersionId=version)
  response=validator.get_object(Bucket='hexalith-recovery-points',Key=key,VersionId=version)
  data=bytearray()
  try:
   while True:
    part=response['Body'].read(min(65536,len(raw)-len(data)+1))
    if not part:break
    data.extend(part);require(len(data)<=len(raw),'telemetry-policy-original-readback-size-bound')
  finally:response['Body'].close()
  lock=validator.get_object_retention(Bucket='hexalith-recovery-points',Key=key,VersionId=version)['Retention']
  created,retain=head.get('LastModified'),head.get('ObjectLockRetainUntilDate')
  require(head.get('VersionId')==response.get('VersionId')==version and bytes(data)==raw
          and head.get('ContentLength')==response.get('ContentLength')==len(raw)
          and head.get('ServerSideEncryption')==response.get('ServerSideEncryption')=='AES256'
          and head.get('ObjectLockMode')==lock.get('Mode')=='COMPLIANCE'
          and isinstance(created,datetime) and created.tzinfo is not None
          and isinstance(retain,datetime) and retain.tzinfo is not None
          and lock.get('RetainUntilDate')==retain and retain>=created+timedelta(days=30)
          and retain>datetime.now(timezone.utc),'telemetry-policy-original-immutable-readback-refused')
  retrieved[name]=bytes(data)
  observations[name]={'key':key,'versionId':version,'bytes':len(raw),'sha256':hashlib.sha256(data).hexdigest(),
       'lastModified':created.isoformat(),'retainUntil':retain.isoformat(),'head':head,'retention':lock}
 # Re-run the same original cryptographic verification on the actual off-node buffers.
 require(verify_original(retrieved['policy'],retrieved['signature'],trust,trust_sha256,principal)==value,
         'telemetry-policy-offnode-signature-refused')
 serial=json.loads(json.dumps(observations,default=serial_datetime))
 require(record_private('original-policy-and-signature-immutable-readback',serial) is True,
         'telemetry-policy-original-readback-custody-refused')
 return {'kind':'actual-original4.0-policy-and-signature-readback','recoveryId':recovery_id,
         'policySha256':hashlib.sha256(policy).hexdigest(),'signatureSha256':hashlib.sha256(signature).hexdigest(),
         'trustRootSha256':trust_sha256,'principal':principal,'records':serial,
         'verifiedAt':datetime.now(timezone.utc).isoformat(),'originalSignatureVerifiedBeforeAndAfterReadback':True,
         'sourceCaptureAuthorizedByThisReceiptAlone':False}
