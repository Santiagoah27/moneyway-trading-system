[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateRange(1, 900)][int]$SessionSeconds,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$')][string]$ReferenceHost,
    [Parameter(Mandatory = $true)][ValidateRange(1000, 30000)][int]$QueryTimeoutMilliseconds
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1')

$root = Join-Path $env:APPDATA 'MetaQuotes\Terminal\Common\Files\MoneyWayCorrelatedTick'
Invoke-Mt5TickSession -Root $root -SessionSeconds $SessionSeconds -ReferenceHost $ReferenceHost -QueryTimeoutMilliseconds $QueryTimeoutMilliseconds
