import importlib.util
import inspect
import io
import json
from pathlib import Path
import sys
import time

path = Path.cwd() / 'eng/recovery-preparation/test_native_client_certificate.py'
spec = importlib.util.spec_from_file_location('parent_guard_probe', path)
t = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = t
spec.loader.exec_module(t)
case = t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization')
case.setUp()
outcomes = []
try:
    source, start = inspect.getsourcelines(t.b.NativeTLS.exchange)
    allocation = start + next(i for i, line in enumerate(source) if 'owned = object.__new__' in line)
    invocation = start + next(i for i, line in enumerate(source) if 'try: response = self._guard_native' in line)
    for target in ('allocation', 'invocation', 'guard-entry'):
        stream = t.f.Stream(t.f.http(b'{}'))
        stream.file = io.FileIO(t.b.sealed(b'public synthetic prepared peer'), 'rb', closefd=True)
        original_close = stream.close
        def close():
            stream.file.close()
            original_close()
        stream.close = close
        marker = KeyboardInterrupt('synthetic parent guard entry')
        fired = []
        def trace(frame, event, argument):
            hit = (target == 'guard-entry' and event == 'call' and frame.f_code is t.b.NativeTLS._guard_native.__code__) or (
                target != 'guard-entry' and event == 'line' and frame.f_code is t.b.NativeTLS.exchange.__code__ and
                frame.f_lineno == (allocation if target == 'allocation' else invocation))
            if hit and not fired:
                fired.append(True)
                raise marker
            return trace
        caught = None
        try:
            sys.settrace(trace)
            t.b.NativeTLS(t.f.owned_connector(stream)).exchange(case.request, time.monotonic() + 3, case.binding, case.credential)
        except BaseException as exc:
            caught = exc
        finally:
            sys.settrace(None)
        outcomes.append({'target': target, 'injected': fired == [True], 'exactInterrupt': caught is marker,
            'streamClosed': stream.closed, 'descriptorClosed': stream.file.closed})
        close()
finally:
    case.doCleanups()
print(json.dumps(outcomes, indent=2))
