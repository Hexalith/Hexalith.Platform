import json, pathlib, subprocess, sys
root = pathlib.Path(__file__).parent
sys.exit(subprocess.call([sys.executable, str(root / 'run-check.py'), 'module-final', *json.loads((root / 'module-final-argv.json').read_text())]))
