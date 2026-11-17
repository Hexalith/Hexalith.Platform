# /// script
# requires-python = ">=3.12"
# dependencies = ["Jinja2==3.1.6", "PyYAML==6.0.3", "jsonschema==4.26.0"]
# ///
import importlib.util, pathlib, sys
owner=pathlib.Path(sys.argv[1])
spec=importlib.util.spec_from_file_location("normal_global_checks",owner/"tools/check_global.py")
checks=importlib.util.module_from_spec(spec);sys.modules[spec.name]=checks;spec.loader.exec_module(checks)
checks.check_bootstrap(owner)
print("normal immutable delivery bootstrap: passed",flush=True)
with checks.selected_contract(owner) as contract:
 checks.check_release(contract)
print("normal acceptable Builds publication: passed",flush=True)
