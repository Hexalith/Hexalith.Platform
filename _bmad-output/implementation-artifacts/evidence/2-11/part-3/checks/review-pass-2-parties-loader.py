"""Use the permitted root-declared Builds source instead of initializing the empty nested checkout."""
import importlib.util
from pathlib import Path
import sys
import unittest
original = importlib.util.spec_from_file_location
root = Path('/home/administrator/projects/hexalith/platform')
def mapped(name, location, *args, **kwargs):
    location = str(location)
    missing = str(root / 'references/Hexalith.Parties/references/Hexalith.Builds') + '/'
    if location.startswith(missing):
        location = str(root / 'references/Hexalith.Builds') + '/' + location[len(missing):]
    return original(name, location, *args, **kwargs)
importlib.util.spec_from_file_location = mapped
suite = unittest.defaultTestLoader.discover('tests/ci-lifecycle', pattern=sys.argv[1] if len(sys.argv) > 1 else 'test*.py')
raise SystemExit(not unittest.TextTestRunner(verbosity=2).run(suite).wasSuccessful())
