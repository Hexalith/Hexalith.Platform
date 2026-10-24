from pathlib import Path
import importlib.util, sys, os, dis, resource, json

path = Path('eng/recovery-preparation/test_native_client_certificate.py').absolute()
spec = importlib.util.spec_from_file_location('blind_review_probe', path)
t = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = t
spec.loader.exec_module(t)
b = t.b
results = {}
case = t.CertificateTests()
case.setUp()
try:
    stores = {i.offset for i in dis.get_instructions(b.NativeTLS.exchange)
        if i.opname == 'STORE_ATTR' and i.argval == 'stream'}
    fired = []
    def trace(frame, event, argument):
        if frame.f_code is b.NativeTLS.exchange.__code__:
            frame.f_trace_opcodes = True
            if event == 'opcode' and frame.f_lasti in stores and not fired:
                fired.append(True)
                raise KeyboardInterrupt('connector result transfer')
        return trace
    try:
        sys.settrace(trace)
        case.exchange()
    except KeyboardInterrupt:
        pass
    finally:
        sys.settrace(None)
    results['stream_transfer'] = {'injected': bool(fired), 'streamClosed': case.streams[-1].closed}

    custody = b._DescriptorCustody()
    allocation = b._MemoryAllocation()
    custody.allocations.append(allocation)
    fd = os.memfd_create(allocation.name, os.MFD_CLOEXEC | os.MFD_ALLOW_SEALING)
    allocation.observe(fd)
    original = custody.register
    def register(raw):
        os.close(raw)
        replacement = os.open('/dev/null', os.O_RDONLY)
        if replacement != raw:
            os.dup2(replacement, raw)
            os.close(replacement)
        return original(raw)
    custody.register = register
    b._close_owned(custody)
    try:
        os.fstat(fd)
        wrong_close = False
        os.close(fd)
    except OSError:
        wrong_close = True
    results['recovery_registration_race'] = {'replacementWronglyClosed': wrong_close}

    old = KeyboardInterrupt('caller previously handled interruption')
    current = KeyboardInterrupt('current loading interruption')
    try:
        raise old
    except BaseException:
        with t.patch.object(t.ssl.SSLContext, 'load_cert_chain', side_effect=current):
            try:
                case.exchange()
            except BaseException as error:
                results['exception_context'] = {'oldResurfaced': error is old, 'currentPreserved': error is current}

    stream = t.f.Stream(t.f.http(b'{}'))
    calls = []
    def close():
        calls.append(True)
        if len(calls) < 3:
            raise KeyboardInterrupt('close interruption ' + str(len(calls)))
        stream.closed = True
    stream.close = close
    try:
        case.exchange(connector=lambda *arguments: stream)
    except BaseException:
        pass
    results['two_close_interruptions'] = {'closeAttempts': len(calls), 'streamClosed': stream.closed}

    stream = t.f.Stream(t.f.http(b'{}'))
    fd = os.open('/dev/null', os.O_RDONLY)
    calls = []
    def completed_close():
        calls.append(True)
        os.close(fd)
        if len(calls) == 1:
            replacement = os.open('/dev/null', os.O_RDONLY)
            if replacement != fd:
                os.dup2(replacement, fd)
                os.close(replacement)
            raise KeyboardInterrupt('after completed stream close')
        stream.closed = True
    stream.close = completed_close
    try:
        case.exchange(connector=lambda *arguments: stream)
    except BaseException:
        pass
    try:
        os.fstat(fd)
        wrong_close = False
        os.close(fd)
    except OSError:
        wrong_close = True
    results['non_idempotent_stream_close'] = {'closeAttempts': len(calls), 'replacementWronglyClosed': wrong_close}
finally:
    case.doCleanups()

custody = b._DescriptorCustody()
before = t.descriptors()
instructions = list(dis.get_instructions(b.sealed))
target = next(i.offset for previous, i in zip(instructions, instructions[1:])
    if previous.opname == 'CALL' and i.opname == 'STORE_FAST' and i.argval == 'low')
fired = []
def trace(frame, event, argument):
    if frame.f_code is b.sealed.__code__:
        frame.f_trace_opcodes = True
        if event == 'opcode' and frame.f_lasti == target and not fired:
            fired.append(True)
            raise KeyboardInterrupt('raw allocation transfer')
    return trace
limit = resource.getrlimit(resource.RLIMIT_NOFILE)
resource.setrlimit(resource.RLIMIT_NOFILE, (max(before) + 2, limit[1]))
try:
    sys.settrace(trace)
    try:
        b.sealed(b'public synthetic bytes', custody=custody)
    except BaseException:
        pass
    finally:
        sys.settrace(None)
finally:
    resource.setrlimit(resource.RLIMIT_NOFILE, limit)
leaked = set(t.descriptors()) - set(before)
results['descriptor_exhaustion'] = {'injected': bool(fired), 'newDescriptors': sorted(leaked), 'registeredOwners': len(custody.handles)}
for fd in leaked:
    os.close(fd)
print(json.dumps(results, indent=2))
