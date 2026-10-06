[CmdletBinding()]
param(
    [ValidateSet('Local', 'Live')][string]$Mode = 'Live',
    [string]$ArtifactsDirectory = '/tmp/hexalith-agents54-custody-artifacts',
    [string]$EvidenceDirectory = '/tmp/hexalith-agents54-custody-evidence',
    [string]$EventStoreSourceRoot = '/home/administrator/projects/hexalith/eventstore'
)
$ErrorActionPreference = 'Stop'
# No manifest can unlock code, providers and full persisted-state qualification that do not exist.
if ($Mode -eq 'Live') {
    throw 'DependencyNotAvailable: EXT-SECRETS-1. Complete S1-S4/v23, production custody/profile, independent authority, accepted targets/commands and persisted qualification are unavailable.'
}
$root = Split-Path $PSScriptRoot -Parent
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
$assemblyPath = Join-Path $ArtifactsDirectory 'bin/Hexalith.Platform.Custody.Tests/debug/Hexalith.Platform.Custody.Tests.dll'
$testArgs = @($assemblyPath, '-class', '*CustodyPrerequisiteTests', '-result-xml', $xmlPath)
& dotnet @testArgs *> $testLog
if ($LASTEXITCODE -ne 0) { throw "Local custody tests failed; see $testLog" }
[xml]$results = Get-Content -Raw $xmlPath
$assembly = $results.assemblies.assembly
if ($null -eq $assembly -or [int]$assembly.total -le 0 -or [int]$assembly.passed -ne [int]$assembly.total -or [int]$assembly.failed -ne 0 -or [int]$assembly.errors -ne 0 -or [int]$assembly.skipped -ne 0 -or [int]$assembly.'not-run' -ne 0) {
    throw 'Local custody XML contains missing, failed, skipped or unrun tests.'
}
@{
    Mode = 'Local'; LiveReady = $false; DependencyAvailable = $false
    Scope = 'S1/S2 cryptographic fixtures only'; Total = [int]$assembly.total
    Passed = [int]$assembly.passed; BuildArguments = $buildArgs; TestArguments = $testArgs
    XmlSha256 = (Get-FileHash $xmlPath -Algorithm SHA256).Hash.ToLowerInvariant()
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceDirectory 'local-evidence.json')
Write-Output "Local custody fixture checks passed: $($assembly.passed). Full EXT-SECRETS-1 remains unavailable."
