# Standalone focused checks; no Pester, terminal, network, or broker dependency.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$validator = Join-Path $repo 'tools/market-data/mt5/Test-Mt5ProbeArtifact.ps1'
$source = [System.IO.File]::ReadAllText((Join-Path $repo 'tools/market-data/mt5/MoneyWayReadOnlyProbe.mq5'))
$temporaryArtifact = Join-Path ([System.IO.Path]::GetTempPath()) ('moneyway-probe-test-' + [guid]::NewGuid().ToString('N') + '.json')
$passed = 0
$failed = 0

function Native($Value) {
    $time = [datetime]::ParseExact($Value, 'yyyy.MM.dd HH:mm:ss', [cultureinfo]::InvariantCulture)
    return @{ raw = [long]($time - [datetime]'1970-01-01').TotalSeconds; display = $Value }
}

function Fixture {
    $frames = @()
    foreach ($pair in @(@('M1', 60), @('M5', 300), @('H1', 3600), @('H4', 14400))) {
        $latest = Native '2026.01.05 12:00:00'
        $olderTime = ([datetime]'2026-01-05T12:00:00').AddSeconds(-$pair[1]).ToString('yyyy.MM.dd HH:mm:ss')
        $bars = @()
        for ($i = 0; $i -lt 2; $i++) {
            $bars += @{
                series_position = $i; open_time_native = $(if ($i -eq 0) { $latest } else { Native $olderTime })
                open = 100.25; high = 102.5; low = 99.75; close = 101.25; ohlc_valid = $true
                role = $(if ($i -eq 0) { 'current_bar_completion_unverified' } else { 'historical_preceding_bar' })
                newer_than_next_seconds = $(if ($i -eq 0) { $pair[1] } else { $null })
                open_time_utc = $null; provider_confirmed_closed = $null; historical_available_at_utc = $null
            }
        }
        $frames += @{
            timeframe = $pair[0]; period_seconds = $pair[1]; requested_count = 2
            status = 'sample_returned'; copy_return = 2; copy_error_code = 0
            copy_started_elapsed_us = 10; copy_finished_elapsed_us = 20
            synchronized_before = @{ value = 1; error_code = 0 }; synchronized_after = @{ value = 1; error_code = 0 }
            last_bar_before = @{ value = $latest.raw; error_code = 0 }; last_bar_after = @{ value = $latest.raw; error_code = 0 }
            terminal_bar_count = @{ value = 2; error_code = 0 }
            terminal_first_date = @{ value = $bars[1].open_time_native.raw; error_code = 0 }
            server_first_date = @{ value = $bars[1].open_time_native.raw; error_code = 0 }
            head_consistent = $true; bars = $bars
        }
    }
    return @{
        schema = 'moneyway.mt5-read-only-probe'; version = 1; diagnostic_only = $true; canonical_ready = $false
        generated_at_utc = $null; generation_time_basis = 'TimeGMT_computer_derived_not_authenticated'
        generated_gmt_candidate = (Native '2026.01.05 12:00:00')
        utc_conversion_authenticated = $false; feed_equivalence_verified = $false
        source = @{ platform = 'MetaTrader 5'; server = 'OANDA_Global-Demo-1'; company = 'SYNTHETIC FIXTURE'
            demo_verified = $true; mode_error_code = 0; connected_before = $true; terminal_build = 0 }
        gate = 'passed'
        symbol = @{ native = 'US100'; query_attempted = $true; exists = $true; custom = $false; selected = $true
            error_code = 0; description = 'SYNTHETIC'; description_error_code = 0; profit_currency = 'USD'
            profit_currency_error_code = 0; digits = 2; digits_error_code = 0 }
        server_quote_before = $null; server_quote_after = $null; connected_after = $true
        limitations = @('synthetic_not_provider_evidence'); timeframes = $frames
    }
}

function Case([string]$Name, [scriptblock]$Change, [bool]$Reject = $false) {
    $data = Fixture
    & $Change $data
    [System.IO.File]::WriteAllText($temporaryArtifact, ($data | ConvertTo-Json -Depth 24), (New-Object System.Text.UTF8Encoding($false)))
    $rejected = $false
    try { $result = & $validator -Path $temporaryArtifact }
    catch { $rejected = $true }
    if ($rejected -ne $Reject -or (-not $rejected -and ($result.CanonicalReady -ne $false -or $result.ProviderConnectivityAuthenticatedByValidator -ne $false))) {
        $script:failed++
        Write-Output "FAIL: $Name"
    } else { $script:passed++; Write-Output "PASS: $Name" }
}

function SafeSource([string]$Text) {
    if ($Text -match '(?m)^\s*#(?!property\b)') { throw 'Unexpected preprocessor directive.' }
    $code = [regex]::Replace($Text, '//[^\r\n]*|"(?:\\.|[^"\\])*"', ' ')
    $allowed = @('BoolJson', 'Quote', 'NativeTime', 'NumberJson', 'ValidOhlc', 'SeriesProperty', 'ProbeTimeframe', 'ExportDiagnostic',
        'OnStart', 'for', 'if', 'StringLen', 'StringGetCharacter', 'StringFormat', 'ShortToString', 'TimeToString',
        'MathIsValidNumber', 'DoubleToString', 'ResetLastError', 'SeriesInfoInteger', 'GetLastError', 'PeriodSeconds',
        'ArraySetAsSeries', 'GetMicrosecondCount', 'CopyRates', 'TimeLocal', 'FileIsExist', 'Print', 'FileOpen',
        'FileSize', 'FileClose', 'FileWriteString', 'FileFlush', 'MQLInfoInteger', 'TerminalInfoInteger',
        'AccountInfoInteger', 'AccountInfoString', 'SymbolExist', 'SymbolSelect', 'SymbolInfoString', 'SymbolInfoInteger',
        'TimeGMT', 'TimeCurrent')
    foreach ($call in [regex]::Matches($code, '\b([A-Za-z_]\w*)\s*\(')) {
        if ($allowed -cnotcontains $call.Groups[1].Value) { throw 'Unapproved function call.' }
    }
    foreach ($field in [regex]::Matches($code, '\b(?:ACCOUNT|TERMINAL|SYMBOL|MQL)_[A-Z_]+\b')) {
        if (@('ACCOUNT_TRADE_MODE', 'ACCOUNT_TRADE_MODE_DEMO', 'ACCOUNT_SERVER', 'ACCOUNT_COMPANY', 'TERMINAL_CONNECTED',
            'TERMINAL_BUILD', 'SYMBOL_DESCRIPTION', 'SYMBOL_CURRENCY_PROFIT', 'SYMBOL_DIGITS', 'MQL_TESTER') -cnotcontains $field.Value) {
            throw 'Unapproved metadata query.'
        }
    }
}

try {
    Case 'Synthetic diagnostic preserves raw times and exact four timeframe identities' { param($d) }
    Case 'Partial history remains a partial sample' { param($d) $d.timeframes[0].requested_count = 3; $d.timeframes[0].status = 'partial_sample' }
    Case 'Unsynchronized returned data is diagnostic only' { param($d) $d.timeframes[0].synchronized_after.value = 0 }
    Case 'Failed history preserves error and has no candles' { param($d)
        $f = $d.timeframes[0]; $f.copy_return = -1; $f.copy_error_code = 4401; $f.bars = @(); $f.head_consistent = $false; $f.status = 'copy_failed'
        $f.synchronized_after.value = $null; $f.synchronized_after.error_code = 4401
    }
    Case 'Empty history stays empty' { param($d)
        $f = $d.timeframes[0]; $f.copy_return = 0; $f.bars = @(); $f.head_consistent = $false; $f.status = 'empty'
    }
    Case 'Disconnected terminal produces only unattempted frames' { param($d)
        $d.gate = 'terminal_disconnected'; $d.source.connected_before = $false; $d.symbol.query_attempted = $false
        foreach ($f in $d.timeframes) { $f.status = 'not_attempted'; $f.copy_return = $null; $f.copy_error_code = $null; $f.bars = @() }
    }
    Case 'Head race leaves all roles unverified' { param($d)
        $d.timeframes[0].head_consistent = $false
        foreach ($b in $d.timeframes[0].bars) { $b.role = 'unverified_head' }
    }
    Case 'Malformed OHLC retained only with invalid diagnostic flag' { param($d) $d.timeframes[0].bars[0].high = 1; $d.timeframes[0].bars[0].ohlc_valid = $false }
    Case 'Malformed OHLC cannot claim validity' { param($d) $d.timeframes[0].bars[0].high = 1 } $true
    Case 'Position zero cannot become historical/closed' { param($d) $d.timeframes[0].bars[0].role = 'historical_preceding_bar' } $true
    Case 'No silent UTC normalization' { param($d) $d.timeframes[0].bars[0].open_time_utc = '2026-01-05T12:00:00Z' } $true
    Case 'No provider close assertion from CopyRates' { param($d) $d.timeframes[0].bars[0].provider_confirmed_closed = $true } $true
    Case 'No synthetic historical availability' { param($d) $d.timeframes[0].bars[0].historical_available_at_utc = '2026-01-05T12:00:00Z' } $true
    Case 'Raw timestamp/display mismatch rejected' { param($d) $d.timeframes[0].bars[0].open_time_native.raw++ } $true
    Case 'Missing timeframe rejected' { param($d) $d.timeframes = @($d.timeframes[0]) } $true
    Case 'Unapproved timeframe rejected' { param($d) $d.timeframes[0].timeframe = 'M30' } $true
    Case 'USTEC alias rejected' { param($d) $d.symbol.native = 'USTEC' } $true
    Case 'Unapproved feed equivalence rejected' { param($d) $d.feed_equivalence_verified = $true } $true
    Case 'Server mismatch cannot pass acquisition gate' { param($d) $d.source.server = 'other' } $true
    Case 'Non-demo cannot pass acquisition gate' { param($d) $d.source.demo_verified = $false } $true
    Case 'Account identifier field rejected' { param($d) $d.source.account_login = 'synthetic-forbidden' } $true
    Case 'Unbounded request rejected' { param($d) $d.timeframes[0].requested_count = 129 } $true
    Case 'Diagnostic cannot claim canonical readiness' { param($d) $d.canonical_ready = $true } $true
    SafeSource $source
    $passed++; Write-Output 'PASS: Probe calls and metadata limited to explicit read-only allowlist'
    foreach ($unsafe in @('OrderSend(request, result);', 'AccountInfoInteger(ACCOUNT_LOGIN);', '#include <Trade/Trade.mqh>')) {
        $blocked = $false
        try { SafeSource ($source + "`n" + $unsafe) } catch { $blocked = $true }
        if ($blocked) { $passed++; Write-Output 'PASS: Unsafe source mutation rejected' } else { $failed++ }
    }
} finally {
    # Delete only this test's explicitly created single temporary JSON file.
    if (Test-Path -LiteralPath $temporaryArtifact) { Remove-Item -LiteralPath $temporaryArtifact }
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { throw 'MT5 probe checks failed.' }
