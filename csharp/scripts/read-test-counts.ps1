[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CoreResult,
    [Parameter(Mandatory = $true)][string]$IoResult,
    [Parameter(Mandatory = $true)][string]$DesktopResult
)

$ErrorActionPreference = "Stop"

function Read-TrxCounters {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "TRX result does not exist: $Path"
    }

    [xml]$document = Get-Content -LiteralPath $Path -Raw
    $counters = $document.SelectSingleNode(
        "//*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw "TRX result is missing counters: $Path"
    }

    return [pscustomobject]@{
        total = [int]$counters.total
        failed = [int]$counters.failed
        skipped = [int]$counters.notExecuted
    }
}

$core = Read-TrxCounters -Path $CoreResult
$io = Read-TrxCounters -Path $IoResult
$desktop = Read-TrxCounters -Path $DesktopResult

[ordered]@{
    total = $core.total + $io.total + $desktop.total
    core = $core.total
    io = $io.total
    desktop = $desktop.total
    failed = $core.failed + $io.failed + $desktop.failed
    skipped = $core.skipped + $io.skipped + $desktop.skipped
} | ConvertTo-Json -Compress
