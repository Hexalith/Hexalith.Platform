"""Bounded target-only PostgreSQL restore primitives; no CLI or implicit authority.

An authenticated, source-bound caller must create/fence the fresh target, verify
exact immutable readback and authorise the complete scope. These functions do
not create resources, grant source access, upload, resume or accept recovery.
"""
import hashlib
import json
import os
import selectors
import signal
import subprocess
import time
import telemetry_postgres_adapter as adapter
from telemetry_capture import MAX_ARTIFACT, Session, require


def bounded_run(argv, data=b'', *, timeout=120, maximum=4*1024*1024):
    """Own all pipes while delivering at most the qualified64MiB stdin bound."""
    require(type(data) is bytes and len(data) <= MAX_ARTIFACT and 0 < timeout <= 120
            and 0 < maximum <= MAX_ARTIFACT, 'restore-command-input-bound')
    child = subprocess.Popen(argv, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                             stderr=subprocess.PIPE, start_new_session=True, env={'LANG':'C.UTF-8'})
    deadline, sent, stdout, stderr = time.monotonic()+timeout, 0, bytearray(), bytearray()
    selected = selectors.DefaultSelector()
    primary = None
    try:
        for stream,label in ((child.stdout,'stdout'),(child.stderr,'stderr')):
            os.set_blocking(stream.fileno(),False);selected.register(stream,selectors.EVENT_READ,label)
        if data:
            os.set_blocking(child.stdin.fileno(),False);selected.register(child.stdin,selectors.EVENT_WRITE,'stdin')
        else:child.stdin.close()
        while selected.get_map():
            require(time.monotonic()<deadline,'restore-command-deadline')
            for key,_ in selected.select(min(.1,max(0,deadline-time.monotonic()))):
                if key.data=='stdin':
                    sent += os.write(key.fd,data[sent:sent+65536])
                    if sent==len(data):selected.unregister(child.stdin);child.stdin.close()
                    continue
                target,limit=(stdout,maximum) if key.data=='stdout' else (stderr,65536)
                chunk=os.read(key.fd,min(65536,limit-len(target)+1))
                if not chunk:selected.unregister(key.fileobj);continue
                target.extend(chunk);require(len(target)<=limit,'restore-command-output-bound')
        child.wait(timeout=max(.001,deadline-time.monotonic()))
        return subprocess.CompletedProcess(argv,child.returncode,bytes(stdout),bytes(stderr))
    except BaseException as exc:
        primary=exc
        raise
    finally:
        failures=[]
        if child.poll() is None:
            try:os.killpg(child.pid,signal.SIGKILL)
            except ProcessLookupError:pass
            except BaseException as exc:failures.append(exc)
        try:child.wait(timeout=5)
        except BaseException as exc:failures.append(exc)
        selected.close()
        for stream in (child.stdin,child.stdout,child.stderr):
            try:stream.close()
            except BaseException as exc:failures.append(exc)
        if failures:raise BaseExceptionGroup('restore-command-primary-and-cleanup',([primary] if primary else [])+failures)


def restore(value, binding, target_prefix, ciphertext, age, identity, source_canary,
            authorize, observe_target):
    """Restore only the exact new target already validated by restore_command."""
    adapter.validate(value)
    command=adapter.restore_command(value,binding)
    require(callable(authorize) and callable(observe_target),'restore-integration-gate-missing')
    require(authorize('telemetry-restore') is True,'restore-authentication-or-fence-refused')
    require(observe_target()==binding,'restore-current-target-binding-drift')
    artifact=adapter.c.Artifact.parse(binding['artifact'])
    require(type(ciphertext) is bytes and len(ciphertext)==artifact.size
            and hashlib.sha256(ciphertext).hexdigest()==artifact.sha256,'restore-ciphertext-binding-mismatch')
    # Both age and pg_restore are exact caller-bound executables. Decrypted
    # bytes exist only in bounded operator memory and target stdin.
    result=bounded_run([str(age),'-d','-i',str(identity)],ciphertext,maximum=MAX_ARTIFACT,timeout=30)
    require(result.returncode==0 and hashlib.sha256(result.stdout).hexdigest()==binding['decryptedStdinSha256']
            and len(result.stdout)==binding['decryptedStdinSize'],'restore-age-readback-or-plaintext-digest-refused')
    proposal=adapter.render_proposal(value)
    require(authorize('telemetry-target-roles') is True and observe_target()==binding,'restore-target-authority-drift')
    roles=bounded_run(target_prefix+proposal['restore']['targetRoleArgv'],
                      proposal['restore']['targetRoleSql'].encode(),timeout=30)
    require(roles.returncode==0,'restore-target-roles-refused')
    require(authorize('telemetry-pg-restore') is True and observe_target()==binding,'restore-target-authority-drift')
    restored=bounded_run(target_prefix+command['argv'],result.stdout,timeout=120)
    require(restored.returncode==0,'restore-target-pg-restore-refused')
    # The target is no longer empty after restore; the caller must compare
    # immutable UID/image/storage identity and report that measured transition.
    after=observe_target()
    require({k:v for k,v in after.items() if k!='emptySchema'}==
            {k:v for k,v in binding.items() if k!='emptySchema'},'restore-post-target-identity-drift')
    canary=bounded_run(target_prefix+proposal['restore']['canaryArgv'],
                       proposal['restore']['canarySql'].encode(),timeout=60)
    require(canary.returncode==0,'restore-target-canary-refused')
    comparison=adapter.compare_canaries(value,source_canary,canary.stdout)
    return {'state':'isolated-target-restore-comparison-only','targetCanaryTranscript':canary.stdout,
            'comparison':comparison,'plaintextSha256':hashlib.sha256(result.stdout).hexdigest(),
            'plaintextBytes':len(result.stdout),'decryptedStdinMatchesCapture':True,
            'restoreCommandSucceeded':True,'completeTargetCanaryCaptured':True,
            'sourceResumeExecuted':False,'targetCleanupExecuted':False,'recoveryAccepted':False}
