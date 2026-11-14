"""Read the exact producer/publication binding; incomplete preparation never qualifies a tuple."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BINDING = ROOT / "eng/builds-tuple.json"


def resolve_builds_root(platform_root):
    """Prefer the existing workspace producer; callers must still verify its identity."""
    sibling = platform_root.parent / "Hexalith.Builds"
    if (sibling / "Props/Directory.Packages.props").is_file():
        return sibling
    return platform_root / "references/Hexalith.Builds"


def load_binding(path=BINDING, require_publication=True):
    return validate_binding(json.loads(path.read_bytes()), require_publication)


def validate_binding(binding, require_publication=True):
    """Validate either the original inventory binding or the finite shared release handoff."""
    if binding.get("schemaVersion") != 1 or binding.get("candidateAcceptance") is not False:
        raise ValueError("Tuple binding must use schema 1 and candidateAcceptance:false.")
    producer = binding["producer"]
    catalog = producer["catalog"]
    if producer["repository"] != "https://github.com/Hexalith/Hexalith.Builds.git":
        raise ValueError("Tuple producer must be the official Builds repository.")
    for value in (producer["sourceRevision"], catalog["blob"]):
        if not isinstance(value, str) or not re.fullmatch(r"[0-9a-f]{40}", value):
            raise ValueError("Producer source and catalog blob require full lowercase Git identities.")
    if catalog["path"] != "Props/Directory.Packages.props" or not isinstance(catalog["sha256"], str) or not re.fullmatch(r"[0-9a-f]{64}", catalog["sha256"]):
        raise ValueError("Producer catalog path and SHA-256 are required.")
    if require_publication:
        publication = binding["publication"]
        common = {"version", "sourceRevision", "releaseTag", "catalogSha256", "archiveSha256", "toolingSha256"}
        shared = publication.get("kind") == "shared-release-handoff/v1"
        if "kind" in publication and not shared:
            raise ValueError("Unsupported official publication kind.")
        required = common | ({"kind", "releaseId", "runId", "artifactId", "artifactSha256", "releaseBodySha256",
                              "manifestJson", "manifestSha256"}
                             if shared else {"inventorySha256", "inventoryUrl"})
        missing = [key for key in required if not publication.get(key)]
        if missing:
            raise ValueError("Official publication binding is unavailable: " + ", ".join(missing) + ". Preparation is unqualified.")
        if set(publication) != required:
            raise ValueError("Unsupported official publication fields.")
        version = publication["version"]
        match = re.fullmatch(r"(?:4|5|6)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?", version) if isinstance(version, str) else None
        if not match or any(int(number) > 2147483647 for number in match.group(1, 2)) or any(identifier.isdigit() and len(identifier) > 1 and identifier.startswith("0") for identifier in (match.group(3) or "").split(".")):
            raise ValueError("Publication requires an exact supported 4.x NuGet semantic version or 5.x/6.x NET11 version with valid prerelease identifiers.")
        if version.startswith(("5.", "6.")) and not shared:
            raise ValueError("NET11 Builds " + version.split(".")[0] + " requires the shared release handoff; legacy inventory authority is unavailable.")
        if publication["sourceRevision"] != producer["sourceRevision"] or publication["releaseTag"] != "v" + version:
            raise ValueError("Publication source/tag differs from the exact producer binding.")
        if publication["catalogSha256"] != catalog["sha256"]:
            raise ValueError("Publication catalog differs from the prepared source catalog.")
        digests = ("archiveSha256", "toolingSha256") + (("artifactSha256", "releaseBodySha256", "manifestSha256") if shared else ("inventorySha256",))
        for key in digests:
            if not isinstance(publication[key], str) or not re.fullmatch(r"[0-9a-f]{64}", publication[key]):
                raise ValueError("Publication requires a lowercase SHA-256 for " + key + ".")
        if shared:
            for key in ("releaseId", "runId", "artifactId"):
                if type(publication[key]) is not int or publication[key] <= 0:
                    raise ValueError("Shared release requires a positive numeric " + key + ".")
            raw = publication["manifestJson"]
            if not isinstance(raw, str) or len(raw.encode()) > 4096 or hashlib.sha256(raw.encode()).hexdigest() != publication["manifestSha256"]:
                raise ValueError("Original shared release manifest bytes differ from their exact digest.")
            def unique(pairs):
                result = {}
                for key, value in pairs:
                    if key in result:
                        raise ValueError("Duplicate original shared release manifest key.")
                    result[key] = value
                return result
            try:
                manifest = json.loads(raw, object_pairs_hook=unique)
            except (ValueError, TypeError) as error:
                raise ValueError("Original shared release manifest is invalid.") from error
            expected = {"sha": producer["sourceRevision"], "version": version, "packages": [
                {"id": identity, "path": "packages/" + identity + "." + version + ".nupkg"}
                for identity in ("Hexalith.Builds.Evidence.Cli", "Hexalith.Builds.Module.Cli")], "containers": []}
            if manifest != expected:
                raise ValueError("Original shared release manifest source/version/package closure differs.")
        else:
            expected_inventory = "https://github.com/Hexalith/Hexalith.Builds/releases/download/" + publication["releaseTag"] + "/g4-tool-package-inventory.json"
            if publication["inventoryUrl"] != expected_inventory:
                raise ValueError("Qualification inventory requires the exact official release asset locator.")
    return binding


def validate_owner_pins(binding, root):
    version = binding["publication"]["version"]
    tools = json.loads((root / ".config/dotnet-tools.json").read_bytes())
    floor = json.loads((root / "hexalith-platform.json").read_bytes())
    upper = str(int(version.split(".")[0]) + 1) + ".0.0-0"
    if tools["tools"]["hexalith.builds.module.cli"]["version"] != version or floor["toolVersionRange"] != "[" + version + ", " + upper + ")":
        raise ValueError("Published tuple must match the owner tool pin and range floor.")


def validate_recorded_source(binding, pin, dirty, require_clean=True):
    if pin != binding["producer"]["sourceRevision"] or (require_clean and dirty):
        raise ValueError("CI requires clean recorded Platform source with the exact approved Builds pin.")


def verify_catalog(binding, builds_root):
    """Check committed bytes and the real checkout, allowing Git's configured CRLF conversion."""
    def git(*arguments):
        return subprocess.check_output(["git", *arguments], cwd=builds_root)

    producer = binding["producer"]
    catalog = producer["catalog"]
    if git("rev-parse", "HEAD").decode().strip() != producer["sourceRevision"]:
        raise ValueError("Builds checkout differs from the bound producer revision.")
    if git("rev-parse", "HEAD:" + catalog["path"]).decode().strip() != catalog["blob"]:
        raise ValueError("Recorded catalog blob differs from the producer binding.")
    if hashlib.sha256(git("show", "HEAD:" + catalog["path"])).hexdigest() != catalog["sha256"]:
        raise ValueError("Recorded catalog SHA-256 differs from the producer binding.")
    command = ["git", "diff", "--exit-code", "HEAD", "--", catalog["path"]]
    comparison = subprocess.run(command, cwd=builds_root, capture_output=True, text=True)
    if comparison.returncode:
        observation = {"argv": command, "cwd": str(builds_root), "exitCode": comparison.returncode,
                       "stdout": comparison.stdout, "stderr": comparison.stderr}
        raise ValueError("Builds catalog checkout comparison failed: " + json.dumps(observation, sort_keys=True))
    return builds_root / catalog["path"]


if __name__ == "__main__":
    print(load_binding()["producer"]["sourceRevision"])
