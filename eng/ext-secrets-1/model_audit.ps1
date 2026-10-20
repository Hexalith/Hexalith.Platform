# Local-only evidence guard. No qualification, provider or acceptance behavior.
function Invoke-CustodySharedCutModelAudit {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ReportPath,
        [Parameter(Mandatory)][string]$XmlPath,
        [Parameter(Mandatory)][scriptblock]$RunTests
    )
    # Delete THIS audit's prior outputs before invoking the test process, even if execution fails.
    if (Test-Path -LiteralPath $ReportPath) { Remove-Item -LiteralPath $ReportPath -Force }
    if (Test-Path -LiteralPath $XmlPath) { Remove-Item -LiteralPath $XmlPath -Force }
    $priorReport = $env:HEXALITH_CUSTODY_MODEL_REPORT
    try {
        $env:HEXALITH_CUSTODY_MODEL_REPORT = $ReportPath
        & $RunTests
    }
    finally { $env:HEXALITH_CUSTODY_MODEL_REPORT = $priorReport }
    if (-not (Test-Path -LiteralPath $XmlPath -PathType Leaf)) { throw 'Fresh shared-cut model test XML was not produced.' }
    [xml]$modelResults = Get-Content -LiteralPath $XmlPath -Raw
    $producer = 'BoundedExplorationAndUnsafeCounterexamplesProduceValueFreeEvidence'
    $producingTests = @($modelResults.SelectNodes('//test') | Where-Object {
        $_.type -eq 'Hexalith.Platform.Custody.Tests.CustodySharedCutModelTests' -and $_.method -eq $producer
    })
    if ($producingTests.Count -ne 1 -or $producingTests[0].result -ne 'Pass') {
        throw 'The specific shared-cut model evidence-producing test must execute and pass exactly once.'
    }
    if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) { throw 'Fresh shared-cut model report was not produced.' }
    $report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    if ($report.Kind -ne 'OfflineModelOnly' -or $report.EvidenceProducer -ne $producer -or $report.LiveReady -ne $false -or $report.DependencyAvailable -ne $false) {
        throw 'Shared-cut model report must be value-free offline evidence from the required producer.'
    }
    return (Get-FileHash -LiteralPath $ReportPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
