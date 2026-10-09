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
# No manifest can unlock code, providers and full persisted-state qualification that do not exist.
if ($Mode -eq 'Live') {
    throw 'DependencyNotAvailable: EXT-SECRETS-1. Complete live S1-S4/v23 qualification requires production custody/profile, independent authority, accepted targets/commands and qualified persisted installation; implemented source protocols do not establish those inputs.'
}
$callerDirectory = (Get-Location).ProviderPath
$root = Split-Path $PSScriptRoot -Parent
$EventStoreSourceRoot = [IO.Path]::GetFullPath($EventStoreSourceRoot, $callerDirectory)
$ArtifactsDirectory = [IO.Path]::GetFullPath($ArtifactsDirectory)
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory)
New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
$project = Join-Path $root 'tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj'
$buildLog = Join-Path $EvidenceDirectory 'local-build.log'
$testLog = Join-Path $EvidenceDirectory 'local-tests.log'
$xmlPath = Join-Path $EvidenceDirectory 'local-tests.xml'
$buildArgs = @('build', $project, '-c', 'Debug', '--artifacts-path', $ArtifactsDirectory, '-m:1', '-p:NuGetAudit=false', '-p:MinVerVersionOverride=1.0.0', '-p:UseHexalithProjectReferences=true', "-p:HexalithEventStoreRoot=$EventStoreSourceRoot")
& dotnet @buildArgs *> $buildLog
if ($LASTEXITCODE -ne 0) { throw "Local custody build failed; see $buildLog" }
$buildOutput = Get-Content -LiteralPath $buildLog -Raw
if ($buildOutput -notmatch '(?m)^\s*0 Warning\(s\)\s*$' -or $buildOutput -notmatch '(?m)^\s*0 Error\(s\)\s*$') {
    throw "Local custody build was not warning/error free; see $buildLog"
}
$assemblyPath = Join-Path $ArtifactsDirectory 'bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll'
$requiredClasses = @(
    'Hexalith.Platform.Custody.Tests.CustodyKeyLifecycleTests',
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
& dotnet @testArgs *> $testLog
if ($LASTEXITCODE -ne 0) { throw "Local custody tests failed; see $testLog" }
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
@{
    Mode = 'Local'; LiveReady = $false; DependencyAvailable = $false
    Scope = 'Configured S1/S2/S3/S4, lifecycle/inventory/delivery/signing/decision/private-credential/spool/FR34/replay/denial/compromise source simulations; no live qualification'; RequiredClasses = $classEvidence
    Total = [int]$assembly.total
    Passed = [int]$assembly.passed; BuildArguments = $buildArgs; TestArguments = $testArgs
    XmlSha256 = (Get-FileHash $xmlPath -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceDirectory 'local-evidence.json')
Write-Output "Local custody and host technical source checks passed: $($assembly.passed). Full EXT-SECRETS-1 remains unavailable."
