[CmdletBinding()]
param(
    [ValidateSet('Local', 'Live')][string]$Mode = 'Live',
    [string]$ArtifactsDirectory = '/tmp/hexalith-agents54-custody-artifacts',
    [string]$EvidenceDirectory = '/tmp/hexalith-agents54-custody-evidence',
    [string]$EventStoreSourceRoot = '/home/administrator/projects/hexalith/eventstore'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$callerDirectory = (Get-Location).ProviderPath
$root = Split-Path $PSScriptRoot -Parent
$eventStoreArgument = $EventStoreSourceRoot
$artifactsArgument = $ArtifactsDirectory
$evidenceArgument = $EvidenceDirectory
if ($Mode -eq 'Live') {
    # Configuration alone never authorizes effects: the gate reads the authoritative accepted
    # target/command and independent runtime trust pins before calling an enrolled isolated adapter.
    & python3 (Join-Path $PSScriptRoot 'ext-secrets-1/qualify.py') --root $root --caller $callerDirectory --eventstore-argument $eventStoreArgument --artifacts-argument $artifactsArgument --evidence-argument $evidenceArgument
    if ($LASTEXITCODE -eq 2) { throw 'DependencyNotAvailable: EXT-SECRETS-1. Missing prerequisites; see sanitized attempt-*/live-evidence.json.' }
    if ($LASTEXITCODE -eq 4) { throw 'ReplacementAcceptancePacket: EXT-SECRETS-1. The replacement acceptance packet is not an object; see sanitized attempt-*/live-evidence.json.' }
    if ($LASTEXITCODE -ne 0) { throw 'QualificationFailed: EXT-SECRETS-1. See sanitized attempt-*/live-evidence.json; retain original recovery journal.' }
    return
}
$EventStoreSourceRoot = [IO.Path]::GetFullPath($EventStoreSourceRoot, $callerDirectory)
$ArtifactsDirectory = [IO.Path]::GetFullPath($ArtifactsDirectory)
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$commonsSourceRoot = Join-Path $root 'references/Hexalith.Commons'
# The native console is an executable source suite and is not discovered by solution test runners.
$nativeRun = [Guid]::NewGuid().ToString('N')
$nativeArtifacts = Join-Path $ArtifactsDirectory "native-$nativeRun"
$nativeEvidence = Join-Path $EvidenceDirectory "native-$nativeRun"
New-Item -ItemType Directory -Path $nativeEvidence | Out-Null
$nativeBuildLog = Join-Path $nativeEvidence 'build.log'
$nativeTestLog = Join-Path $nativeEvidence 'tests.log'
$nativeProject = Join-Path $root 'infrastructure/Hexalith.Platform.Custody.Dapr.Tests/Hexalith.Platform.Custody.Dapr.Tests.csproj'
$nativeBuildArgs = @('build', $nativeProject, '-c', 'Debug', '--artifacts-path', $nativeArtifacts, '-m:1', '-p:NuGetAudit=false', '-p:MinVerVersionOverride=1.0.0', '-p:UseHexalithProjectReferences=true', "-p:HexalithEventStoreRoot=$EventStoreSourceRoot", '-p:UseNuGetDeps=false', '-p:HexalithCommonsFromSource=true', "-p:HexalithCommonsRoot=$commonsSourceRoot", ('-p:NuGetLockFilePath=' + (Join-Path $nativeArtifacts 'locks/$(MSBuildProjectName).lock.json')))
& dotnet @nativeBuildArgs *> $nativeBuildLog
if ($LASTEXITCODE -ne 0) { throw "Local native console build failed; see $nativeBuildLog" }
$nativeBuildOutput = Get-Content -LiteralPath $nativeBuildLog -Raw
if ($nativeBuildOutput -notmatch '(?m)^\s*0 Warning\(s\)\s*$' -or $nativeBuildOutput -notmatch '(?m)^\s*0 Error\(s\)\s*$') { throw "Local native console build was not warning/error free; see $nativeBuildLog" }
$nativeAssembly = Join-Path $nativeArtifacts 'bin/Hexalith.Platform.Custody.Dapr.Tests/debug/Hexalith.Platform.Custody.Dapr.Tests.dll'
& dotnet $nativeAssembly *> $nativeTestLog
if ($LASTEXITCODE -ne 0) { throw "Local native console suite failed; see $nativeTestLog" }
$nativeResult = Get-Content -LiteralPath $nativeTestLog -Raw
if ($nativeResult -notmatch 'sourceComponentOnly=true physicalQualification=false passed=([1-9][0-9]*) failed=0') { throw "Local native console result is incomplete; see $nativeTestLog" }
$nativePassed = [int]$Matches[1]
@{ SourceComponentOnly = $true; InstalledQualification = $false; Passed = $nativePassed; BuildArguments = $nativeBuildArgs; LogSha256 = (Get-FileHash $nativeTestLog -Algorithm SHA256).Hash.ToLowerInvariant() } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $nativeEvidence 'source-evidence.json')
$project = Join-Path $root 'tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj'
$buildLog = Join-Path $EvidenceDirectory 'local-build.log'
$testLog = Join-Path $EvidenceDirectory 'local-tests.log'
$xmlPath = Join-Path $EvidenceDirectory 'local-tests.xml'
$modelReportPath = Join-Path $EvidenceDirectory 'custody-shared-cut-model.json'
. (Join-Path $PSScriptRoot 'ext-secrets-1/model_audit.ps1')
$buildArgs = @('build', $project, '-c', 'Debug', '--artifacts-path', $ArtifactsDirectory, '-m:1', '-p:NuGetAudit=false', '-p:MinVerVersionOverride=1.0.0', '-p:UseHexalithProjectReferences=true', "-p:HexalithEventStoreRoot=$EventStoreSourceRoot", '-p:UseNuGetDeps=false', '-p:HexalithCommonsFromSource=true', "-p:HexalithCommonsRoot=$commonsSourceRoot", ('-p:NuGetLockFilePath=' + (Join-Path $ArtifactsDirectory 'locks/$(MSBuildProjectName).lock.json')))
& dotnet @buildArgs *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "Local custody build failed; see $buildLog" }
$buildOutput = Get-Content -LiteralPath $buildLog -Raw
if ($buildOutput -notmatch '(?m)^\s*0 Warning\(s\)\s*$' -or $buildOutput -notmatch '(?m)^\s*0 Error\(s\)\s*$') {
    throw "Local custody build was not warning/error free; see $buildLog"
}
$assemblyPath = Join-Path $ArtifactsDirectory 'bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll'
$requiredClasses = @(
    'Hexalith.Platform.Custody.Tests.DaprPlatformHmacBindingTests',
    'Hexalith.Platform.Custody.Tests.CustodyKeyLifecycleTests',
    'Hexalith.Platform.Custody.Tests.CustodySharedCutModelTests',
    'Hexalith.Platform.Custody.Tests.CustodyPrerequisiteTests',
    'Hexalith.Platform.Custody.Tests.DeletionBatchCapabilityCodecTests',
    'Hexalith.Platform.Custody.Tests.DeletionBatchExecutionCoordinatorTests',
    'Hexalith.Platform.Custody.Tests.DeletionCapabilityRevocationSubscriberTests',
    'Hexalith.Platform.Custody.Tests.DeletionCapabilitySigningActorTests',
    'Hexalith.Platform.Custody.Tests.ExportCustodyCryptographyTests',
    'Hexalith.Platform.Custody.Tests.ExportKeyDeliveryActorTests',
    'Hexalith.Platform.Custody.Tests.Fr34ProtectionGateTests',
    'Hexalith.Platform.Custody.Tests.PrivateOwnerOperationDeadlineTests',
    'Hexalith.Platform.Custody.Tests.IdentityHistoryCleanupTests',
    'Hexalith.Platform.Custody.Tests.IdentityHistoryCustodyPolicyTests',
    'Hexalith.Platform.Custody.Tests.IndependentDecisionManifestVerifierTests',
    'Hexalith.Platform.Custody.Tests.PlatformAcceptedEnvelopeTimingTests',
    'Hexalith.Platform.Custody.Tests.PlatformKeyInventoryActorTests',
    'Hexalith.Platform.Custody.Tests.PlatformSecurityDenialRecorderTests',
    'Hexalith.Platform.Custody.Tests.PrivateOwnerOperationAuthenticatorTests',
    'Hexalith.Platform.Custody.Tests.ReplicatedSecurityObservationSpoolTests',
    'Hexalith.Platform.Custody.Tests.ReplicatedSecurityObservationSpoolWorkerTests',
    'Hexalith.Platform.Custody.Tests.TrustedEnvelopeReplayVerifierTests')
$testArgs = @($assemblyPath)
foreach ($class in $requiredClasses) { $testArgs += @('-class', $class) }
$testArgs += @('-result-xml', $xmlPath)
$modelReportSha256 = Invoke-CustodySharedCutModelAudit -ReportPath $modelReportPath -XmlPath $xmlPath -RunTests {
    & dotnet @testArgs *> $testLog
    if ($LASTEXITCODE -ne 0) { throw "Local custody tests failed; see $testLog" }
}
[xml]$results = Get-Content -Raw $xmlPath
$assembly = $results.assemblies.assembly
if ($null -eq $assembly -or [int]$assembly.total -le 0 -or [int]$assembly.passed -ne [int]$assembly.total -or [int]$assembly.failed -ne 0 -or [int]$assembly.errors -ne 0 -or [int]$assembly.skipped -ne 0 -or [int]$assembly.'not-run' -ne 0) {
    throw 'Local custody XML contains missing, failed, skipped or unrun tests.'
}
$executed = @($results.SelectNodes('//test'))
if ($executed.Count -ne [int]$assembly.total -or @($executed | Where-Object { $_.result -ne 'Pass' -or $_.type -notin $requiredClasses }).Count -ne 0) {
    throw 'Local custody XML contains unexpected, missing or nonpassing execution.'
}
$classEvidence = @()
foreach ($class in $requiredClasses) {
    $tests = @($executed | Where-Object { $_.type -eq $class })
    if ($tests.Count -eq 0) { throw "Required custody/cleanup class did not execute: $class" }
    $classEvidence += @{ Class = $class; Passed = $tests.Count }
}
$runtimeProject = Join-Path $root 'tests/Hexalith.Platform.Secrets.Runtime.Tests/Hexalith.Platform.Secrets.Runtime.Tests.csproj'
$runtimeBuildLog = Join-Path $EvidenceDirectory 'runtime-build.log'
$runtimeTestLog = Join-Path $EvidenceDirectory 'runtime-tests.log'
$runtimeXmlPath = Join-Path $EvidenceDirectory 'runtime-tests.xml'
$runtimeBuildArgs = @($buildArgs)
$runtimeBuildArgs[1] = $runtimeProject
& dotnet @runtimeBuildArgs *> $runtimeBuildLog
if ($LASTEXITCODE -ne 0) { throw "Local runtime/host build failed; see $runtimeBuildLog" }
$runtimeBuildOutput = Get-Content -LiteralPath $runtimeBuildLog -Raw
if ($runtimeBuildOutput -notmatch '(?m)^\s*0 Warning\(s\)\s*$' -or $runtimeBuildOutput -notmatch '(?m)^\s*0 Error\(s\)\s*$') {
    throw "Local runtime/host build was not warning/error free; see $runtimeBuildLog"
}
$runtimeAssemblyPath = Join-Path $ArtifactsDirectory 'bin/Hexalith.Platform.Secrets.Runtime.Tests/debug/Hexalith.Platform.Secrets.Runtime.Tests.dll'
& dotnet $runtimeAssemblyPath -result-xml $runtimeXmlPath *> $runtimeTestLog
if ($LASTEXITCODE -ne 0) { throw "Local runtime/host tests failed; see $runtimeTestLog" }
[xml]$runtimeResults = Get-Content -Raw $runtimeXmlPath
$runtimeAssembly = $runtimeResults.assemblies.assembly
$runtimeExecuted = @($runtimeResults.SelectNodes('//test'))
if ($null -eq $runtimeAssembly -or [int]$runtimeAssembly.total -le 0 -or [int]$runtimeAssembly.passed -ne [int]$runtimeAssembly.total -or [int]$runtimeAssembly.failed -ne 0 -or [int]$runtimeAssembly.errors -ne 0 -or [int]$runtimeAssembly.skipped -ne 0 -or [int]$runtimeAssembly.'not-run' -ne 0 -or $runtimeExecuted.Count -ne [int]$runtimeAssembly.total -or @($runtimeExecuted | Where-Object { $_.result -ne 'Pass' }).Count -ne 0) {
    throw 'Local runtime/host XML contains missing, failed, skipped or unrun tests.'
}
foreach ($class in @('Hexalith.Platform.Secrets.Runtime.Tests.SecretsRuntimeAdapterTests', 'Hexalith.Platform.Secrets.Runtime.Tests.SecretsAuthorityFailureTests', 'Hexalith.Platform.Secrets.Runtime.Tests.QualificationHostTests', 'Hexalith.Platform.Secrets.Runtime.Tests.ObserverJournalDurabilityTests')) {
    if (@($runtimeExecuted | Where-Object { $_.type -eq $class }).Count -eq 0) { throw "Required runtime/host class did not execute: $class" }
}
$pythonLog = Join-Path $EvidenceDirectory 'python-tests.log'
& python3 -m unittest discover -s (Join-Path $PSScriptRoot 'ext-secrets-1') -p 'test_*.py' *> $pythonLog
if ($LASTEXITCODE -ne 0) { throw "Local qualification/packet regressions failed; see $pythonLog" }
@{
    Mode = 'Local'; LiveReady = $false; DependencyAvailable = $false
    Scope = 'Configured S1/S2/S3/S4, lifecycle/inventory/delivery/signing/decision/private-credential/spool/FR34/replay/denial/compromise source simulations; no live qualification'; RequiredClasses = $classEvidence
    ModelReportSha256 = $modelReportSha256; ModelReportPath = $modelReportPath
    NativePassed = $nativePassed; NativeEvidence = $nativeEvidence
    RuntimePassed = [int]$runtimeAssembly.passed; RuntimeXmlSha256 = (Get-FileHash $runtimeXmlPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Total = [int]$assembly.total
    Passed = [int]$assembly.passed; BuildArguments = $buildArgs; TestArguments = $testArgs
    XmlSha256 = (Get-FileHash $xmlPath -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceDirectory 'local-evidence.json')
Write-Output "Local custody and host technical source checks passed: $($assembly.passed). Full EXT-SECRETS-1 remains unavailable."
