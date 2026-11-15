#!/usr/bin/env bash
# Supplementary T lanes: HEAD's .config/dotnet-tools.json (466c6dd1, Builds CLI 4.44.3) selects net10.0 in
# Directory.Build.props:7, so the default package build fails NU1201/NU1202; override only the manifest text that selects it (Directory.Build.props:5,7).
set -u
P=/home/administrator/projects/hexalith/platform
K=$P/infrastructure/keycloak-workload-binding
O=/tmp/claude-1000/-home-administrator-projects-hexalith-platform/453dcd32-4ce2-4c99-9857-a753ff8ccb96/scratchpad/delta-verify
export PATH="$HOME/.dotnet/tools:$PATH"
T_DLL=$P/tests/Hexalith.Platform.Composition.Tests/bin/Debug/net11.0/Hexalith.Platform.Composition.Tests.dll
step() { local name=$1 cwd=$2; shift 2; local start=$(date -u +%FT%TZ); ( cd "$cwd" && "$@" ) > "$O/logs/$name.log" 2>&1; local rc=$?
  echo "$name exit=$rc start=$start end=$(date -u +%FT%TZ)" >> "$O/summary.txt"; return $rc; }
step supplementary-t-package-net11-build "$P" dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -p:UseHexalithProjectReferences=false -p:_PlatformRuntimeToolManifest=manifest-net11-override -m:1 -nodeReuse:false -v:q \
  && step supplementary-t-package-net11-enrollment-default "$P" dotnet exec "$T_DLL" -noColor -class Hexalith.Platform.Composition.Tests.PlatformRunOwnedWorkloadEnrollmentTests -result-xml "$O/xml/supplementary-t-package-net11-enrollment-default.xml" \
  && step supplementary-t-package-net11-live-lanes "$K" env -u NO_COLOR TERM=xterm-256color python3 -B verify_component.py --run-owned --platform-assembly "$T_DLL"
step leftovers-after-t "$P" bash -c 'docker ps -a --filter label=com.hexalith.run-owned-workload --format "{{.ID}}" | wc -l; ls -d /tmp/hexalith-run-owned-workload-* 2>/dev/null | wc -l'
sha256sum "$T_DLL" >> "$O/assembly-sha256.txt"
echo DONE-T >> "$O/summary.txt"
