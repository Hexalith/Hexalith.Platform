#!/usr/bin/env bash
# Parent re-verification of story 2.12 review patches G31-G38 (2026-11-15).
set -u
P=/home/administrator/projects/hexalith/platform
F=$P/references/Hexalith.Folders
K=$P/infrastructure/keycloak-workload-binding
O=/tmp/claude-1000/-home-administrator-projects-hexalith-platform/259f8f1a-0d27-4625-9d54-f039a2fab18d/scratchpad/rp
export PATH="$HOME/.dotnet/tools:$PATH"
mkdir -p "$O/xml"
: > "$O/summary.txt"
SRC="-p:UseHexalithProjectReferences=true -m:1 -nodeReuse:false -v:q -p:HexalithPlatformRoot=$P -p:HexalithBuildsRoot=$P/references/Hexalith.Builds"

step() { # name cwd command...
  local name=$1 cwd=$2; shift 2
  local start=$(date -u +%FT%TZ)
  ( cd "$cwd" && "$@" ) > "$O/$name.log" 2>&1
  local rc=$?
  echo "$name exit=$rc start=$start end=$(date -u +%FT%TZ)" >> "$O/summary.txt"
  return $rc
}

step k-maven "$K" mvn -B -ntp package
step k-unittest "$K" python3 -B -m unittest test_run_owned
step k-live-run-owned "$K" python3 -B verify_component.py --run-owned

step t-package-build "$P" dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -p:UseHexalithProjectReferences=false -m:1 -nodeReuse:false -v:q \
  && step t-package-enrollment-default "$P" dotnet exec "$P/tests/Hexalith.Platform.Composition.Tests/bin/Debug/net11.0/Hexalith.Platform.Composition.Tests.dll" -noColor -class Hexalith.Platform.Composition.Tests.PlatformRunOwnedWorkloadEnrollmentTests -result-xml "$O/xml/t-package-enrollment-default.xml" \
  && step t-package-live-lanes "$K" env -u NO_COLOR TERM=xterm-256color python3 -B verify_component.py --run-owned --platform-assembly "$P/tests/Hexalith.Platform.Composition.Tests/bin/Debug/net11.0/Hexalith.Platform.Composition.Tests.dll"

step a-source-build "$F" dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj -c Debug $SRC \
  && step a-source-enrollment-default "$P" dotnet exec "$F/tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net11.0/Hexalith.Folders.AppHost.Tests.dll" -noColor -class Hexalith.Folders.AppHost.Tests.WorkloadEnrollmentComponentTests -class Hexalith.Folders.AppHost.Tests.WorkloadEnrollmentConfigurationTests -result-xml "$O/xml/a-source-enrollment-default.xml" \
  && step a-source-live-run-owned "$K" env -u NO_COLOR TERM=xterm-256color python3 -B verify_component.py --run-owned --apphost-assembly "$F/tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net11.0/Hexalith.Folders.AppHost.Tests.dll"

step testing-source-build "$F" dotnet build tests/Hexalith.Folders.Testing.Tests/Hexalith.Folders.Testing.Tests.csproj -c Debug $SRC \
  && step testing-scaffold "$F" dotnet exec "$F/tests/Hexalith.Folders.Testing.Tests/bin/Debug/net11.0/Hexalith.Folders.Testing.Tests.dll" -noColor -class Hexalith.Folders.Testing.Tests.ScaffoldContractTests -result-xml "$O/xml/testing-scaffold.xml"

step contracts-source-build "$F" dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj -c Debug $SRC \
  && step contracts-script-conformance "$F" dotnet exec "$F/tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net11.0/Hexalith.Folders.Contracts.Tests.dll" -noColor \
       -class Hexalith.Folders.Contracts.Tests.Deployment.ConsumerDocsConformanceTests \
       -class Hexalith.Folders.Contracts.Tests.Deployment.ScheduledDriftAndPolicyWorkflowConformanceTests \
       -class Hexalith.Folders.Contracts.Tests.Deployment.ProductionObservabilityConformanceTests \
       -class Hexalith.Folders.Contracts.Tests.Deployment.ProviderErrorDocsConformanceTests \
       -class Hexalith.Folders.Contracts.Tests.Deployment.OperationsAuditDocsConformanceTests \
       -class Hexalith.Folders.Contracts.Tests.Deployment.AdrRunbookDocsConformanceTests \
       -class Hexalith.Folders.Contracts.Tests.OpenApi.DaprPolicyConformance.DaprPolicyConformanceTests \
       -class Hexalith.Folders.Contracts.Tests.Deployment.ContractParityCiWorkflowConformanceTests \
       -class Hexalith.Folders.Contracts.Tests.Deployment.SecurityRedactionCiWorkflowConformanceTests \
       -result-xml "$O/xml/contracts-script-conformance.xml" \
  && step script-consumer-docs "$F" pwsh -NoLogo -NoProfile -File tests/tools/run-consumer-docs-gates.ps1 -SkipRestoreBuild -Configuration Debug \
  ; step script-scheduled-policy "$F" pwsh -NoLogo -NoProfile -File tests/tools/run-scheduled-policy-conformance-gates.ps1 -SkipRestoreBuild -Configuration Debug -PolicyMode static-only

step leftovers "$P" bash -c 'docker ps -a --filter label=com.hexalith.run-owned-workload --format "{{.ID}}" | wc -l; ls -d /tmp/hexalith-run-owned-workload-* 2>/dev/null | wc -l'
echo DONE >> "$O/summary.txt"
