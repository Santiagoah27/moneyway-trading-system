# All fixtures are synthetic. No terminal, network, live data or broker is used.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5Probe.SyntheticFixtures.ps1')
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$comparator = Join-Path $repo 'tools/market-data/mt5/Compare-Mt5ProbeArtifacts.ps1'
$directory = Join-Path ([System.IO.Path]::GetTempPath()) ('moneyway-compare-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$earlierPath = Join-Path $directory 'earlier.json'
$laterPath = Join-Path $directory 'later.json'
$reportPath = Join-Path $directory 'report.json'
$passed = 0; $failed = 0

function Check([bool]$Value, [string]$Message = 'Assertion failed') { if (-not $Value) { throw $Message } }
function Save($Data, [string]$Path) {
    [System.IO.File]::WriteAllText($Path, ($Data | ConvertTo-Json -Depth 24), (New-Object System.Text.UTF8Encoding($false)))
}
function ShiftFrame($Frame, [int]$Steps, [int]$Count = 2) {
    $head = $Frame.bars[0].open_time_native.raw + $Steps * $Frame.period_seconds
    $template = $Frame.bars[0] | ConvertTo-Json -Depth 8
    $bars = @()
    for ($i = 0; $i -lt $Count; $i++) {
        $bar = $template | ConvertFrom-Json
        $bar.series_position = $i
        $bar.open_time_native = Native (([datetime]'1970-01-01').AddSeconds($head-$i*$Frame.period_seconds).ToString('yyyy.MM.dd HH:mm:ss'))
        $bar.role = if ($i -eq 0) { 'current_bar_completion_unverified' } else { 'historical_preceding_bar' }
        $bar.newer_than_next_seconds = if ($i+1 -lt $Count) { $Frame.period_seconds } else { $null }
        $bars += $bar
    }
    $Frame.bars = $bars; $Frame.copy_return = $Count; $Frame.requested_count = [math]::Max(2,$Count)
    $Frame.last_bar_before.value = $head; $Frame.last_bar_after.value = $head
    $Frame.status = if ($Count -lt $Frame.requested_count) { 'partial_sample' } else { 'sample_returned' }
}
function Case([string]$Name, [scriptblock]$Change, [scriptblock]$Assert = {}, [string]$RejectReason = '', [switch]$Unattested) {
    try {
        $a = Fixture; $b = Fixture
        & $Change $a $b
        Save $a $earlierPath; Save $b $laterPath
        $hashA = (Get-FileHash -LiteralPath $earlierPath).Hash
        $hashB = (Get-FileHash -LiteralPath $laterPath).Hash
        $caught = $null
        try { $r = & $comparator -EarlierPath $earlierPath -LaterPath $laterPath -CaptureOrderAttested:(-not $Unattested) 6>$null }
        catch { $caught = $_.Exception.Message }
        if ($RejectReason) { Check ($null -ne $caught -and $caught -match $RejectReason) "Expected rejection matching '$RejectReason'; got '$caught'" }
        else {
            Check ($null -eq $caught) "Unexpected rejection: $caught"
            Check (-not $r.canonical_ready -and -not $r.utc_conversion_authenticated -and $null -eq $r.provider_confirmed_closed -and $null -eq $r.historical_available_at_utc)
            & $Assert $r
        }
        Check ((Get-FileHash -LiteralPath $earlierPath).Hash -ceq $hashA -and (Get-FileHash -LiteralPath $laterPath).Hash -ceq $hashB) 'Input mutation detected'
        $script:passed++; Write-Output "PASS: $Name"
    } catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}

try {
    Case 'Identical artifacts have unchanged overlaps and matching actual hashes' {} { param($r)
        Check ($r.earlier.sha256 -ceq $r.later.sha256)
        foreach ($f in $r.timeframes) { Check ($f.unchanged_count -eq 2 -and $f.changed_count -eq 0 -and $f.newly_present_count -eq 0) }
    }
    Case 'One new M1 bar, bounded eviction and prior current progression' { param($a,$b) ShiftFrame $b.timeframes[0] 1 } { param($r)
        $f = $r.timeframes[0]; Check ($f.newly_present_count -eq 1 -and $f.absent_from_later_sample_count -eq 1)
        Check ($f.role_transitions.Count -eq 1 -and $f.role_transitions[0].later_role -ceq 'historical_preceding_bar')
    }
    Case 'Multiple newly present bars' { param($a,$b) ShiftFrame $b.timeframes[0] 2 4 } { param($r) Check ($r.timeframes[0].newly_present_count -eq 2) }
    Case 'No new bars is not a disconnection verdict' {} { param($r) Check ($r.limitations -contains 'no_new_bars_is_not_disconnection') }
    Case 'Current bar changes may be forming evolution' { param($a,$b) $b.timeframes[0].bars[0].close = 101.5 } { param($r)
        Check ($r.timeframes[0].overlapping[0].classification -ceq 'current_bar_change_may_be_forming_evolution')
    }
    Case 'Previous current changes while progressing to historical' { param($a,$b) ShiftFrame $b.timeframes[0] 1; $b.timeframes[0].bars[1].close = 101.5 } { param($r)
        Check ($r.timeframes[0].overlapping[0].classification -ceq 'current_to_historical_changed_completion_unverified')
    }
    Case 'Historical discrepancy has no invented cause' { param($a,$b) $b.timeframes[0].bars[1].close = 101.5 } { param($r)
        Check ($r.timeframes[0].overlapping[1].classification -ceq 'historical_value_discrepancy_requires_investigation')
    }
    Case 'No overlap prevents revision assessment' { param($a,$b) ShiftFrame $b.timeframes[0] 10 } { param($r)
        Check ($r.timeframes[0].overlapping.Count -eq 0 -and $r.timeframes[0].limitations -contains 'no_overlap_cannot_assess_value_changes')
    }
    Case 'Duplicate/conflicting native identities rejected' { param($a,$b)
        $b.timeframes[0].bars[1].open_time_native = $b.timeframes[0].bars[0].open_time_native
    } -RejectReason 'duplicate/conflicting native bar identity'
    Case 'Different provider rejected' { param($a,$b) $b.source.company = 'SYNTHETIC OTHER PROVIDER' } -RejectReason 'source identity: company'
    Case 'Different server rejected by existing validator' { param($a,$b) $b.source.server = 'SYNTHETIC OTHER SERVER' } -RejectReason 'demo/server gate'
    Case 'Different symbol rejected' { param($a,$b) $b.symbol.native = 'USTEC' } -RejectReason 'native symbol'
    Case 'Missing timeframe rejected without manufacturing rows' { param($a,$b) $b.timeframes = @($b.timeframes[0]) } -RejectReason 'exactly M1/M5/H1/H4'
    Case 'Unavailable timeframe is non-comparable rather than all absent' { param($a,$b)
        $f = $b.timeframes[0]; $f.copy_return = -1; $f.copy_error_code = 4401; $f.status = 'copy_failed'; $f.bars = @(); $f.head_consistent = $false
    } { param($r) Check (-not $r.timeframes[0].comparable -and $null -eq $r.timeframes[0].newly_present_count -and $r.timeframes[0].later_sample.copy_error_code -eq 4401) }
    Case 'Invalid OHLC flagged diagnostic cannot be compared' { param($a,$b) $b.timeframes[0].bars[0].high = 1; $b.timeframes[0].bars[0].ohlc_valid = $false } -RejectReason 'invalid OHLC'
    Case 'Invalid schema rejected' { param($a,$b) $b.schema = 'synthetic.other' } -RejectReason 'schema/version'
    Case 'UTC remains unauthenticated despite computer/server candidates' {} { param($r) Check (-not $r.utc_conversion_authenticated) }
    Case 'Terminal build differences preserved without blocking' { param($a,$b) $b.source.terminal_build = 9999 } { param($r)
        Check ($r.earlier.terminal_build -eq 0 -and $r.later.terminal_build -eq 9999)
    }
    Case 'Partial sample is comparable only as observed data' { param($a,$b) $b.timeframes[0].requested_count = 3; $b.timeframes[0].status = 'partial_sample' } { param($r)
        Check ($r.timeframes[0].comparable -and $r.timeframes[0].limitations -contains 'later_partial_sample')
    }
    Case 'Head races keep classification uncertain' { param($a,$b)
        $b.timeframes[0].head_consistent = $false
        foreach ($bar in $b.timeframes[0].bars) { $bar.role = 'unverified_head' }
        $b.timeframes[0].bars[0].close = 101.5
    } { param($r) Check ($r.timeframes[0].overlapping[0].classification -ceq 'unverified_or_changed_role_discrepancy') }
    Case 'No order attestation is explicitly uncertain' {} { param($r) Check ($r.capture_order -ceq 'unverified_caller_labels_only') } -Unattested
    Case 'Regressed native head is reported without reordering' { param($a,$b) ShiftFrame $b.timeframes[0] -1 } { param($r)
        Check ($r.timeframes[0].limitations -contains 'native_head_regressed_capture_order_or_history_requires_review')
    }
    Case 'Negative OHLC is compared exactly without an invented positive-price rule' { param($a,$b)
        foreach ($d in @($a,$b)) { foreach ($f in $d.timeframes) { foreach ($bar in $f.bars) {
            $bar.open = -100.25; $bar.high = -99.75; $bar.low = -102.5; $bar.close = -101.25
        } } }
        $b.timeframes[0].bars[0].close = -101.5
    } { param($r) Check ($r.timeframes[0].changed_count -eq 1 -and $r.timeframes[0].unchanged_count -eq 1) }
    Case 'Unattempted timeframe retains null counts and its actual status' { param($a,$b)
        $f = $b.timeframes[0]; $f.status = 'not_attempted'; $f.copy_return = $null; $f.copy_error_code = $null; $f.bars = @()
    } { param($r) Check (-not $r.timeframes[0].comparable -and $r.timeframes[0].later_sample.status -ceq 'not_attempted') }
    Case 'Native discontinuities remain observations without calendar repair' { param($a,$b)
        $f = $b.timeframes[0]
        $f.bars[1].open_time_native = Native '2026.01.05 11:55:00'; $f.bars[0].newer_than_next_seconds = 300
    } { param($r) Check ($r.timeframes[0].later_native_discontinuities.Count -eq 1 -and
        $r.timeframes[0].later_native_discontinuities[0].classification -ceq 'native_interval_difference_cause_unresolved') }

    # Lexical tests bypass JSON serialization only to preserve the literals under test.
    Save (Fixture) $earlierPath; Save (Fixture) $laterPath
    $original = [System.IO.File]::ReadAllText($laterPath)
    $escaped = $original.Replace('2026.01.05', '\u0032' + '026.01.05')
    [System.IO.File]::WriteAllText($laterPath, $escaped)
    $r = & $comparator -EarlierPath $earlierPath -LaterPath $laterPath 6>$null
    Check ($r.timeframes[0].unchanged_count -eq 2)
    $passed++; Write-Output 'PASS: Equivalent escaped timestamp display formatting uses the raw identity'

    [System.IO.File]::WriteAllText($laterPath, $original.Replace('101.25', '101.25000000000000001'))
    $r = & $comparator -EarlierPath $earlierPath -LaterPath $laterPath 6>$null
    Check ($r.timeframes[0].changed_count -eq 2 -and $r.timeframes[0].overlapping[0].later.ohlc_literals.close -ceq '101.25000000000000001')
    $passed++; Write-Output 'PASS: Sub-double-resolution changes retained exactly with no tolerance'

    [System.IO.File]::WriteAllText($laterPath, $original.Replace('101.25', '1.012500e2'))
    $r = & $comparator -EarlierPath $earlierPath -LaterPath $laterPath 6>$null
    Check ($r.timeframes[0].unchanged_count -eq 2 -and $r.timeframes[0].overlapping[0].later.ohlc_literals.close -ceq '1.012500e2')
    $passed++; Write-Output 'PASS: Numerically identical formats retain original tokens without false discrepancies'

    [System.IO.File]::WriteAllText($laterPath, $original.Replace('102.5', '101.249999999999999999'))
    $rejected = $false
    try { & $comparator -EarlierPath $earlierPath -LaterPath $laterPath 6>$null | Out-Null } catch { $rejected = $_.Exception.Message -match 'exact OHLC inequality|OHLC validation flag' }
    Check $rejected
    $passed++; Write-Output 'PASS: Invalid precise OHLC rejected by validator or exact comparator check'

    Save (Fixture) $laterPath
    $beforeA = (Get-FileHash -LiteralPath $earlierPath).Hash; $beforeB = (Get-FileHash -LiteralPath $laterPath).Hash
    $r1 = & $comparator -EarlierPath $earlierPath -LaterPath $laterPath -ReportPath $reportPath 6>$null
    $r2 = & $comparator -EarlierPath $earlierPath -LaterPath $laterPath 6>$null
    Check (($r1 | ConvertTo-Json -Depth 24) -ceq ($r2 | ConvertTo-Json -Depth 24))
    $passed++; Write-Output 'PASS: Identical inputs produce deterministic report and JSON file'
    $beforeReport = (Get-FileHash -LiteralPath $reportPath).Hash
    $blocked = $false
    try { & $comparator -EarlierPath $earlierPath -LaterPath $laterPath -ReportPath $reportPath 6>$null | Out-Null } catch { $blocked = $true }
    Check ($blocked -and (Get-FileHash -LiteralPath $reportPath).Hash -ceq $beforeReport)
    $passed++; Write-Output 'PASS: Existing report never overwritten'
    $blocked = $false
    try { & $comparator -EarlierPath $earlierPath -LaterPath $laterPath -ReportPath $earlierPath 6>$null | Out-Null } catch { $blocked = $true }
    Check ($blocked -and (Get-FileHash -LiteralPath $earlierPath).Hash -ceq $beforeA -and (Get-FileHash -LiteralPath $laterPath).Hash -ceq $beforeB)
    $passed++; Write-Output 'PASS: Inputs cannot be overwritten, including as report target'
} catch { $failed++; Write-Output "FAIL: lexical/report checks - $($_.Exception.Message)" }
finally {
    # Only the three explicitly owned temporary files; no recursive deletion.
    foreach ($path in @($earlierPath,$laterPath,$reportPath)) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
    Remove-Item -LiteralPath $directory
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { throw 'MT5 comparator checks failed.' }
