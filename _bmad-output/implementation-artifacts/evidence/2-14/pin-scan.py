"""Scan src/, eng/*.py and the docs for the current Builds pin's identities and for 40/64-hex Builds literals."""
import base64, hashlib, json, re, subprocess, sys, zipfile
from pathlib import Path
root = Path(sys.argv[1]); archive = Path(sys.argv[2])
version = json.loads((root / "eng/builds-tuple.json").read_text())["version"]
gitlink = subprocess.check_output(["git", "rev-parse", "HEAD:references/Hexalith.Builds"], cwd=root, text=True).strip()
builds = root / "references/Hexalith.Builds"
blob = subprocess.check_output(["git", "rev-parse", gitlink + ":Props/Directory.Packages.props"], cwd=builds, text=True).strip()
body = subprocess.check_output(["git", "cat-file", "blob", blob], cwd=builds)
raw = archive.read_bytes()
identities = {"version": version, "gitlink": gitlink, "gitlink8": gitlink[:8], "catalogBlob": blob, "catalogSha256": hashlib.sha256(body).hexdigest(),
              "archiveSha256": hashlib.sha256(raw).hexdigest(), "contentHash": base64.b64encode(hashlib.sha512(raw).digest()).decode()}
with zipfile.ZipFile(archive) as package:
    for name in package.namelist():
        if name.endswith(("Hexalith.Builds.Tooling.dll", "Hexalith.Builds.Module.AppHost.dll", "Hexalith.Builds.Module.EventStoreHost.dll")):
            identities[name] = hashlib.sha256(package.read(name)).hexdigest()
tags = subprocess.check_output(["git", "show-ref", "--tags", "-d"], cwd=builds, text=True).split()[::2]
files = [*sorted((root / "src").rglob("*.cs")), *sorted((root / "src").rglob("*.csproj")), *sorted((root / "eng").glob("*.py")),
         root / "README.md", *sorted((root / "docs").glob("*.md")), *sorted((root / "eng").glob("*.md"))]
hits = []
historical = []
for path in files:
    text = path.read_text(errors="replace")
    for key, value in identities.items():
        pattern = re.escape(value) if key != "version" else r"(?<![0-9.])" + re.escape(value) + r"(?![0-9])"
        for match in re.finditer(pattern, text):
            hits.append((str(path.relative_to(root)), key, text.count("\n", 0, match.start()) + 1))
    if path.suffix in (".cs", ".csproj") or path.parent.name == "eng" and path.suffix == ".py":
        for literal in set(re.findall(r"\b[0-9a-f]{40}\b", text)) & set(tags):
            historical.append((str(path.relative_to(root)), literal))
print(json.dumps({"scanned": len(files), "currentPin": identities, "currentPinHits": hits,
                  "historicalBuildsTagCommitLiterals": sorted(historical)}, indent=2))
sys.exit(1 if hits else 0)
