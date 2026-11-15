#!/usr/bin/env bash
# Parent re-verification of the story 2.12 delta-review patches (2026-11-15).
set -u
P=/home/administrator/projects/hexalith/platform
F=$P/references/Hexalith.Folders
W=/tmp/claude-1000/-home-administrator-projects-hexalith-platform/453dcd32-4ce2-4c99-9857-a753ff8ccb96/scratchpad/folders-2-12-delta
K=$P/infrastructure/keycloak-workload-binding
O=/tmp/claude-1000/-home-administrator-projects-hexalith-platform/453dcd32-4ce2-4c99-9857-a753ff8ccb96/scratchpad/delta-verify
export PATH="$HOME/.dotnet/tools:$PATH"
mkdir -p "$O/xml" "$O/logs"
: > "$O/summary.txt"
SRC="-p:UseHexalithProjectReferences=true -m:1 -nodeReuse:false -v:q -p:HexalithPlatformRoot=$P -p:HexalithBuildsRoot=$P/references/Hexalith.Builds"
T_DLL=$P/tests/Hexalith.Platform.Composition.Tests/bin/Debug/net11.0/Hexalith.Platform.Composition.Tests.dll
A_DLL=$F/tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net11.0/Hexalith.Folders.AppHost.Tests.dll

step() { # name cwd command...
  local name=$1 cwd=$2; shift 2
  local start=$(date -u +%FT%TZ)
  ( cd "$cwd" && "$@" ) > "$O/logs/$name.log" 2>&1
  local rc=$?
  echo "$name exit=$rc start=$start end=$(date -u +%FT%TZ)" >> "$O/summary.txt"
  return $rc
}

step k-maven "$K" mvn -B -ntp package
step k-unittest "$K" python3 -B -m unittest test_run_owned
step k-live-run-owned "$K" python3 -B verify_component.py --run-owned

step t-package-build "$P" dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -p:UseHexalithProjectReferences=false -m:1 -nodeReuse:false -v:q \
  && step t-package-enrollment-default "$P" dotnet exec "$T_DLL" -noColor -class Hexalith.Platform.Composition.Tests.PlatformRunOwnedWorkloadEnrollmentTests -result-xml "$O/xml/t-package-enrollment-default.xml" \
  && step t-package-live-lanes "$K" env -u NO_COLOR TERM=xterm-256color python3 -B verify_component.py --run-owned --platform-assembly "$T_DLL"

step a-source-build "$F" dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Debug $SRC \
  && step a-source-enrollment-default "$P" dotnet exec "$A_DLL" -noColor -class Hexalith.Folders.AppHost.Tests.WorkloadEnrollmentComponentTests -class Hexalith.Folders.AppHost.Tests.WorkloadEnrollmentConfigurationTests -result-xml "$O/xml/a-source-enrollment-default.xml" \
  && step a-source-live-run-owned "$K" env -u NO_COLOR TERM=xterm-256color python3 -B verify_component.py --run-owned --apphost-assembly "$A_DLL"

# Folders CI step "Run CI/CD tooling unit tests", on the patched worktree (Builds props copied from the pinned 7cd0c17a).
step folders-tooling-unittest "$W" python3 -m unittest discover -s scripts/tests -p 'test_*.py'
step folders-consumer-generator-compile "$W" python3 -B -m py_compile scripts/validate-consumer-package-references.py

step leftovers "$P" bash -c 'docker ps -a --filter label=com.hexalith.run-owned-workload --format "{{.ID}}" | wc -l; ls -d /tmp/hexalith-run-owned-workload-* 2>/dev/null | wc -l'

( cd "$P" && sha256sum docs/security/workload-binding-enrollment.md \
    tests/Hexalith.Platform.Composition.Tests/PlatformRunOwnedWorkloadEnrollmentTests.cs \
    src/Hexalith.Platform.Composition/PlatformWorkloadEnrollment.cs \
    src/Hexalith.Platform.Composition/PlatformWorkloadEnrollmentInventory.cs \
    src/Hexalith.Platform.Composition/PlatformRunOwnedWorkloadEnrollment.cs \
    infrastructure/keycloak-workload-binding/run_owned.py infrastructure/keycloak-workload-binding/run_owned_component.py \
    infrastructure/keycloak-workload-binding/test_run_owned.py infrastructure/keycloak-workload-binding/verify_component.py ) > "$O/source-sha256.txt"
( cd "$F" && sha256sum tests/Hexalith.Folders.AppHost.Tests/WorkloadEnrollmentComponentTests.cs tests/Hexalith.Folders.AppHost.Tests/WorkloadEnrollmentConfigurationTests.cs \
    tests/Hexalith.Folders.AppHost.Tests/OperationalProviderFixture.cs tests/Hexalith.Folders.AppHost.Tests/FolderDurabilityAcceptanceFixture.cs ) >> "$O/source-sha256.txt"
( cd "$W" && sha256sum scripts/tests/test_ci_cd_tooling.py scripts/validate-consumer-package-references.py ) | sed 's#  #  worktree:#' >> "$O/source-sha256.txt"
sha256sum "$T_DLL" "$A_DLL" > "$O/assembly-sha256.txt" 2>&1
( cd "$W" && git diff ) > "$O/folders-net11-scripts.patch"
echo DONE >> "$O/summary.txt"
