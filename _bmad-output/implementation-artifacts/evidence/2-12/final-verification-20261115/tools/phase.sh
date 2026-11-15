#!/usr/bin/env bash
# Usage: phase.sh source|package [step...]
# Runs the Debug build/test checks for one dependency mode. Continues after failures; check.py records each result.
set -u
MODE=$1; shift
case "$MODE" in
  source) REFS=true ;;
  package) REFS=false ;;
  *) echo "mode?"; exit 2 ;;
esac
FV=/tmp/claude-1000/-home-administrator-projects-hexalith-platform/259f8f1a-0d27-4625-9d54-f039a2fab18d/scratchpad/fv
P=/home/administrator/projects/hexalith/platform
F=$P/references/Hexalith.Folders
K=$P/infrastructure/keycloak-workload-binding
CHECK="python3 -B $FV/tools/check.py"
CDLL=$P/src/Hexalith.Platform.Composition/bin/Debug/net11.0/Hexalith.Platform.Composition.dll
TDLL=$P/tests/Hexalith.Platform.Composition.Tests/bin/Debug/net11.0/Hexalith.Platform.Composition.Tests.dll
TCDLL=$P/tests/Hexalith.Platform.Composition.Tests/bin/Debug/net11.0/Hexalith.Platform.Composition.dll
ADLL=$F/tests/Hexalith.Folders.AppHost.Tests/bin/Debug/net11.0/Hexalith.Folders.AppHost.Tests.dll
UDLL=$F/tests/Hexalith.Folders.UI.Tests/bin/Debug/net11.0/Hexalith.Folders.UI.Tests.dll
UIDLL=$F/tests/Hexalith.Folders.UI.Tests/bin/Debug/net11.0/Hexalith.Folders.UI.dll
CTDLL=$F/tests/Hexalith.Folders.Contracts.Tests/bin/Debug/net11.0/Hexalith.Folders.Contracts.Tests.dll
BUILD=(-c Debug -p:UseHexalithProjectReferences=$REFS -m:1 -nodeReuse:false -v:q)
FROOTS=(-p:HexalithPlatformRoot=$P -p:HexalithBuildsRoot=$P/references/Hexalith.Builds)
STEPS=${*:-c t t-default t-live a a-default a-ordinary a-run-owned ui ui-tests contracts contracts-tests}
for step in $STEPS; do
  case $step in
    c) $CHECK c-$MODE-build $P --hash $CDLL --timeout 1500 -- dotnet build src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj "${BUILD[@]}" ;;
    t) $CHECK t-$MODE-build $P --hash $TDLL --hash $TCDLL --timeout 1500 -- dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj "${BUILD[@]}" ;;
    t-default) $CHECK t-$MODE-enrollment-default $P --hash $TDLL --hash $TCDLL --xml $FV/xml/t-$MODE-enrollment-default.xml --timeout 900 -- \
        dotnet exec $TDLL -noColor -class Hexalith.Platform.Composition.Tests.PlatformRunOwnedWorkloadEnrollmentTests -result-xml $FV/xml/t-$MODE-enrollment-default.xml ;;
    t-live) $CHECK t-$MODE-live-consumer-and-cancellation $K --unset NO_COLOR --env TERM=xterm-256color --hash $TDLL --hash $TCDLL --timeout 900 -- \
        python3 -B verify_component.py --run-owned --platform-assembly $TDLL ;;
    a) (cd $F && $CHECK a-$MODE-build $F --hash $ADLL --timeout 1500 -- dotnet build tests/Hexalith.Folders.AppHost.Tests/Hexalith.Folders.AppHost.Tests.csproj "${BUILD[@]}" "${FROOTS[@]}") ;;
    a-default) $CHECK a-$MODE-enrollment-default $P --hash $ADLL --xml $FV/xml/a-$MODE-enrollment-default.xml --timeout 900 -- \
        dotnet exec $ADLL -noColor -class Hexalith.Folders.AppHost.Tests.WorkloadEnrollmentComponentTests -class Hexalith.Folders.AppHost.Tests.WorkloadEnrollmentConfigurationTests -result-xml $FV/xml/a-$MODE-enrollment-default.xml ;;
    a-ordinary) $CHECK a-$MODE-live-ordinary${ATTEMPT:-} $K --env NO_COLOR=1 --env TERM=dumb --hash $ADLL --timeout 900 -- \
        python3 -B verify_component.py --apphost-assembly $ADLL ;;
    a-run-owned) $CHECK a-$MODE-live-run-owned $K --unset NO_COLOR --env TERM=xterm-256color --hash $ADLL --timeout 900 -- \
        python3 -B verify_component.py --run-owned --apphost-assembly $ADLL ;;
    ui) $CHECK ui-$MODE-build $F --hash $UDLL --hash $UIDLL --timeout 1500 -- dotnet build tests/Hexalith.Folders.UI.Tests/Hexalith.Folders.UI.Tests.csproj "${BUILD[@]}" "${FROOTS[@]}" ;;
    ui-tests) $CHECK ui-$MODE-regressions $P --hash $UDLL --hash $UIDLL --xml $FV/xml/ui-$MODE-regressions.xml --timeout 900 -- \
        dotnet exec $UDLL -noColor -class Hexalith.Folders.UI.Tests.SafeCopyIdTests -class Hexalith.Folders.UI.Tests.CorrelationCopyButtonTests -class Hexalith.Folders.UI.Tests.UserContextAccessorRegistrationTests -result-xml $FV/xml/ui-$MODE-regressions.xml ;;
    contracts) $CHECK contracts-$MODE-build $F --hash $CTDLL --timeout 1500 -- dotnet build tests/Hexalith.Folders.Contracts.Tests/Hexalith.Folders.Contracts.Tests.csproj "${BUILD[@]}" "${FROOTS[@]}" ;;
    contracts-tests) $CHECK contracts-$MODE-conformance $F --hash $CTDLL --xml $FV/xml/contracts-$MODE-conformance.xml --timeout 1500 -- \
        dotnet exec $CTDLL -noColor \
          -class Hexalith.Folders.Contracts.Tests.Deployment.AccessibilityCiWorkflowConformanceTests \
          -class Hexalith.Folders.Contracts.Tests.Deployment.ContractParityCiWorkflowConformanceTests \
          -class Hexalith.Folders.Contracts.Tests.Deployment.E2eCiWorkflowConformanceTests \
          -class Hexalith.Folders.Contracts.Tests.Deployment.NfrTraceabilityConformanceTests \
          -class Hexalith.Folders.Contracts.Tests.Deployment.SecurityRedactionCiWorkflowConformanceTests \
          -class Hexalith.Folders.Contracts.Tests.OpenApi.ContractSpineCiGateTests \
          -class Hexalith.Folders.Contracts.Tests.OpenApi.GovernanceCompletenessGateTests \
          -class Hexalith.Folders.Contracts.Tests.OpenApi.SafetyInvariantGateTests \
          -class Hexalith.Folders.Contracts.Tests.McpCli.PackedContractsClosureTests \
          -result-xml $FV/xml/contracts-$MODE-conformance.xml ;;
    *) echo "unknown step $step" ;;
  esac
done
