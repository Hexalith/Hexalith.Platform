[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$DependencyRegisterPath
)
$ErrorActionPreference = 'Stop'
$callerDirectory = (Get-Location).ProviderPath
$EvidenceDirectory = [IO.Path]::GetFullPath($EvidenceDirectory, $callerDirectory)
$DependencyRegisterPath = [IO.Path]::GetFullPath($DependencyRegisterPath, $callerDirectory)
$required = @('EXT_PARTIES_GATEWAY_URL', 'EXT_PARTIES_READER_HEADERS_JSON', 'EXT_PARTIES_TARGET_SHA', 'EXT_PARTIES_COMPATIBILITY_RECEIPT',
    'EXT_PARTIES_TENANT_A', 'EXT_PARTIES_HISTORY_PARTY_ID', 'EXT_PARTIES_HISTORY_ACTOR_ID',
    'EXT_PARTIES_HISTORY_ACTION_AT', 'EXT_PARTIES_HISTORY_BINDING_VERSION', 'EXT_PARTIES_POLICY_ID', 'EXT_PARTIES_RETENTION', 'EXT_PARTIES_EXPIRY_TRIGGER')
$missing = @($required | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
if ($missing.Count) {
    throw ('DependencyNotAvailable: EXT-PARTIES-1. Missing read-only live readiness inputs: ' + ($missing -join ', '))
}
if ($env:EXT_PARTIES_TARGET_SHA -cnotmatch '^[0-9a-f]{40}$') { throw 'An exact requested source target SHA is required.' }
# Read probes execute the dependency seam. A local fixture exemption cannot bypass
# the authoritative consumer execution gate or establish owner acceptance.
if (-not (Test-Path -LiteralPath $DependencyRegisterPath -PathType Leaf)) { throw 'DependencyNotAvailable: EXT-PARTIES-1 authoritative register is missing.' }
$register = Get-Content -LiteralPath $DependencyRegisterPath -Raw
$section = [regex]::Match($register, '(?ms)^### EXT-PARTIES-1\b.*?(?=^### |\z)').Value
function Get-Commitment([string]$Field) {
    $pattern = '(?m)^\|\s*`' + [regex]::Escape($Field) + '`\s*\|\s*(.*?)\s*\|\s*$'
    return [regex]::Match($section, $pattern).Groups[1].Value.Trim().Trim([char]'`')
}
$acceptedTarget = Get-Commitment 'TargetVersionOrCommit'
$acceptedDate = Get-Commitment 'TargetIntegrationDate'
$acceptedContract = Get-Commitment 'CompatibilityContractAndVerificationCommand'
$acceptedCommand = [regex]::Match($acceptedContract, '(?s)Command:\s*`([^`]+)`').Groups[1].Value
$integrationDate = [DateTimeOffset]::MinValue
if ((Get-Commitment 'AcceptedStatus') -cne 'Available' -or $acceptedTarget -cne $env:EXT_PARTIES_TARGET_SHA -or
    -not [DateTimeOffset]::TryParse($acceptedDate, [ref]$integrationDate) -or
    [string]::IsNullOrWhiteSpace($acceptedCommand) -or $acceptedCommand -eq 'TBD') {
    throw 'DependencyNotAvailable: EXT-PARTIES-1 requires Available, the exact accepted target/date/command and passing prerequisite evidence before any live read.'
}
try { $receipt = Get-Content -LiteralPath $env:EXT_PARTIES_COMPATIBILITY_RECEIPT -Raw | ConvertFrom-Json -AsHashtable }
catch { throw 'DependencyNotAvailable: EXT-PARTIES-1 compatibility receipt is missing or malformed.' }
if ($receipt.recordId -cne 'EXT-PARTIES-1' -or $receipt.targetVersionOrCommit -cne $acceptedTarget -or
    $receipt.compatibilityCommand -cne $acceptedCommand -or $receipt.outcome -cne 'Pass' -or
    $receipt.evidenceLevel -lt 4 -or $receipt.prerequisitesAvailable -ne $true) {
    throw 'DependencyNotAvailable: EXT-PARTIES-1 exact-target compatibility/prerequisite proof does not match the accepted record.'
}
$retention = [TimeSpan]::Zero
if (-not [TimeSpan]::TryParse($env:EXT_PARTIES_RETENTION, [ref]$retention) -or $retention -ne [TimeSpan]::FromDays(365) -or
    $env:EXT_PARTIES_EXPIRY_TRIGGER -cne 'binding-effective-at' -or
    $receipt.policyId -cne $env:EXT_PARTIES_POLICY_ID -or $receipt.retention -cne $env:EXT_PARTIES_RETENTION -or
    $receipt.expiryTrigger -cne $env:EXT_PARTIES_EXPIRY_TRIGGER) {
    throw 'DependencyNotAvailable: EXT-PARTIES-1 requires matching explicit supported production retention policy and custody prerequisites.'
}
$base = [Uri]$env:EXT_PARTIES_GATEWAY_URL
if (-not $base.IsAbsoluteUri -or $base.Scheme -notin @('http', 'https') -or $base.UserInfo -or $base.Query -or $base.Fragment) {
    throw 'The authenticated owner transport must be an absolute HTTP(S) base URL without user information.'
}
try { $headers = ConvertFrom-Json -InputObject $env:EXT_PARTIES_READER_HEADERS_JSON -AsHashtable }
catch { throw 'Owner transport headers are malformed.' }
if ($headers -isnot [System.Collections.IDictionary] -or $headers.Count -eq 0 -or
    @($headers.Keys | Where-Object { $_ -notin @('Authorization', 'dapr-api-token') }).Count) {
    throw 'Supply owner-issued Authorization and/or dapr-api-token headers; caller identity headers cannot be fabricated.'
}
$bindingVersion = 0L
$actionAt = [DateTimeOffset]::MinValue
if (-not [long]::TryParse($env:EXT_PARTIES_HISTORY_BINDING_VERSION, [ref]$bindingVersion) -or $bindingVersion -le 0 -or
    -not [DateTimeOffset]::TryParse($env:EXT_PARTIES_HISTORY_ACTION_AT, [ref]$actionAt) -or
    $env:EXT_PARTIES_HISTORY_ACTOR_ID -cnotmatch '^[0-7][0-9ABCDEFGHJKMNPQRSTVWXYZ]{25}$') {
    throw 'The owner fixture must supply an exact recorded actor, binding version and action instant.'
}

function Invoke-OwnerRead([string]$RelativePath, [hashtable]$Payload) {
    $target = [Uri]::new($base.AbsoluteUri.TrimEnd('/') + '/' + $RelativePath)
    $body = ConvertTo-Json -InputObject $Payload -Depth 12 -Compress
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [Net.Http.HttpClient]::new($handler)
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, $target)
    $timeout = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(30))
    $response = $null
    $stream = $null
    $buffered = [IO.MemoryStream]::new()
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(30)
        foreach ($name in $headers.Keys) {
            if (-not $request.Headers.TryAddWithoutValidation($name, [string]$headers[$name])) { throw 'Owner transport headers are invalid.' }
        }
        $request.Content = [Net.Http.StringContent]::new($body, [Text.Encoding]::UTF8, 'application/json')
        $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $timeout.Token).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw ('Owner read denied or unavailable at the authenticated transport (HTTP ' + [int]$response.StatusCode + ').')
        }
        $limit = 32MB
        if ($response.Content.Headers.ContentLength -gt $limit) { throw 'Owner response exceeds the source bound.' }
        $stream = $response.Content.ReadAsStreamAsync($timeout.Token).GetAwaiter().GetResult()
        $chunk = [byte[]]::new(64KB)
        while ($true) {
            # Read at most one byte beyond the limit, and reject before copying it into the buffer.
            $count = [int][Math]::Min($chunk.Length, $limit - $buffered.Length + 1)
            $received = $stream.ReadAsync($chunk, 0, $count, $timeout.Token).GetAwaiter().GetResult()
            if ($received -eq 0) { break }
            if ($buffered.Length + $received -gt $limit) { throw 'Owner response exceeds the source bound.' }
            $buffered.Write($chunk, 0, $received)
        }
        $timeout.Token.ThrowIfCancellationRequested()
        try {
            $content = [Text.UTF8Encoding]::new($false, $true).GetString($buffered.ToArray())
            return ConvertFrom-Json -InputObject $content -AsHashtable
        }
        catch { throw 'Owner response is malformed.' }
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
        if ($null -ne $response) { $response.Dispose() }
        $buffered.Dispose()
        $request.Dispose()
        $timeout.Dispose()
        $client.Dispose()
    }
}

function Get-Instant($Value) {
    # PowerShell may materialize JSON dates as DateTime; string conversion loses UTC and ticks.
    if ($Value -is [DateTimeOffset] -or $Value -is [DateTime]) {
        $parsed = [DateTimeOffset]$Value
        if ($parsed -eq [DateTimeOffset]::MinValue) { throw 'The retained basis contains an invalid instant.' }
        return $parsed
    }
    if ($Value -isnot [string]) { throw 'The retained basis contains an invalid instant.' }
    try {
        $json = ConvertTo-Json -InputObject $Value -Compress
        $parsed = [Text.Json.JsonSerializer]::Deserialize[DateTimeOffset]($json, [Text.Json.JsonSerializerOptions]$null)
    }
    catch { throw 'The retained basis contains an invalid instant.' }
    if ($parsed -eq [DateTimeOffset]::MinValue) { throw 'The retained basis contains an invalid instant.' }
    return $parsed
}

function Get-Version($Value) {
    $parsed = 0L
    if (($Value -isnot [long] -and $Value -isnot [int]) -or $Value -lt 0 -or -not [long]::TryParse([string]$Value, [ref]$parsed)) {
        throw 'The retained basis contains an invalid version or position.'
    }
    return $parsed
}

function Assert-Custody($Custody) {
    if ($null -eq $Custody -or $Custody.purpose -cne 'party-actor-history-v1' -or
        $Custody.policyId -cne $env:EXT_PARTIES_POLICY_ID -or [string]::IsNullOrWhiteSpace($Custody.evidenceId) -or
        (Get-Version $Custody.lifecycleRevision) -le 0) { throw 'The retained binding has no exact supported custody basis.' }
    foreach ($field in @('sourceExpiryEnforced', 'restoreSafe', 'derivedCopiesCovered')) {
        if ($Custody[$field] -isnot [bool] -or $Custody[$field] -ne $true) { throw 'The retained binding has incomplete custody assurances.' }
    }
    $null = Get-Instant $Custody.expiresAt
}

$identity = @{ tenantId=$env:EXT_PARTIES_TENANT_A; domain='party'; aggregateId=$env:EXT_PARTIES_HISTORY_PARTY_ID }
$retained = Invoke-OwnerRead 'api/v1/identity-history/read' @{ identity=$identity; purpose='party-actor-history-v1' }
$source = $retained.stream
if ($null -eq $source -or $retained.failureReason -or $source.purpose -ne 'party-actor-history-v1' -or
    $source.identity.tenantId -cne $identity.tenantId -or $source.identity.domain -cne 'party' -or
    $source.identity.aggregateId -cne $identity.aggregateId -or $source.head -le 0 -or $source.head -gt 10000 -or
    $null -eq $source.events -or $null -eq $source.excludedSequences -or [string]::IsNullOrWhiteSpace($source.observationId) -or [string]::IsNullOrWhiteSpace($source.authorityRevision) -or
    -not $source.observedAt -or (Get-Instant $source.observedAt) -gt [DateTimeOffset]::UtcNow -or
    -not $source.validUntil -or (Get-Instant $source.validUntil) -le [DateTimeOffset]::UtcNow) {
    throw 'Independent retained history has no exact authoritative source certificate.'
}
$covered = [Collections.Generic.HashSet[long]]::new()
$payloadBytes = 0L
$previous = 0L
foreach ($item in $source.events) {
    $position = Get-Version $item.sequenceNumber
    if ($position -le $previous -or $position -gt $source.head -or -not $covered.Add($position) -or
        $null -eq $item.protectionMetadata -or $item.protectionMetadata.state -notin @(0, 'Unprotected') -or
        $item.eventTypeName -cnotmatch '^Hexalith\.Parties\.Contracts\.Events\.HumanActorBinding(Established|Rebound|Revoked)$' -or
        $item.serializationFormat -cne 'json' -or $null -eq $item.payload -or
        $item.messageId -cne '' -or $null -ne $item.userId -or $null -ne $item.correlationId -or $null -ne $item.causationId) {
        throw 'The retained source contains an invalid original position, profile substitution or unreadable event.'
    }
    try { $payloadBytes += [Convert]::FromBase64String($item.payload).LongLength }
    catch { throw 'The retained source payload is not readable base64 JSON.' }
    if ($payloadBytes -gt 16MB) { throw 'The retained source exceeds the decoded payload bound.' }
    $previous = $position
}
$previous = 0L
foreach ($excluded in $source.excludedSequences) {
    $position = Get-Version $excluded
    if ($position -le $previous -or $position -gt $source.head -or -not $covered.Add($position)) { throw 'The source exclusion certificate overlaps or is unordered.' }
    $previous = $position
}
if ($covered.Count -ne $source.head) { throw 'The retained source certificate has a gap.' }

# Independently reconstruct all recorded transitions before trusting a query result.
$bindings = [Collections.Generic.List[hashtable]]::new()
$logicalIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$version = 0L
$observedAt = Get-Instant $source.observedAt
foreach ($item in $source.events) {
    try { $transition = ConvertFrom-Json -InputObject ([Text.UTF8Encoding]::new($false, $true).GetString([Convert]::FromBase64String($item.payload))) -AsHashtable -NoEnumerate }
    catch { throw 'The retained transition is not readable JSON.' }
    if ($transition -isnot [Collections.IDictionary]) { throw 'The retained transition must be a JSON object.' }
    $at = Get-Instant $transition.effectiveAt
    $expected = Get-Version $transition.expectedBindingVersion
    if ($at -gt $observedAt -or $expected -ne $version) { throw 'The retained transition has an inconsistent predecessor or effective instant.' }
    $revoked = $item.eventTypeName.EndsWith('HumanActorBindingRevoked', [StringComparison]::Ordinal)
    $rebound = $item.eventTypeName.EndsWith('HumanActorBindingRebound', [StringComparison]::Ordinal)
    if ($revoked -or $rebound) {
        if ($version -eq 0) { throw 'The retained transition has no predecessor.' }
        $predecessors = @($bindings | Where-Object { $_.evidence.bindingVersion -eq $expected })
        if ($predecessors.Count -eq 0 -and $revoked) { throw 'The retained revocation has no live predecessor.' }
        if ($predecessors.Count -gt 0) {
            $predecessor = $predecessors[0].evidence
            if ((Get-Instant $predecessor.validFrom) -ge $at -or $revoked -and (Get-Instant $predecessor.validUntil) -le $at) {
                throw 'The retained transition cannot close its predecessor interval.'
            }
            if ((Get-Instant $predecessor.validUntil) -gt $at) { $predecessor.validUntil = $at.ToString('O') }
        }
    }
    elseif ($version -ne 0) { throw 'A retained establishment cannot replace an existing binding.' }
    if ($revoked) {
        Assert-Custody $transition.custody
        if ((Get-Instant $transition.custody.expiresAt) -ne $at.Add($retention)) {
            throw 'The retained revocation differs from its exact retention policy.'
        }
        if ([string]::IsNullOrWhiteSpace($transition.logicalId) -or [string]::IsNullOrWhiteSpace($transition.intentDigest) -or
            -not $logicalIds.Add($transition.logicalId)) { throw 'The retained revocation has an invalid logical identity.' }
        if ($version -eq [long]::MaxValue) { throw 'The retained binding version overflows.' }
        $version++
        continue
    }
    $binding = $transition.binding
    $candidate = $binding.evidence
    if ($null -eq $candidate -or $candidate.tenantId -cne $identity.tenantId -or $candidate.partyId -cne $identity.aggregateId -or
        $candidate.actorId -cnotmatch '^[0-7][0-9ABCDEFGHJKMNPQRSTVWXYZ]{25}$' -or (Get-Version $candidate.actorRevision) -le 0 -or
        $version -eq [long]::MaxValue -or (Get-Version $candidate.bindingVersion) -ne $version + 1 -or
        (Get-Instant $candidate.validFrom) -ne $at -or (Get-Instant $candidate.validUntil) -le $at -or
        [string]::IsNullOrWhiteSpace($candidate.sourceId) -or [string]::IsNullOrWhiteSpace($candidate.provenanceId) -or
        [string]::IsNullOrWhiteSpace($binding.logicalId) -or [string]::IsNullOrWhiteSpace($binding.intentDigest) -or
        -not $logicalIds.Add($binding.logicalId)) { throw 'The retained opening binding is inconsistent or outside its scope.' }
    Assert-Custody $candidate.custody
    if ((Get-Instant $candidate.custody.expiresAt) -ne $at.Add($retention) -or
        (Get-Instant $candidate.validUntil) -ne (Get-Instant $candidate.custody.expiresAt)) {
        throw 'The retained opening binding differs from its exact retention policy.'
    }
    foreach ($previousBinding in $bindings) {
        if ((Get-Instant $previousBinding.evidence.validFrom) -ge $at -or (Get-Instant $previousBinding.evidence.validUntil) -gt $at) {
            throw 'The retained intervals overlap or are unordered.'
        }
    }
    $bindings.Add(@{ evidence=$candidate; sourcePosition=(Get-Version $item.sequenceNumber) })
    $version = Get-Version $candidate.bindingVersion
}
$matching = @($bindings | Where-Object { (Get-Instant $_.evidence.validFrom) -le $actionAt -and $actionAt -lt (Get-Instant $_.evidence.validUntil) })
if ($matching.Count -ne 1 -or $matching[0].evidence.actorId -cne $env:EXT_PARTIES_HISTORY_ACTOR_ID -or
    $matching[0].evidence.bindingVersion -ne $bindingVersion -or $actionAt -gt $observedAt) {
    throw 'The independently retained source has no unique exact action-time binding.'
}
$verifiedBinding = $matching[0]

$queryPayload = @{ tenantId=$identity.tenantId; partyId=$identity.aggregateId; actionAt=$actionAt.ToString('O');
    expectedActorId=$env:EXT_PARTIES_HISTORY_ACTOR_ID; expectedBindingVersion=$bindingVersion }
$response = Invoke-OwnerRead 'api/v1/queries' @{ tenant=$identity.tenantId; domain='party'; aggregateId=$identity.aggregateId;
    queryType='Hexalith.Parties.Contracts.Queries.ResolveHumanActorBindingAt'; projectionType='party'; entityId=$identity.aggregateId; payload=$queryPayload }
$history = $response.payload
if ($response.success -ne $true -or $response.metadata.isDegraded -eq $true -or $response.metadata.isStale -eq $true -or
    $null -eq $history -or $history.outcome -notin @(1, 'Resolved') -or $history.contractVersion -ne 1 -or
    $history.tenantId -cne $identity.tenantId -or $history.partyId -cne $identity.aggregateId -or
    (Get-Instant $history.actionAt) -ne $actionAt -or $history.evidence.actorId -cne $env:EXT_PARTIES_HISTORY_ACTOR_ID -or
    $history.evidence.bindingVersion -ne $bindingVersion -or $history.bindingSourcePosition -le 0 -or
    $history.bindingSourcePosition -gt $history.sourcePosition -or $history.sourcePosition -ne $source.head -or [string]::IsNullOrWhiteSpace($history.observationId) -or
    (Get-Instant $history.evidence.validFrom) -gt $actionAt -or $null -eq $history.evidence.validUntil -or
    (Get-Instant $history.evidence.validUntil) -le $actionAt -or $history.evidence.custody.purpose -ne 'party-actor-history-v1' -or
    $history.evidence.custody.policyId -cne $env:EXT_PARTIES_POLICY_ID -or
    $history.evidence.custody.sourceExpiryEnforced -ne $true -or $history.evidence.custody.restoreSafe -ne $true -or
    $history.evidence.custody.derivedCopiesCovered -ne $true -or $history.evidence.custody.lifecycleRevision -le 0 -or
    (Get-Instant $history.evidence.custody.expiresAt) -le [DateTimeOffset]::UtcNow) {
    throw 'The gateway did not reproduce the exact retained action-time binding and original source position.'
}
if ((Get-Version $history.bindingSourcePosition) -ne $verifiedBinding.sourcePosition -or
    (Get-Version $history.sourcePosition) -ne (Get-Version $source.head)) {
    throw 'The query checkpoint differs from the independently verified source.'
}
foreach ($field in @('tenantId', 'partyId', 'actorId', 'sourceId', 'provenanceId')) {
    if ($history.evidence[$field] -cne $verifiedBinding.evidence[$field]) { throw 'The returned binding scope or provenance differs from retained history.' }
}
foreach ($field in @('bindingVersion', 'actorRevision')) {
    if ((Get-Version $history.evidence[$field]) -ne (Get-Version $verifiedBinding.evidence[$field])) { throw 'The returned binding revision differs from retained history.' }
}
foreach ($field in @('validFrom', 'validUntil')) {
    if ((Get-Instant $history.evidence[$field]) -ne (Get-Instant $verifiedBinding.evidence[$field])) { throw 'The returned interval differs from the reconstructed retained transitions.' }
}
Assert-Custody $history.evidence.custody
foreach ($field in @('policyId', 'purpose', 'evidenceId')) {
    if ($history.evidence.custody[$field] -cne $verifiedBinding.evidence.custody[$field]) { throw 'The returned custody identity differs from retained history.' }
}
if ((Get-Version $history.evidence.custody.lifecycleRevision) -ne (Get-Version $verifiedBinding.evidence.custody.lifecycleRevision) -or
    (Get-Instant $history.evidence.custody.expiresAt) -ne (Get-Instant $verifiedBinding.evidence.custody.expiresAt)) {
    throw 'The returned custody lifecycle or expiry differs from its original retained opening basis.'
}

$current = Invoke-OwnerRead 'api/v1/queries' @{ tenant=$identity.tenantId; domain='party'; aggregateId=$identity.aggregateId;
    queryType='Hexalith.Parties.Contracts.Queries.ResolvePartyIdentity'; projectionType='party'; entityId=$identity.aggregateId;
    payload=@{ tenantId=$identity.tenantId; partyId=$identity.aggregateId; expectedActorId=$env:EXT_PARTIES_HISTORY_ACTOR_ID } }
if ($current.success -ne $true -or $null -eq $current.payload -or
    $current.metadata.isDegraded -eq $true -or $current.metadata.isStale -eq $true -or
    $current.payload.outcome -notin @(0, 2, 'Unavailable', 'Ineligible') -or $current.payload.evidence.humanBinding) {
    throw 'The owner erased-profile fixture unexpectedly remains currently eligible.'
}
if ((Get-Instant $source.observedAt) -gt [DateTimeOffset]::UtcNow -or $actionAt -gt [DateTimeOffset]::UtcNow -or
    (Get-Instant $source.validUntil) -le [DateTimeOffset]::UtcNow -or
    (Get-Instant $history.evidence.custody.expiresAt) -le [DateTimeOffset]::UtcNow) {
    throw 'The retained source certificate or custody expired during readiness probes.'
}
$evidence = @{ qualification='ReadinessOnly'; requestedTargetSha=$env:EXT_PARTIES_TARGET_SHA; observedAt=[DateTimeOffset]::UtcNow.ToString('O');
    checks=@('complete-original-position-source-partition', 'exact-action-time-actor-and-version', 'original-binding-source-position', 'current-identity-ineligible');
    sourceHead=$history.sourcePosition; bindingSourcePosition=$history.bindingSourcePosition;
    incomplete=@('P-01-P-10-installed-matrix', 'independent-erasure-certification', 'persisted-restart-restore', 'failure-injection', 'production-custody-destruction-and-copy-cleanup') }
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$evidence | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $EvidenceDirectory 'live-readiness.json')
Write-Host 'Authenticated retained-history readiness probes passed for the owner-provided fixture. Erasure certification and complete Live qualification remain unavailable.'
