# Synthetic tests only. Execute the actual pure MQL decision function bodies in a
# C# host (same scalar syntax); this does NOT execute MQL terminal APIs.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5Probe.SyntheticFixtures.ps1')
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$tools = Join-Path $repo 'tools/market-data/mt5'
$kernelSource = [System.IO.File]::ReadAllText((Join-Path $tools 'MoneyWayCollectorDecision.mqh'))
$hostSource = [regex]::Replace($kernelSource, '(?m)^(int|bool|string) (Collector\w+)\(', 'public static $1 $2(')
Add-Type -TypeDefinition ('public static class MoneyWayCollectorKernel {' + $hostSource + '}')
$passed = 0; $failed = 0
function Check([bool]$Condition) { if (-not $Condition) { throw 'Assertion failed.' } }
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}

Case 'Initial observation requests an initial snapshot, not a new-bar assertion' { Check ([MoneyWayCollectorKernel]::CollectorAction($false,0,-1,100,0,$true,$true) -eq 1) }
Case 'Unavailable startup is still diagnosed as initial, with no fake M1' { Check ([MoneyWayCollectorKernel]::CollectorAction($false,2,-1,0,0,$true,$true) -eq 1) }
Case 'Strictly newer native M1 identity triggers acquisition' { Check ([MoneyWayCollectorKernel]::CollectorAction($true,0,0,160,100,$false,$false) -eq 2) }
Case 'Same identity does not trigger a new bar' { Check ([MoneyWayCollectorKernel]::CollectorAction($true,0,0,100,100,$true,$false) -eq 0) }
Case 'Repeated timer events and market pause do not invent bars or failure' {
    for ($i=0; $i -lt 100; $i++) { Check ([MoneyWayCollectorKernel]::CollectorAction($true,0,0,100,100,$true,$false) -eq 0) }
}
Case 'Disconnected source is explicit, independent of bar age' { Check ([MoneyWayCollectorKernel]::CollectorCondition($true,$false,$false,0,100) -eq 1) }
Case 'Reconnect with the same native identity records recovery, not a new M1' { Check ([MoneyWayCollectorKernel]::CollectorAction($true,0,1,100,100,$false,$false) -eq 3) }
Case 'Reconnect with a genuinely newer M1 records the native change' { Check ([MoneyWayCollectorKernel]::CollectorAction($true,0,1,160,100,$false,$false) -eq 2) }
Case 'Unavailable M1 synchronization remains unavailable' { Check ([MoneyWayCollectorKernel]::CollectorCondition($true,$true,$false,100,100) -eq 2) }
Case 'Repeated unavailability is suppressed until technical retry is due' { Check ([MoneyWayCollectorKernel]::CollectorAction($true,2,2,0,100,$false,$true) -eq 0) }
Case 'Unavailable diagnostic retry is not called a new bar' { Check ([MoneyWayCollectorKernel]::CollectorAction($true,2,2,0,100,$true,$true) -eq 4) }
Case 'Regressed identity is quarantined, not called a new M1' {
    Check ([MoneyWayCollectorKernel]::CollectorCondition($true,$true,$true,40,100) -eq 3)
    Check ([MoneyWayCollectorKernel]::CollectorAction($true,3,0,40,100,$true,$false) -eq 5)
}
Case 'Incomplete higher-timeframe sample retries without a fabricated M1 event' { Check ([MoneyWayCollectorKernel]::CollectorAction($true,0,0,100,100,$true,$true) -eq 6) }
Case 'Failed publication does not create rapid duplicate attempts for one head' { Check (-not [MoneyWayCollectorKernel]::CollectorAttemptAllowed(2,$false,$false,160,160)) }
Case 'A different head is not lost during retry backoff' { Check ([MoneyWayCollectorKernel]::CollectorAttemptAllowed(2,$false,$false,220,160)) }
Case 'Same-head retry becomes eligible at the explicit technical retry boundary' { Check ([MoneyWayCollectorKernel]::CollectorAttemptAllowed(2,$true,$false,160,160)) }
Case 'Wrong source or enabled Algo Trading requests stop before acquisition' { Check ([MoneyWayCollectorKernel]::CollectorCondition($false,$true,$true,160,100) -eq 4); Check ([MoneyWayCollectorKernel]::CollectorAction($true,4,0,160,100,$true,$false) -eq 7) }
Case 'Restart begins a new initial observation without assuming prior UTC availability' { Check ([MoneyWayCollectorKernel]::CollectorAction($false,0,-1,100,0,$true,$true) -eq 1) }
Case 'Actual event labels preserve recovery versus new native identity' { Check ([MoneyWayCollectorKernel]::CollectorEventName(2) -ceq 'new_native_m1'); Check ([MoneyWayCollectorKernel]::CollectorEventName(3) -ceq 'source_recovery') }
Case 'An existing record sequence cannot be reused' { Check (-not [MoneyWayCollectorKernel]::CollectorSequenceFree(1,$true,$false)) }
Case 'An orphan probe sequence cannot be reused or overwritten' { Check (-not [MoneyWayCollectorKernel]::CollectorSequenceFree(1,$false,$true)) }
Case 'Only free bounded positive sequences are eligible for publication' {
    Check ([MoneyWayCollectorKernel]::CollectorSequenceFree(2,$false,$false))
    Check (-not [MoneyWayCollectorKernel]::CollectorSequenceFree(0,$false,$false))
    Check (-not [MoneyWayCollectorKernel]::CollectorSequenceFree(1000000000000,$false,$false))
}

$directory = Join-Path ([System.IO.Path]::GetTempPath()) ('moneyway-collector-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$probePath = Join-Path $directory 'probe-000000000001.json'
$recordPath = Join-Path $directory 'record-000000000001.json'
$nextPath = Join-Path $directory 'record-000000000002.json'
$validator = Join-Path $tools 'Test-Mt5CollectorRecord.ps1'
function Save($Value,[string]$Path) { [System.IO.File]::WriteAllText($Path,($Value | ConvertTo-Json -Depth 24),(New-Object System.Text.UTF8Encoding($false))) }
function NewRecord {
    return @{
        schema='moneyway.mt5-read-only-collector'; version=1; diagnostic_only=$true; canonical_ready=$false; utc_conversion_authenticated=$false
        sequence=1; run_id='run-000000000001'; poll_sequence=1; event='initial_snapshot'; condition=0; timer_seconds=5; retry_every_polls=12; sample_bars=2
        source=@{company='OANDA Global Markets Limited'; server='OANDA_Global-Demo-1'; demo=$true; symbol='US100'}
        trigger_m1_head_native=(Native '2026.01.05 12:00:00'); previous_committed_m1_head_native=$null
        captured_m1_head_native=(Native '2026.01.05 12:00:00'); m1_usable=$true; sample_complete=$true; source_stable=$true
        previous_record=$null; previous_record_sha256=$null; probe_file='probe-000000000001.json'
        probe_sha256=(Get-FileHash -LiteralPath $probePath).Hash.ToLowerInvariant(); shutdown_reason=$null
        sequence_basis='local_single_writer_publication_not_utc'
    }
}
function ReceiptCase([string]$Name,[scriptblock]$Change,[string]$Reject='') {
    Case $Name {
        $fixture = Fixture; $fixture.source.company='OANDA Global Markets Limited'; Save $fixture $probePath
        $record = NewRecord
        & $Change $record $fixture
        Save $fixture $probePath
        $record.probe_sha256=(Get-FileHash -LiteralPath $probePath).Hash.ToLowerInvariant()
        Save $record $recordPath
        $caught=$null
        try { $result=& $validator -Path $recordPath } catch { $caught=$_.Exception.Message }
        if ($Reject) { Check ($null -ne $caught -and $caught -match $Reject) }
        else { Check ($null -eq $caught); Check ($result.ContractValid -and -not $result.CanonicalReady -and -not $result.AuthenticityAndUtcVerified) }
    }
}
try {
    ReceiptCase 'Four timeframes and forming/historical roles reuse the existing probe validator' { param($r,$f) }
    ReceiptCase 'Native timestamps remain raw with no UTC conversion' { param($r,$f) $f.timeframes[0].bars[0].open_time_utc='2026-01-05T12:00:00Z' } 'UTC/closed/availability'
    ReceiptCase 'Invalid OHLC remains flagged diagnostic, never complete' { param($r,$f) $f.timeframes[1].bars[0].high=1; $f.timeframes[1].bars[0].ohlc_valid=$false; $r.sample_complete=$false }
    ReceiptCase 'Invalid OHLC cannot be called a complete sample' { param($r,$f) $f.timeframes[1].bars[0].high=1; $f.timeframes[1].bars[0].ohlc_valid=$false } 'Incomplete/invalid'
    ReceiptCase 'Unavailable higher timeframe is retained, not silently complete' { param($r,$f)
        $t=$f.timeframes[2]; $t.status='copy_failed'; $t.copy_return=-1; $t.copy_error_code=4401; $t.bars=@(); $t.head_consistent=$false; $r.sample_complete=$false
    }
    ReceiptCase 'Unsynchronized M1 cannot be marked usable' { param($r,$f) $f.timeframes[0].synchronized_after.value=0 } 'Unusable M1'
    ReceiptCase 'Duplicate native timestamps fail before receipt acceptance' { param($r,$f) $f.timeframes[0].bars[1].open_time_native=$f.timeframes[0].bars[0].open_time_native } 'chronology/delta'
    ReceiptCase 'Timer-only duplicate cannot claim a new M1' { param($r,$f) $r.event='new_native_m1'; $r.previous_committed_m1_head_native=$r.trigger_m1_head_native } 'duplicate/regressed'
    ReceiptCase 'Unexpected account-identifying metadata is rejected' { param($r,$f) $r.account_login='synthetic-forbidden' } 'Unexpected metadata'
    ReceiptCase 'Escaping the receipt directory is rejected' { param($r,$f) $r.probe_file='../other.json' } 'probe reference'
    ReceiptCase 'Metadata cannot assert canonical readiness' { param($r,$f) $r.canonical_ready=$true } 'boundary violated'
    ReceiptCase 'Metadata cannot silently coerce a textual flag into truth' { param($r,$f) $r.source_stable='false' } 'must be boolean'
    ReceiptCase 'Metadata native display must agree with its preserved integer' { param($r,$f) $r.trigger_m1_head_native.display='2026.01.05 12:00:01' } 'representation mismatch'
    Case 'Probe integrity mismatch is rejected without modifying files' {
        $record=NewRecord; $record.probe_sha256=('0'*64); Save $record $recordPath
        $before=(Get-FileHash -LiteralPath $probePath).Hash; $blocked=$false
        try { & $validator -Path $recordPath | Out-Null } catch { $blocked=$_.Exception.Message -match 'Probe hash mismatch' }
        Check ($blocked -and (Get-FileHash -LiteralPath $probePath).Hash -ceq $before)
    }
    Case 'Shutdown/restart preserves prior record hash and does not fabricate a probe' {
        $record=NewRecord; Save $record $recordPath
        $next=NewRecord; $next.sequence=2; $next.run_id='run-000000000001'; $next.event='shutdown'; $next.probe_file=$null; $next.probe_sha256=$null
        $next.previous_record='record-000000000001.json'; $next.previous_record_sha256=(Get-FileHash -LiteralPath $recordPath).Hash.ToLowerInvariant()
        $next.shutdown_reason=1; $next.m1_usable=$false; $next.sample_complete=$false; $next.source_stable=$false; $next.captured_m1_head_native=$null
        Save $next $nextPath
        Check ((& $validator -Path $nextPath).Event -ceq 'shutdown')
        $next.previous_record_sha256=('0'*64); Save $next $nextPath; $blocked=$false
        try { & $validator -Path $nextPath | Out-Null } catch { $blocked=$_.Exception.Message -match 'Previous-record hash mismatch' }
        Check $blocked
    }
} finally {
    foreach ($path in @($probePath,$recordPath,$nextPath)) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
    Remove-Item -LiteralPath $directory
}

Case 'Collector source calls/metadata restricted to explicit non-trading allowlist' {
    $main=[System.IO.File]::ReadAllText((Join-Path $tools 'MoneyWayReadOnlyCollector.mq5'))
    $snapshot=[System.IO.File]::ReadAllText((Join-Path $tools 'MoneyWayCollectorSnapshot.mqh'))
    $allowed=@('CollectorCondition','CollectorAction','CollectorAttemptAllowed','CollectorSequenceFree','CollectorEventName','SourceSafe','SequenceText','HashBytes','HashFile','Publish','RecoverSequence','ReserveSequence','Record','PollOnce','OnInit','OnTimer','OnDeinit',
        'BoolJson','Quote','NativeTime','NumberJson','ValidOhlc','SeriesProperty','ProbeTimeframe','CaptureProbe','if','for','while',
        'ResetLastError','AccountInfoInteger','AccountInfoString','GetLastError','MQLInfoInteger','TerminalInfoInteger','StringFormat','CryptEncode','FileOpen','FileSize','FileClose','FileReadArray','StringToCharArray','ArrayResize','ArraySize','FileIsExist','GetMicrosecondCount','FileWriteArray','FileFlush','FileMove','FileFindFirst','FileFindNext','FileFindClose','StringLen','StringSubstr','StringGetCharacter','StringToInteger','Print','SymbolExist','SymbolSelect','SeriesInfoInteger','EventKillTimer','ExpertRemove','EventSetTimer','ShortToString','TimeToString','MathIsValidNumber','DoubleToString','PeriodSeconds','ArraySetAsSeries','CopyRates','SymbolInfoString','SymbolInfoInteger','TimeGMT','TimeCurrent')
    $text=$main + $snapshot + $kernelSource
    $code=[regex]::Replace($text,'//[^\r\n]*|"(?:\\.|[^"\\])*"',' ')
    foreach ($call in [regex]::Matches($code,'\b([A-Za-z_]\w*)\s*\(')) { Check ($allowed -ccontains $call.Groups[1].Value) }
    foreach ($field in [regex]::Matches($code,'\b(?:ACCOUNT|TERMINAL|SYMBOL|MQL)_[A-Z_]+\b')) {
        Check (@('ACCOUNT_TRADE_MODE','ACCOUNT_TRADE_MODE_DEMO','ACCOUNT_SERVER','ACCOUNT_COMPANY','TERMINAL_CONNECTED','TERMINAL_BUILD','TERMINAL_TRADE_ALLOWED','MQL_TESTER','SYMBOL_DESCRIPTION','SYMBOL_CURRENCY_PROFIT','SYMBOL_DIGITS') -ccontains $field.Value)
    }
    Check ($main.Contains('FileMove(temporary, FILE_COMMON, target, FILE_COMMON)'))
    Check (-not ($code -match '\b(FILE_REWRITE|OrderSend|OrderSendAsync|CTrade|WebRequest|SocketCreate)\b'))
    Check ($main.Contains('EventKillTimer();') -and $main.Contains('FileClose(lock_handle)'))
    $includes=@([regex]::Matches($main,'(?m)^#include "([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
    Check ($includes.Count -eq 2 -and $includes -ccontains 'MoneyWayCollectorDecision.mqh' -and $includes -ccontains 'MoneyWayCollectorSnapshot.mqh')
}
Case 'Manual probe and copied primitive serialization logic remain unchanged' {
    $manual=[System.IO.File]::ReadAllText((Join-Path $tools 'MoneyWayReadOnlyProbe.mq5'))
    $snapshot=[System.IO.File]::ReadAllText((Join-Path $tools 'MoneyWayCollectorSnapshot.mqh'))
    $manualPart=$manual.Substring($manual.IndexOf('string BoolJson'),$manual.IndexOf('string ProbeTimeframe')-$manual.IndexOf('string BoolJson'))
    $copiedPart=$snapshot.Substring($snapshot.IndexOf('string BoolJson'),$snapshot.IndexOf('string ProbeTimeframe')-$snapshot.IndexOf('string BoolJson'))
    Check ($manualPart -ceq $copiedPart)
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { throw 'MT5 collector checks failed.' }
