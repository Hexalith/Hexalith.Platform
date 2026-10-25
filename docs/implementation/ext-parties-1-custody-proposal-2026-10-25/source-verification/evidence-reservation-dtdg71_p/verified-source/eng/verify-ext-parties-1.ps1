[CmdletBinding()]
param(
    [ValidateSet('Local', 'LiveReadiness', 'Live')][string]$Mode = 'Local',
    [string]$EventStoreRoot = (Join-Path $PSScriptRoot '../../eventstore'),
    [string]$PlatformRoot = (Join-Path $PSScriptRoot '../../platform'),
    [string]$EvidenceDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) ('ext-parties-1-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss'))),
    [string]$ArtifactsDirectory,
    [string]$MemoriesRoot,
    [string]$DependencyRegisterPath = (Join-Path $PSScriptRoot '../../agents/_bmad-output/planning-artifacts/external-dependency-register.md')
)
$ErrorActionPreference = 'Stop'
$callerDirectory = (Get-Location).ProviderPath
$PartiesRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$EventStoreRoot = [System.IO.Path]::GetFullPath($EventStoreRoot, $callerDirectory)
$PlatformRoot = [System.IO.Path]::GetFullPath($PlatformRoot, $callerDirectory)
$EvidenceDirectory = [System.IO.Path]::GetFullPath($EvidenceDirectory, $callerDirectory)
$DependencyRegisterPath = [System.IO.Path]::GetFullPath($DependencyRegisterPath, $callerDirectory)
if (-not [string]::IsNullOrWhiteSpace($ArtifactsDirectory)) { $ArtifactsDirectory = [System.IO.Path]::GetFullPath($ArtifactsDirectory, $callerDirectory) }
if (-not [string]::IsNullOrWhiteSpace($MemoriesRoot)) { $MemoriesRoot = [System.IO.Path]::GetFullPath($MemoriesRoot, $callerDirectory) }
if ($Mode -eq 'LiveReadiness') {
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'verify-ext-parties-history-readiness.ps1') -EvidenceDirectory $EvidenceDirectory -DependencyRegisterPath $DependencyRegisterPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    exit 0
}

# An attempt owns a fresh directory and a permanent, atomically created marker.
# Directory creation alone does not exclude a synchronized competing attempt.
if (Test-Path -LiteralPath $EvidenceDirectory) { throw "Evidence directory already exists: $EvidenceDirectory" }
New-Item -ItemType Directory -Path $EvidenceDirectory -ErrorAction Stop | Out-Null
$attempt = [IO.File]::Open((Join-Path $EvidenceDirectory '.ext-parties-1-attempt'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$attempt.Dispose()

if ($Mode -eq 'Live') {
    $required = @('EXT_PARTIES_GATEWAY_URL', 'EXT_PARTIES_TENANT_A', 'EXT_PARTIES_TENANT_B',
        'EXT_PARTIES_PROVISIONER_CREDENTIAL', 'EXT_PARTIES_IDENTITY_WRITER_CREDENTIAL', 'EXT_PARTIES_READER_CREDENTIAL',
        'EXT_PARTIES_POLICY_ID', 'EXT_PARTIES_RETENTION', 'EXT_PARTIES_EXPIRY_TRIGGER', 'EXT_PARTIES_CUSTODY_TARGET',
        'EXT_PARTIES_RESTORE_TARGET', 'EXT_PARTIES_FAILURE_INJECTION_TARGET')
    $missing = @($required | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
    $gate = if ($missing.Count) { 'Missing live inputs: ' + ($missing -join ', ') } else {
        'Live qualification remains blocked: production custody/policy and complete authenticated P-01-P-10 persisted-state/restart/restore/failure-injection lanes are not installed. The purpose-scoped history contract and LiveReadiness read probes do not establish complete qualification.'
    }
    Set-Content -Path (Join-Path $EvidenceDirectory 'live-gate.txt') -Value $gate
    Write-Error $gate -ErrorAction Continue
    exit 1
}

if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) { $ArtifactsDirectory = Join-Path $EvidenceDirectory 'artifacts' }
if ([string]::IsNullOrWhiteSpace($MemoriesRoot)) { $MemoriesRoot = Join-Path $EvidenceDirectory 'optional-memories-package-mode' }
$ArtifactsDirectory = [System.IO.Path]::GetFullPath($ArtifactsDirectory, $callerDirectory)
$flags = @('-c', 'Debug', '--artifacts-path', $ArtifactsDirectory, '-p:UseHexalithProjectReferences=true', '-p:UseNuGetDeps=false',
    "-p:HexalithEventStoreRoot=$EventStoreRoot", "-p:HexalithCommonsRoot=$PartiesRoot/references/Hexalith.Commons",
    "-p:HexalithMemoriesRoot=$MemoriesRoot", '-p:NuGetAudit=false', '-m:1')
# Resolve one source version for the entire Local graph. Tenants forwards its EventStore
# package version on project references; differing root pins otherwise overwrite a shared
# output assembly with a version that does not satisfy another just-built consumer.
$eventStoreProject = Join-Path $EventStoreRoot 'src/Hexalith.EventStore.Contracts/Hexalith.EventStore.Contracts.csproj'
$versionArguments = @('msbuild', $eventStoreProject, '-nologo', '-getProperty:HexalithEventStoreVersion',
    '-p:Configuration=Debug', '-p:UseHexalithProjectReferences=true', '-p:NuGetAudit=false')
$eventStoreVersion = (& dotnet @versionArguments 2> (Join-Path $EvidenceDirectory 'eventstore-version.stderr.log') | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $eventStoreVersion -notmatch '\A\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z') {
    throw 'Cannot resolve the selected EventStore source version for a consistent Local build graph.'
}
$eventStoreVersion | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'eventstore-version.log')
$flags += "-p:HexalithEventStoreVersion=$eventStoreVersion"
$lanes = @(
    @{ Root=$EventStoreRoot; Project='Hexalith.EventStore.Contracts.Tests'; Classes=@('*StreamReadPageValidatorTests', '*RetainedIdentityHistoryValidatorTests', '*RecoverableAnchoredStateTests'); Matrix='Shared source contract validation' },
    @{ Root=$EventStoreRoot; Project='Hexalith.EventStore.Client.Tests'; Classes=@('*AuthoritativeEventStreamReaderTests', '*EventStoreGatewayClientTests', '*EventStoreGatewayClientStreamTests', '*RetainedIdentityHistoryReaderTests', '*SourceNamespaceSnapshotReaderTests', '*SourcePublicationNamespaceTests', '*SourcePublicationFeedTests', '*SourcePublicationDispatcherTests', '*DaprSourcePublicationIndexStoreTests', '*DirectoryAtomicAppendTests'); Matrix='Shared complete source/isolation/discovery' },
    @{ Root=$EventStoreRoot; Project='Hexalith.EventStore.Server.Tests'; Classes=@('*IdentityAdmissionTests', '*ActorRegistryTests', '*IdentityGatewayDenialTests', '*CommandsControllerTrustedExtensionTests', '*QueriesControllerTests', '*StreamsControllerTests', '*RetainedIdentityHistorySourceReaderTests', '*ExpiredIdentityHistorySourceTests', '*DirectoryMigrationBoundaryTests', '*InteractionOccurrenceRegistryActorTests', '*DeletionConsumptionActorTests', '*GuardedStateTransactionTests', '*GovernanceScopeGuardTests', '*AggregateActorPublicationRegistrationTests'); Matrix='Isolation/actor/history/technical owner authority' },
    @{ Root=$EventStoreRoot; Project='Hexalith.EventStore.PayloadProtection.Tests'; Classes=@('*InteractionOccurrenceKeyDerivationTests', '*InteractionOccurrenceProtectorTests'); Matrix='Exact occurrence derivation/protection and post-decrypt withdrawal' },
    @{ Root=$PlatformRoot; Project='Hexalith.Platform.Identity.Tests'; Classes=@('*PlatformIdentityAdmissionTests'); Matrix='Global registry/bootstrap' },
    @{ Root=$PlatformRoot; Project='Hexalith.Platform.Custody.Tests'; Classes=@('*CustodyPrerequisiteTests', '*IdentityHistoryCleanupTests', '*IdentityHistoryCustodyPolicyTests'); Matrix='Custody prerequisites/policy/lifecycle/expiry/denial' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Contracts.Tests'; Classes=@('*PartyStateTests', '*PartyIdentityContractTests', '*ContractsPublicApiSnapshotTests'); Matrix='History/compatibility' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Server.Tests'; Classes=@('*AgentPartyProvisioningTests', '*HumanActorBindingTests', '*PartyAggregateCreateTests', '*PartyAggregateCompositeTests'); Matrix='Provision/history' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Tests'; Classes=@('*PartyIdentityAdmissionTests', '*PartyIdentityQueryHandlerTests', '*PartyIdentityRetentionConfigurationTests', '*PartyDomainProcessorValidationTests', '*PartySdkQueryHandlerTests', '*PartiesProcessEndpointTests', '*PartiesDomainServiceSecurityTests'); Matrix='Current identity/history/isolation' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Security.Tests'; Classes=@('*IdentityHistoryProtectionTests', '*PartyPayloadProtectionServiceTests'); Matrix='Custody separation/denial' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.Client.Tests'; Classes=@('*HttpPartiesIdentityClientTests', '*HttpPartiesCommandClientTests', '*HttpPartiesQueryClientTests', '*DependencyInjectionTests'); Matrix='Isolation/compatibility' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.UI.Tests'; Classes=@('*IdentityBindingBoundaryTests', '*IdentityBindingProvisioningServiceTests', '*PartyIdClaimResolverTests', '*SelfScopedPartiesClientTests'); Matrix='Consumer compatibility' },
    @{ Root=$PartiesRoot; Project='Hexalith.Parties.IntegrationTests'; Classes=@('*PartyIdentityPersistedVerifierTests'); Matrix='Authenticated persisted P-01-P-10 owner lane' }
)
$manifest = @()
foreach ($lane in $lanes) {
    Push-Location $lane.Root
    try {
        $project = Join-Path $lane.Root "tests/$($lane.Project)/$($lane.Project).csproj"
        if (-not (Test-Path $project)) { throw "Missing required owner project: $project" }
        $buildLog = Join-Path $EvidenceDirectory "$($lane.Project)-build.log"
        Write-Host "Building $($lane.Project)"
        & dotnet build $project @flags *> $buildLog
        if ($LASTEXITCODE -ne 0) { Get-Content $buildLog -Tail 25; throw "Build failed: $($lane.Project)" }
        $assembly = Join-Path $ArtifactsDirectory "bin/$($lane.Project)/debug/$($lane.Project).dll"
        if (-not (Test-Path $assembly)) { throw "Successful owner build did not produce the expected test assembly: $($lane.Project)" }
        $filter = @()
        foreach ($class in $lane.Classes) { $filter += @('-class', $class) }
        $testLog = Join-Path $EvidenceDirectory "$($lane.Project)-tests.log"
        $xmlLog = Join-Path $EvidenceDirectory "$($lane.Project)-tests.xml"
        $previousSourceRoot = [Environment]::GetEnvironmentVariable('HEXALITH_PARTIES_SOURCE_ROOT')
        try {
            [Environment]::SetEnvironmentVariable('HEXALITH_PARTIES_SOURCE_ROOT', $PartiesRoot)
            & dotnet $assembly @filter -result-xml $xmlLog *> $testLog
            $testExitCode = $LASTEXITCODE
        }
        finally { [Environment]::SetEnvironmentVariable('HEXALITH_PARTIES_SOURCE_ROOT', $previousSourceRoot) }
        if ($testExitCode -ne 0) { Get-Content $testLog; throw "Tests failed: $($lane.Project)" }
        $summary = (Get-Content $testLog | Where-Object { $_ -match 'Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+), Not Run: (\d+)' } | Select-Object -Last 1)
        if (-not $summary -or $summary -notmatch 'Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+), Not Run: (\d+)') { throw "No execution evidence: $($lane.Project)" }
        if ([int]$Matches[1] -le 0 -or [int]$Matches[2] -ne 0 -or [int]$Matches[3] -ne 0 -or [int]$Matches[4] -ne 0 -or [int]$Matches[5] -ne 0) { throw "Required lane incomplete: $summary" }
        [xml]$xmlEvidence = Get-Content -Raw $xmlLog
        $executed = @($xmlEvidence.SelectNodes('//test'))
        if ($executed.Count -eq 0 -or @($executed | Where-Object { $_.result -ne 'Pass' }).Count -ne 0) { throw "Incomplete XML execution evidence: $($lane.Project)" }
        foreach ($pattern in $lane.Classes) {
            $matched = @($executed | Where-Object { $_.type -like $pattern -and $_.result -eq 'Pass' })
            if ($matched.Count -eq 0) { throw "Required class had no passing executed test: $pattern in $($lane.Project)" }
        }
        $manifest += @{ Project=$lane.Project; Matrix=$lane.Matrix; Classes=$lane.Classes; BuildLog=$buildLog; TestLog=$testLog; XmlLog=$xmlLog; Summary=$summary }
        $manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $EvidenceDirectory 'local-evidence.json')
        Write-Host $summary
    }
    finally { Pop-Location }

}
Write-Host "Local verification passed. Evidence: $EvidenceDirectory"
Write-Host 'Live qualification is not established; LiveReadiness performs only owner-configured read probes and Live retains the complete qualification gate.'
