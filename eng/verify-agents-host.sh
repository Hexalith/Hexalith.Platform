#!/usr/bin/env bash
# EXT-HOST-1 compatibility verification for Hexalith Agents.
# Contract: clean-checkout composition through the platform host without
# module-owned Agents AppHost, Aspire, or ServiceDefaults projects.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

fail() {
  echo "ERROR: $*" >&2
  exit 1
}

[[ -f "$ROOT/apphost.cs" ]] || fail "platform AppHost missing: apphost.cs"
[[ -f "$ROOT/aspire.config.json" ]] || fail "aspire.config.json missing"
[[ -f "$ROOT/README.md" ]] || fail "README.md missing"
[[ -f "$ROOT/docs/ext-host-1-agents-composition.md" ]] || fail "EXT-HOST-1 contract doc missing"

# Fail closed if this repo accidentally reintroduces Agents-module hosting paths.
if [[ -d "$ROOT/src/Hexalith.Agents.AppHost" \
   || -d "$ROOT/src/Hexalith.Agents.Aspire" \
   || -d "$ROOT/src/Hexalith.Agents.ServiceDefaults" ]]; then
  fail "Agents module hosting projects must not live in Hexalith.Platform"
fi

echo "Building platform AppHost (Release)..."
# File-based AppHost: avoid -warnaserror shorthand (it breaks file-based app parsing).
dotnet build ./apphost.cs -c Release -p:TreatWarningsAsErrors=true
build_exit=$?
[[ $build_exit -eq 0 ]] || fail "platform AppHost Release build failed (exit $build_exit)"

echo "EXT-HOST-1 verify-agents-host: PASS"
echo "Note: live Agents DomainService/UI composition remains Story 5.6 work;"
echo "this command proves the platform host repository and AppHost build gate."
