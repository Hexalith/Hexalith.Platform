import ast
import importlib.util
import io
import json
from pathlib import Path
import sys
import unittest

root = Path.cwd()
path = root / 'eng/recovery-preparation/test_native_client_certificate.py'
spec = importlib.util.spec_from_file_location('invalid_protocol_mutation_control', path)
tests = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = tests
spec.loader.exec_module(tests)
old = root / '_bmad-output/implementation-artifacts/evidence/epic-4/4-27/native-client-certificate-delivery/review-5-first-candidate/sources/recovery_execution_boundary.py'
tree = ast.parse(old.read_text())
connector = next(node for node in tree.body if isinstance(node, ast.ClassDef) and node.name == '_OwnedConnector')
method = next(node for node in connector.body if isinstance(node, ast.FunctionDef) and node.name == '__call__')
namespace = dict(tests.b.__dict__)
exec(compile(ast.Module(body=[method], type_ignores=[]), tests.b.__file__, 'exec'), namespace)
original = tests.b._OwnedConnector.__call__
tests.b._OwnedConnector.__call__ = namespace['__call__']
output = io.StringIO()
try:
    case = tests.CertificateTests('test_invalid_owned_stream_method_cancellation_preserves_primary_without_unraisable')
    result = unittest.TextTestRunner(stream=output, verbosity=2).run(unittest.TestSuite([case]))
finally:
    tests.b._OwnedConnector.__call__ = original
assert result.testsRun == 1 and not result.wasSuccessful()
print(output.getvalue())
print(json.dumps({'oldShortCircuitGeneratorRejected': True, 'testsRun': result.testsRun,
                  'expectedFailures': len(result.failures), 'expectedErrors': len(result.errors),
                  'workingSourceChanged': False}, sort_keys=True))
