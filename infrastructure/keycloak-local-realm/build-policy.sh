#!/usr/bin/env bash
set -euo pipefail

# Usage: build-policy.sh [requester-audience.jar] [workload-binding.jar]
# Without arguments, rewrites the committed JARs next to this script.
policy_root=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
work=$(mktemp -d)
trap 'rm -rf -- "$work"' EXIT
export TMPDIR="$work"

workload_provider="$work/keycloak-workload-binding"
mkdir "$workload_provider"
cp -r "$policy_root/../keycloak-workload-binding/pom.xml" "$policy_root/../keycloak-workload-binding/src" "$workload_provider/"
mvn -q -f "$policy_root/pom.xml" -Drealm.policy.buildDirectory="$work/keycloak-local-realm" compile
mvn -q -f "$workload_provider/pom.xml" test
python3 - "$work/keycloak-local-realm/classes" "${1:-$policy_root/platform-requester-audience.jar}" \
  "$workload_provider/target/classes" "${2:-$policy_root/platform-workload-binding.jar}" <<'PY'
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
