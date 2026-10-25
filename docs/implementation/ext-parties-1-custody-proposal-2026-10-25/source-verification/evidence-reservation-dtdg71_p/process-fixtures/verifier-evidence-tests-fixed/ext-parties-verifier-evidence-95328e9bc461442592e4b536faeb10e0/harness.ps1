param([string]$Script, [string]$Mode, [string]$EvidenceDirectory,
    [string]$DependencyRegisterPath, [string]$Fixture, [string]$Participant,
    [switch]$Synchronize)
$ErrorActionPreference = 'Stop'
$lookupLog = Join-Path $Fixture ($Participant + '.lookups')
$writeLog = Join-Path $Fixture ($Participant + '.writes')
function dotnet {
    [IO.File]::AppendAllText($lookupLog, "dotnet`n")
    throw 'Unexpected Local dependency lookup.'
}
function Test-Path {
    [CmdletBinding()]
    param([string]$LiteralPath, [string]$Path, [string]$PathType)
    if ($LiteralPath -eq $DependencyRegisterPath -or $Path -eq $DependencyRegisterPath) {
        [IO.File]::AppendAllText($lookupLog, "register`n")
        throw 'Unexpected readiness dependency lookup.'
    }
    Microsoft.PowerShell.Management\Test-Path @PSBoundParameters
}
function Set-Content {
    [CmdletBinding()]
    param([string]$Path, [string]$LiteralPath, [object]$Value)
    [IO.File]::AppendAllText($writeLog, "evidence-write`n")
    Microsoft.PowerShell.Management\Set-Content @PSBoundParameters
}
if ($Synchronize) {
    function New-Item {
        [CmdletBinding()]
        param([string]$ItemType, [string]$Path)
        if ($ItemType -ne 'Directory' -or $Path -ne $EvidenceDirectory) {
            throw 'Unexpected fixture directory creation.'
        }
        # Both real scripts have passed their existing-path check before
        # either directory creation returns. Model the nonexclusive API
        # returning success to both contenders, independent of scheduling.
        [IO.File]::WriteAllText((Join-Path $Fixture ($Participant + '.ready')), 'ready')
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while (-not [IO.File]::Exists((Join-Path $Fixture 'release'))) {
            if ([DateTime]::UtcNow -ge $deadline) { throw 'Reservation barrier timed out.' }
            [Threading.Thread]::Sleep(10)
        }
        [IO.Directory]::CreateDirectory($Path)
    }
}
try {
    if ($Mode -eq 'DirectReadiness') {
        & $Script -EvidenceDirectory $EvidenceDirectory -DependencyRegisterPath $DependencyRegisterPath
    }
    else {
        & $Script -Mode $Mode -EvidenceDirectory $EvidenceDirectory -DependencyRegisterPath $DependencyRegisterPath
    }
    if ($null -ne $LASTEXITCODE) { exit $LASTEXITCODE }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}