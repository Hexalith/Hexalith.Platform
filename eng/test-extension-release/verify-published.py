"""Retain the signed NuGet archive and require the exact qualified package bodies."""
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import time
import urllib.error
import urllib.request

spec = importlib.util.spec_from_file_location("fixture_package", Path(__file__).with_name("verify-package.py"))
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("directory", type=Path)
parser.add_argument("version")
parser.add_argument("source")
args = parser.parse_args()
expected = json.loads((args.directory / "package-identity.json").read_text())
url = f"https://api.nuget.org/v3-flatcontainer/{package.PACKAGE.lower()}/{args.version}/{package.PACKAGE.lower()}.{args.version}.nupkg"
archive = args.directory / "published.nupkg"
deadline = time.monotonic() + 300
while True:
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            archive.write_bytes(response.read(20_000_001))
        if archive.stat().st_size > 20_000_000:
            raise ValueError("Published fixture archive exceeds its bound")
        break
    except urllib.error.HTTPError as error:
        if error.code != 404 or time.monotonic() >= deadline:
            raise
        time.sleep(10)
verification = subprocess.run(["dotnet", "nuget", "verify", str(archive), "--all"],
                              capture_output=True, text=True, timeout=60, check=True)
observed = package.inspect(archive, args.version, args.source)
if {key: value for key, value in observed.items() if key != "archiveSha256"} \
        != {key: value for key, value in expected.items() if key != "archiveSha256"}:
    raise ValueError("Signed publication differs from the exact qualified package bodies")
(args.directory / "published-identity.json").write_text(json.dumps({**observed, "url": url,
    "signatureVerification": verification.stdout, "candidateAcceptance": False, "storyAcceptance": False}, indent=2) + "\n")
