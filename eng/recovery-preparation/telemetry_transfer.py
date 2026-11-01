"""Single-attempt exact-version encrypted telemetry upload/readback primitives.

Authenticated original policy+signature readback must precede source capture.
This integration has no credentials/CLI and never changes bucket policy, grants,
retention or object data after PUT. An uncertain PUT never triggers a retry.
NEW capture.capturedAt is exact-version provider LastModified durability time;
actual cutoff, dump completion, readback completion and source observation remain
separate factual events, including their original fractional precision.
"""
import base64,hashlib,json,math,os,re,stat,time
from datetime import datetime,timezone,timedelta
from pathlib import Path


def require(ok,why):
 if not ok:raise ValueError(why)

def timestamp(value):
 require(type(value) is str and re.fullmatch(
     r'\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?(?:Z|\+00(?::00)?)',value),
     'telemetry-transfer-time-refused')
 result=datetime.fromisoformat(value.replace('Z','+00:00'))
 require(result.tzinfo is not None,'telemetry-transfer-time-refused');return result

def iso(value):
 require(isinstance(value,datetime) and value.tzinfo is not None,'telemetry-provider-time-refused')
 return value.astimezone(timezone.utc).isoformat().replace('+00:00','Z')

def utcnow():return datetime.now(timezone.utc)

def json_record(value):
 return json.loads(json.dumps(value,default=lambda v:iso(v)))


def wait_provider_order(minimum,provider_clock,*,timeout=12):
 """Wait real time; never round/backdate cutoff or fabricate provider precision."""
 require(isinstance(minimum,datetime) and minimum.tzinfo is not None and callable(provider_clock)
         and 0<timeout<=12,'telemetry-provider-clock-input-refused')
 deadline=time.monotonic()+timeout;observations=[]
 while True:
  actual=provider_clock()
  require(type(actual) is dict and set(actual)=={'providerDate','requestStartedAt','responseObservedAt'},
          'telemetry-provider-clock-observation-refused')
  server,before,after=(timestamp(actual[k]) for k in ('providerDate','requestStartedAt','responseObservedAt'))
  require(before<=after and before-timedelta(seconds=2)<=server<=after+timedelta(seconds=2),
          'telemetry-provider-clock-skew-refused')
  observations.append(actual)
  if server>=minimum+timedelta(seconds=2):return observations
  require(time.monotonic()<deadline and len(observations)<24,'telemetry-provider-clock-order-timeout')
  time.sleep(.25)


def upload(value,writer,validator,ciphertext,expected,minimum_events,provider_clock,observe_source,
           record_private,authorize):
 """PUT once, then actual validator HEAD/GET/retention for the exact returned version.

The supplied SDK clients must have verified fixed endpoint/region/credential
custody and total_max_attempts=1. provider_clock performs an authenticated
read-only exact policy-version HEAD, preserving its provider Date and local
request brackets. observe_source performs an actual bound read-only PG sample.
 """
 import telemetry_postgres_adapter as adapter
 adapter.validate(value)
 require(all(callable(v) for v in (provider_clock,observe_source,record_private,authorize))
         and authorize('telemetry-artifact-upload') is True,'telemetry-transfer-authentication-refused')
 for client in (writer,validator):
  require(getattr(client.meta.config,'retries',{}).get('total_max_attempts')==1
          and client.meta.endpoint_url=='https://s3.fr-par.scw.cloud'
          and client.meta.region_name=='fr-par',
          'telemetry-transfer-sdk-retry-profile-refused')
 require(type(expected) is dict and set(expected)=={'ciphertextBytes','ciphertextSha256','plaintextBytes','plaintextSha256'}
         and all(type(expected[k]) is int and 0<expected[k]<=65*1024*1024 for k in ('ciphertextBytes','plaintextBytes'))
         and all(isinstance(expected[k],str) and re.fullmatch('[a-f0-9]{64}',expected[k]) for k in ('ciphertextSha256','plaintextSha256')),
         'telemetry-transfer-artifact-binding-refused')
 require(type(minimum_events) is dict and set(minimum_events)=={'dataCutoff','proposalObservedAt','localDumpCompletedAt'},
         'telemetry-transfer-event-timestamps-required')
 times={k:timestamp(v) for k,v in minimum_events.items()}
 require(times['dataCutoff']<=times['localDumpCompletedAt'] and minimum_events['proposalObservedAt']==value['observedAt'],
         'telemetry-transfer-source-event-order-refused')
 clocks=wait_provider_order(max(times.values()),provider_clock)
 require(record_private('telemetry-provider-put-ordering',clocks) is True,'telemetry-transfer-ordering-custody-refused')
 path=Path(ciphertext);info=path.lstat();parent=path.parent.lstat()
 require(stat.S_ISREG(info.st_mode) and not path.is_symlink() and info.st_uid==os.getuid() and info.st_nlink==1
         and stat.S_IMODE(info.st_mode)==0o600 and stat.S_ISDIR(parent.st_mode) and parent.st_uid==os.getuid()
         and stat.S_IMODE(parent.st_mode)==0o700,'telemetry-transfer-private-artifact-custody-refused')
 fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW);digest=hashlib.sha256();md5=hashlib.md5();size=0
 try:
  opened=os.fstat(fd);require((opened.st_dev,opened.st_ino)==(info.st_dev,info.st_ino),'telemetry-transfer-local-artifact-replaced')
  while True:
   raw=os.read(fd,1048576)
   if not raw:break
   size+=len(raw);require(size<=65*1024*1024,'telemetry-transfer-artifact-size-bound');digest.update(raw);md5.update(raw)
  require(size==expected['ciphertextBytes'] and digest.hexdigest()==expected['ciphertextSha256'],
          'telemetry-transfer-ciphertext-size-or-hash-refused')
  key=value['transfer']['key'];bucket='hexalith-recovery-points'
  require(value['transfer']['store']=='scaleway-fr-par-hexalith-recovery-points',
          'telemetry-transfer-approved-store-refused')
  require(re.fullmatch(r'memories/[0-9]{8}t[0-9]{6}z/telemetry-postgresql\.dump\.age',key),
          'telemetry-transfer-exact-artifact-key-refused')
  existing=validator.list_object_versions(Bucket=bucket,Prefix=key,MaxKeys=1000)
  require(not existing.get('IsTruncated') and not any(x['Key']==key for name in ('Versions','DeleteMarkers') for x in existing.get(name,[])),
          'telemetry-transfer-key-version-preexists')
  require(authorize('telemetry-artifact-put-once') is True,'telemetry-transfer-authentication-refused')
  intent={'bucket':bucket,'key':key,'expected':expected,'minimumEvents':minimum_events,'transport':'one PUT only; uncertain response is a stop'}
  require(record_private('telemetry-artifact-put-intent',intent) is True,'telemetry-transfer-intent-custody-refused')
  os.lseek(fd,0,os.SEEK_SET)
  with os.fdopen(os.dup(fd),'rb') as body:
   # Provider-supported atomic first write, also qualified by the mandatory
   # signed-policy publication before any source capture. No unconditional retry.
   # https://www.scaleway.com/en/docs/object-storage/api-cli/using-conditional-writes/
   put=writer.put_object(Bucket=bucket,Key=key,Body=body,ContentLength=size,
        ContentMD5=base64.b64encode(md5.digest()).decode(),IfNoneMatch='*',ServerSideEncryption='AES256',ContentType='application/octet-stream',
        Metadata={'sha256':expected['ciphertextSha256'],'recovery-id':key.split('/')[1],'client-encryption':'age-two-ssh-recipients'})
  version=put.get('VersionId');require(isinstance(version,str) and version,'telemetry-transfer-put-outcome-unverified')
  require(record_private('telemetry-artifact-put-response',json_record({'key':key,'versionId':version,'response':put})) is True,
          'telemetry-transfer-put-response-custody-refused')
  head=validator.head_object(Bucket=bucket,Key=key,VersionId=version)
  fetched=validator.get_object(Bucket=bucket,Key=key,VersionId=version)
  rb_hash=hashlib.sha256();rb_bytes=0
  try:
   for raw in iter(lambda:fetched['Body'].read(1048576),b''):
    rb_bytes+=len(raw);require(rb_bytes<=size,'telemetry-transfer-readback-size-bound');rb_hash.update(raw)
  finally:fetched['Body'].close()
  lock=validator.get_object_retention(Bucket=bucket,Key=key,VersionId=version)['Retention']
  captured=head.get('LastModified');retain=head.get('ObjectLockRetainUntilDate')
  require(head.get('VersionId')==fetched.get('VersionId')==version and head.get('ContentLength')==rb_bytes==size
          and fetched.get('ContentLength')==size and rb_hash.hexdigest()==expected['ciphertextSha256']
          and fetched.get('LastModified')==captured
          and head.get('ServerSideEncryption')==fetched.get('ServerSideEncryption')=='AES256'
          and head.get('ObjectLockMode')==lock.get('Mode')=='COMPLIANCE'
          and isinstance(captured,datetime) and captured.tzinfo is not None
          and isinstance(retain,datetime) and retain.tzinfo is not None
          and lock.get('RetainUntilDate')==retain and retain>=captured+timedelta(days=30)
          and captured>=max(times.values()),'telemetry-transfer-exact-version-protection-time-or-readback-refused')
  durability_verified=utcnow()
  local_source_before=utcnow()
  actual=observe_source()
  local_source_after=utcnow()
  require(type(actual) is dict and 'sourceObservedAt' in actual,'telemetry-transfer-source-observation-required')
  observed=timestamp(actual['sourceObservedAt']);rpo=math.ceil((observed-times['dataCutoff']).total_seconds())
  require(observed>=captured>=times['dataCutoff'] and 0<=rpo<=1800
          and local_source_before-timedelta(seconds=2)<=observed<=local_source_after+timedelta(seconds=2),
          'telemetry-transfer-rpo-or-source-clock-order-refused')
  end=os.fstat(fd)
  require(all(getattr(info,k)==getattr(end,k) for k in ('st_dev','st_ino','st_size','st_mtime_ns','st_ctime_ns')),
          'telemetry-transfer-local-artifact-drift')
  result={'key':key,'versionId':version,'bytes':size,'sha256':expected['ciphertextSha256'],'contentLength':size,
          'readBackSha256':rb_hash.hexdigest(),'readBy':'validator','serverSideEncryption':'AES256',
          'objectLockMode':'COMPLIANCE','objectLockRetainUntil':iso(retain),
          'retentionApi':{'mode':'COMPLIANCE','retainUntil':iso(retain)},'capturedAt':iso(captured),
          'capturedAtBasis':'authenticated exact-version provider LastModified durability time',
          'dataCutoff':minimum_events['dataCutoff'],'localDumpCompletedAt':minimum_events['localDumpCompletedAt'],
          'durabilityVerifiedAt':iso(durability_verified),'expiresAt':iso(captured+timedelta(seconds=86400)),
          'sourceObservationLocalBracket':{'before':iso(local_source_before),'after':iso(local_source_after)},
          'measuredRpo':{'seconds':rpo,'withinPolicy':True,'sourceObservedAt':actual['sourceObservedAt']},
          'sourceObservation':actual,'putAttempts':1,'sourceEventTimestampsUnchanged':True}
  require(record_private('telemetry-artifact-exact-version-readback',json_record({'result':result,'head':head,'retention':lock})) is True,
          'telemetry-transfer-readback-custody-refused')
  return result
 finally:os.close(fd)
