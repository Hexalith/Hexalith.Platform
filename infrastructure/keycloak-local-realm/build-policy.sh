#!/usr/bin/env bash
set -euo pipefail

policy_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
run-with-temp bash -euo pipefail -c '
  workload_provider="$TMPDIR/keycloak-workload-binding"
  mkdir "$workload_provider"
  cp -r "$1/../keycloak-workload-binding/pom.xml" "$1/../keycloak-workload-binding/src" "$workload_provider/"
  mvn -q -f "$1/pom.xml" -Drealm.policy.buildDirectory="$TMPDIR/keycloak-local-realm" compile
  mvn -q -f "$workload_provider/pom.xml" test
  python3 - "$TMPDIR/keycloak-local-realm/classes" "${2:-$1/platform-requester-audience.jar}" \
    "$workload_provider/target/classes" "${3:-$1/platform-workload-binding.jar}" <<'"'"'PY'"'"'
from pathlib import Path
import sys
from zipfile import ZipFile, ZipInfo

for classes, output in zip(map(Path, sys.argv[1::2]), map(Path, sys.argv[2::2]), strict=True):
    with ZipFile(output, "w") as jar:
        for path in sorted(classes.rglob("*")):
            if path.is_file():
                member = ZipInfo(path.relative_to(classes).as_posix())
                jar.writestr(member, path.read_bytes())
PY
' bash "$policy_root" "${1:-}" "${2:-}"
