#!/usr/bin/env bash
# Local scaffold validation and closed full EXT-HOST-1 qualification.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE=Full
ARTIFACTS_DIR="${TMPDIR:-/tmp}/hexalith-agents54-host-artifacts"
fail() {
  echo "ERROR: $*" >&2
  exit 1
}
while (($#)); do
  case "$1" in
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
    *) fail "unknown argument: $1" ;;
  esac
done
case "$MODE" in
  Full|LocalScaffold) ;;
  *) fail "unknown mode: $MODE" ;;
esac
cd "$ROOT"
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
  fail "DependencyNotAvailable: EXT-HOST-1. Complete H1-H4 composition, production providers, accepted targets/commands and persisted qualification are unavailable."
fi
echo "Building local Platform scaffold (Debug)..."
# The file-based AppHost requires full property syntax, rather than -warnaserror shorthand.
dotnet build ./apphost.cs -c Debug --artifacts-path "$ARTIFACTS_DIR" \
  -p:TreatWarningsAsErrors=true -p:AspireUseCliBundle=true -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0
echo "LocalScaffold build succeeded. Full EXT-HOST-1 qualification remains unavailable."
