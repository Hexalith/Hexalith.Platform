#!/usr/bin/env python3
"""Finite offline trace controls; static synthetic credentials and zero sockets.

Single line/opcode injection requires the exact injected interrupt, no descriptor
change, closed peers and no unraisable exception. Secondary line injection after
loading interruption requires the original primary throughout acquisition and
active cleanup. Terminal exchange propagation is reported separately only when
all resources were already released at injection. It promises no signal-atomic
terminal boundary, infinite cancellation handling or kernel-stall protection.
"""
import dis
import gc
import importlib.util
import inspect
import json
from pathlib import Path
import sys
from unittest.mock import patch

ROOT = Path.cwd()
PATH = ROOT / 'eng/recovery-preparation/test_native_client_certificate.py'
spec = importlib.util.spec_from_file_location('review4_certificate_sweeps', PATH)
t = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = t
spec.loader.exec_module(t)
b = t.b
source, start = inspect.getsourcelines(b.NativeTLS.exchange)
terminal = start + next(i for i, line in enumerate(source) if line.strip().startswith('except BaseException as exc:'))


def case():
    value = t.CertificateTests('test_exact_certificate_and_key_authenticate_real_peer_without_authorization')
    value.setUp()
    return value


def collect(mode):
    events = []
    primary = KeyboardInterrupt('synthetic sweep loading interruption')
    def record(frame, event, argument):
        if frame.f_code.co_filename == b.__file__:
            if mode == 'opcode':
                frame.f_trace_opcodes = True
            if event == ('opcode' if mode == 'opcode' else 'line') and (mode != 'secondary' or primary.__traceback__ is not None):
                events.append((frame.f_code.co_qualname, frame.f_lasti if mode == 'opcode' else frame.f_lineno))
        return record
    value = case()
    try:
        sys.settrace(record)
        if mode == 'secondary':
            with patch.object(t.ssl.SSLContext, 'load_cert_chain', side_effect=primary):
                value.exchange()
        else:
            value.exchange()
    except KeyboardInterrupt as exc:
        assert mode == 'secondary' and exc is primary
    finally:
        sys.settrace(None)
        value.doCleanups()
    return list(dict.fromkeys(events))


def sweep(mode):
    findings, terminal_events, unraisable = [], [], []
    targets = collect(mode)
    original_hook = sys.unraisablehook
    sys.unraisablehook = lambda event: unraisable.append(type(event.exc_value).__name__)
    try:
        for target in targets:
            value, hit, terminal_released = case(), [], []
            before = t.descriptors()
            primary = KeyboardInterrupt('synthetic sweep primary')
            secondary = SystemExit('synthetic sweep secondary')
            def trace(frame, event, argument):
                if frame.f_code.co_filename == b.__file__:
                    if mode == 'opcode':
                        frame.f_trace_opcodes = True
                    actual = (frame.f_code.co_qualname, frame.f_lasti if mode == 'opcode' else frame.f_lineno)
                    if not hit and event == ('opcode' if mode == 'opcode' else 'line') and actual == target and (mode != 'secondary' or primary.__traceback__ is not None):
                        hit.append(True)
                        is_terminal = mode == 'secondary' and frame.f_code is b.NativeTLS.exchange.__code__ and frame.f_lineno >= terminal
                        if is_terminal:
                            terminal_released.append(t.descriptors() == before and all(stream.closed for stream in value.streams))
                        raise secondary if mode == 'secondary' else primary
                return trace
            caught = None
            try:
                sys.settrace(trace)
                if mode == 'secondary':
                    with patch.object(t.ssl.SSLContext, 'load_cert_chain', side_effect=primary):
                        value.exchange()
                else:
                    value.exchange()
            except BaseException as exc:
                caught = exc
            finally:
                sys.settrace(None)
            unchanged = t.descriptors() == before
            closed = all(stream.closed for stream in value.streams)
            if terminal_released == [True]:
                terminal_events.append({'target': target, 'resourcesReleasedBeforeInjection': True,
                    'externalCancellationPropagated': caught is secondary})
                correct = caught is secondary
            else:
                correct = caught is primary
            if hit != [True] or not unchanged or not closed or not correct:
                findings.append({'target': target, 'injected': hit == [True], 'descriptorTablePreserved': unchanged,
                    'allPeersClosed': closed, 'exactPrimaryPreserved': caught is primary,
                    'exception': type(caught).__name__})
            value.doCleanups()
            caught = primary = secondary = None
            gc.collect()
    finally:
        sys.unraisablehook = original_hook
    return {'targets': len(targets), 'findings': findings, 'unraisableExceptions': unraisable,
        'terminalExternalCancellations': terminal_events}


if __name__ == '__main__':
    results = {mode: sweep(mode) for mode in ('line', 'secondary', 'opcode')}
    print(json.dumps(results, indent=2, sort_keys=True))
    raise SystemExit(1 if any(value['findings'] or value['unraisableExceptions'] for value in results.values()) else 0)
