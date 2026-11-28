"""Read the Builds version pin; the gitlink and the generated tool lock supply every other identity."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
BINDING = ROOT / "eng/builds-tuple.json"
GITLINK = "references/Hexalith.Builds"
CATALOG = "Props/Directory.Packages.props"
TOOL_PACKAGE = "Hexalith.Builds.Module.Cli"
# A new major generation may still need code; its tool framework is a behavior fact, not a release identity.
GENERATIONS = {6: "net11.0"}
RELEASED_VERSION = re.compile(r"(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})\.(0|[1-9][0-9]{0,8})")


def resolve_builds_root(platform_root):
    """Prefer the existing workspace producer; callers must still verify its identity."""
    sibling = platform_root.parent / "Hexalith.Builds"
    if (sibling / CATALOG).is_file():
        return sibling
    return platform_root / GITLINK


def load_binding(path=BINDING):
    return validate_binding(json.loads(Path(path).read_bytes()))


def validate_binding(binding):
    """Schema 2 pins the released version and tag only; hashes and IDs never enter the binding."""
    if not isinstance(binding, dict) or set(binding) != {"schemaVersion", "version", "releaseTag"} \
            or binding["schemaVersion"] != 2:
        raise ValueError("The Builds binding must be schema 2 with exactly schemaVersion, version and releaseTag.")
    version = binding["version"]
    if not isinstance(version, str) or RELEASED_VERSION.fullmatch(version) is None:
        raise ValueError("The Builds binding requires an exact released MAJOR.MINOR.PATCH version, not " + repr(version) + ".")
    if int(version.split(".")[0]) not in GENERATIONS:
        raise ValueError("Builds " + version + " is outside the supported major generation; a new generation needs code.")
    if binding["releaseTag"] != "v" + version:
        raise ValueError("The Builds release tag must be v" + version + ", not " + repr(binding["releaseTag"]) + ".")
    return binding


def tool_framework(version):
    framework = GENERATIONS.get(int(version.split(".")[0]))
    if framework is None:
        raise ValueError("Builds " + version + " is outside the supported major generation; a new generation needs code.")
    return framework


def tool_version_range(version):
    """The admitted range runs from the pin to the next major generation."""
    return "[" + version + ", " + str(int(version.split(".")[0]) + 1) + ".0.0-0)"


def validate_owner_pins(binding, root):
    """The tools pin, the binding version and the range floor are one decision."""
    version = binding["version"]
    try:
        pinned = json.loads((root / ".config/dotnet-tools.json").read_bytes())["tools"]["hexalith.builds.module.cli"]["version"]
        selected = json.loads((root / "hexalith-platform.json").read_bytes())["toolVersionRange"]
    except (OSError, KeyError, TypeError, ValueError) as error:
        raise ValueError("The owner tool pin and range floor are unreadable.") from error
    if pinned != version or selected != tool_version_range(version):
        raise ValueError("The tools pin " + repr(pinned) + ", the binding version " + repr(version)
                         + " and the range " + repr(selected) + " differ; expected range " + tool_version_range(version) + ".")


def recorded_gitlink(root, revision="HEAD", environment=None):
    """Read the Builds gitlink from the recorded Platform tree, never from a checkout."""
    entry = subprocess.check_output(["git", "ls-tree", revision, "--", GITLINK], cwd=root, text=True,
                                    **({"env": environment} if environment is not None else {})).strip().split()
    if len(entry) != 4 or entry[:2] != ["160000", "commit"] or entry[3] != GITLINK \
            or re.fullmatch(r"[0-9a-f]{40}", entry[2]) is None:
        raise ValueError("Recorded Platform source must declare its actual Builds catalog gitlink.")
    return entry[2]


def verify_catalog(gitlink, builds_root, environment=None):
    """Read the catalog from the gitlink blob; the checkout must be that commit with unchanged catalog bytes.

    Returns the checkout path, the committed body and its identity, allowing Git's configured CRLF conversion.
    """
    def git(*arguments):
        return subprocess.check_output(["git", *arguments], cwd=builds_root, **({"env": environment} if environment is not None else {}))

    if git("rev-parse", "HEAD").decode().strip() != gitlink:
        raise ValueError("Builds checkout differs from the recorded Builds gitlink " + gitlink + ".")
    blob = git("rev-parse", gitlink + ":" + CATALOG).decode().strip()
    body = git("cat-file", "blob", blob)
    comparison = subprocess.run(["git", "diff", "--exit-code", "--end-of-options", gitlink, "--", CATALOG],
                                cwd=builds_root, capture_output=True, text=True,
                                **({"env": environment} if environment is not None else {}))
    if comparison.returncode:
        observation = {"argv": comparison.args, "cwd": str(builds_root), "exitCode": comparison.returncode,
                       "stdout": comparison.stdout, "stderr": comparison.stderr}
        raise ValueError("Builds catalog checkout comparison failed: " + json.dumps(observation, sort_keys=True))
    identity = {"path": CATALOG, "sourceRevision": gitlink, "blob": blob, "sha256": hashlib.sha256(body).hexdigest()}
    return Path(builds_root) / CATALOG, body, identity


def main():
    """Validate the pins and print the recorded Builds gitlink that the checkout must equal."""
    binding = load_binding()
    validate_owner_pins(binding, ROOT)
    print(recorded_gitlink(ROOT))


if __name__ == "__main__":
    main()
