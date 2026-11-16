#!/usr/bin/env bash
# Local scaffold validation and EXT-HOST-1 component qualification.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE=Full
CONFIGURATION=Debug
PROFILE=""
BUILDS_ROOT=""
ARTIFACTS_DIR="${TMPDIR:-/tmp}/hexalith-agents54-host-artifacts"
fail() {
  echo "ERROR: $*" >&2
  exit 1
}
while (($#)); do
  case "$1" in
    --configuration)
      (($# >= 2)) || fail "--configuration requires Debug or Release"
      CONFIGURATION="$2"
      shift 2
      ;;
    --mode)
      (($# >= 2)) || fail "--mode requires Full or LocalScaffold"
      MODE="$2"
      shift 2
      ;;
    --artifacts-path)
      (($# >= 2)) || fail "--artifacts-path requires a path"
      ARTIFACTS_DIR="$2"
      [[ -n "$ARTIFACTS_DIR" ]] || fail "artifacts path is empty"
      shift 2
      ;;
    --profile)
      (($# >= 2)) || fail "--profile requires a path"
      PROFILE="$2"
      [[ -n "$PROFILE" ]] || fail "profile path is empty"
      shift 2
      ;;
    --builds-root)
      (($# >= 2)) || fail "--builds-root requires the bound producer checkout"
      BUILDS_ROOT="$2"
      [[ -n "$BUILDS_ROOT" ]] || fail "bound producer path is empty"
      shift 2
      ;;
    *) fail "unknown argument: $1" ;;
  esac
done
case "$MODE" in
  Full|LocalScaffold) ;;
  *) fail "unknown mode: $MODE" ;;
esac
case "$CONFIGURATION" in
  Debug) project_references=true; nuget_dependencies=false ;;
  Release) project_references=false; nuget_dependencies=true ;;
  *) fail "unknown configuration: $CONFIGURATION" ;;
esac
case "$ARTIFACTS_DIR" in
  /*) ;;
  *) ARTIFACTS_DIR="$(pwd)/$ARTIFACTS_DIR" ;;
esac
if [[ -n "$PROFILE" ]]; then
  case "$PROFILE" in
    /*) ;;
    *) PROFILE="$(pwd)/$PROFILE" ;;
  esac
fi
if [[ -n "$BUILDS_ROOT" && "$BUILDS_ROOT" != /* ]]; then
  BUILDS_ROOT="$(pwd)/$BUILDS_ROOT"
fi
cd "$ROOT"
if [[ -z "$BUILDS_ROOT" ]]; then
  mkdir -p "$ARTIFACTS_DIR"
  bound_workspace="$(mktemp -d "$ARTIFACTS_DIR/bound-producer.XXXXXXXX")"
  trap 'rm -rf -- "$bound_workspace"' EXIT
  BUILDS_ROOT="$(python3 "$ROOT/eng/bound_builds.py" --output "$bound_workspace/checkout")"
else
  BUILDS_ROOT="$(python3 "$ROOT/eng/bound_builds.py" --verify-root "$BUILDS_ROOT")"
fi
export HexalithBuildsRoot="$BUILDS_ROOT"
catalog_sha256="$(python3 - "$ROOT/eng/builds-tuple.json" <<'PY'
from pathlib import Path
import sys
from eng.builds_tuple import load_binding
print(load_binding(Path(sys.argv[1]))["producer"]["catalog"]["sha256"])
PY
)"
python3 "$ROOT/eng/verify-platform-pins.py" --catalog "$BUILDS_ROOT/Props/Directory.Packages.props" \
  --catalog-sha256 "$catalog_sha256" --producer-binding "$ROOT/eng/builds-tuple.json"
(cd "$ROOT/eng" && python3 -m unittest test_verify_platform_pins test_local_dapr_security_cleanup) || fail "eng Python tests failed"
[[ -f "$ROOT/apphost.cs" ]] || fail "platform AppHost missing: apphost.cs"
[[ -f "$ROOT/aspire.config.json" ]] || fail "aspire.config.json missing"
[[ -f "$ROOT/README.md" ]] || fail "README.md missing"
[[ -f "$ROOT/docs/ext-host-1-agents-composition.md" ]] || fail "EXT-HOST-1 contract doc missing"
if [[ -d "$ROOT/src/Hexalith.Agents.AppHost" \
   || -d "$ROOT/src/Hexalith.Agents.Aspire" \
   || -d "$ROOT/src/Hexalith.Agents.ServiceDefaults" ]]; then
  fail "Agents module hosting projects must not live in Hexalith.Platform"
fi
if [[ "$MODE" == Full ]]; then
  full_args=(--artifacts-path "$ARTIFACTS_DIR" --configuration "$CONFIGURATION")
  if [[ -n "$PROFILE" ]]; then
    full_args+=(--profile "$PROFILE")
  fi
  python3 "$ROOT/eng/ext-host-1/verify_full.py" "${full_args[@]}"
  echo "OQ-5.4-1 joint owner qualification passed; dependency availability requires register acceptance."
else
  echo "Building local Platform scaffold ($CONFIGURATION)..."
  # The file-based AppHost requires full property syntax, rather than -warnaserror shorthand.
  dotnet build ./apphost.cs -c "$CONFIGURATION" --artifacts-path "$ARTIFACTS_DIR" \
    -p:TreatWarningsAsErrors=true -p:AspireUseCliBundle=true -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low -p:MinVerVersionOverride=1.0.0 \
    -p:UseHexalithProjectReferences="$project_references" -p:UseNuGetDeps="$nuget_dependencies" \
    -p:HexalithBuildsRoot="$BUILDS_ROOT"
  echo "LocalScaffold build succeeded. Full EXT-HOST-1 qualification remains unavailable."
fi
