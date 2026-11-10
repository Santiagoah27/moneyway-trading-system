param([Parameter(Mandatory = $true)][string]$Path)

# Diagnostic validation only. Does not import candles or authenticate a provider.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Invalid MT5 probe diagnostic: $Message" }
}

function ExactKeys($Value, [string[]]$Allowed) {
    foreach ($key in $Value.PSObject.Properties.Name) {
        Require ($Allowed -ccontains $key) "Unexpected field: $key"
    }
}

function NativeTime($Value) {
    Require ($null -ne $Value) 'Missing native opening timestamp.'
    ExactKeys $Value @('raw', 'display')
    Require (($Value.raw -is [long] -or $Value.raw -is [int]) -and $Value.raw -gt 0) 'Invalid raw native timestamp.'
    # Compare representations in an unspecified coordinate; never convert to UTC.
    $native = [datetime]::ParseExact($Value.display, 'yyyy.MM.dd HH:mm:ss', [cultureinfo]::InvariantCulture)
    $epoch = [datetime]::SpecifyKind([datetime]'1970-01-01', [DateTimeKind]::Unspecified)
    Require (($native - $epoch).TotalSeconds -eq $Value.raw) 'Native integer/display mismatch.'
}

$file = Get-Item -LiteralPath $Path
Require (-not $file.PSIsContainer -and $file.Length -le 2097152) 'Expected a JSON file of at most 2 MiB.'
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
$artifact = [System.IO.File]::ReadAllText($file.FullName, $utf8) | ConvertFrom-Json
ExactKeys $artifact @('schema', 'version', 'diagnostic_only', 'canonical_ready', 'generated_at_utc',
    'generation_time_basis', 'generated_gmt_candidate', 'utc_conversion_authenticated', 'feed_equivalence_verified',
    'source', 'gate', 'symbol', 'server_quote_before', 'timeframes', 'server_quote_after', 'connected_after', 'limitations')
Require ($artifact.schema -ceq 'moneyway.mt5-read-only-probe' -and $artifact.version -eq 1) 'Unsupported schema/version.'
Require ($artifact.diagnostic_only -ceq $true -and $artifact.canonical_ready -ceq $false) 'Diagnostic-only boundary violated.'
Require ($artifact.utc_conversion_authenticated -ceq $false -and $artifact.feed_equivalence_verified -ceq $false) 'Unapproved source mapping.'
Require ($null -eq $artifact.generated_at_utc -and $artifact.generation_time_basis -ceq 'TimeGMT_computer_derived_not_authenticated') 'Unverified generation time labeled UTC.'
ExactKeys $artifact.source @('platform', 'server', 'company', 'demo_verified', 'mode_error_code', 'connected_before', 'terminal_build')
ExactKeys $artifact.symbol @('native', 'query_attempted', 'exists', 'custom', 'selected', 'error_code', 'description',
    'description_error_code', 'profit_currency', 'profit_currency_error_code', 'digits', 'digits_error_code')
Require ($artifact.source.platform -ceq 'MetaTrader 5' -and $artifact.symbol.native -ceq 'US100') 'Unexpected platform/native symbol.'
$allowedGates = @('passed', 'tester_rejected', 'terminal_disconnected', 'demo_unverified', 'server_mismatch', 'sample_bound_rejected')
Require ($allowedGates -ccontains $artifact.gate) 'Unknown source gate.'
if ($artifact.gate -ceq 'passed') {
    Require ($artifact.source.server -ceq 'OANDA_Global-Demo-1' -and $artifact.source.demo_verified -ceq $true -and
        $artifact.source.connected_before -ceq $true) 'Acquisition without verified demo/server gate.'
}
$periods = @{ M1 = 60; M5 = 300; H1 = 3600; H4 = 14400 }
Require (@($artifact.timeframes).Count -eq 4) 'Expected exactly M1/M5/H1/H4.'
$seen = @()
$barCount = 0
$invalidOhlcCount = 0
foreach ($tf in $artifact.timeframes) {
    ExactKeys $tf @('timeframe', 'period_seconds', 'requested_count', 'status', 'copy_return', 'copy_error_code', 'bars',
        'copy_started_elapsed_us', 'copy_finished_elapsed_us', 'synchronized_before', 'synchronized_after',
        'last_bar_before', 'last_bar_after', 'terminal_bar_count', 'terminal_first_date', 'server_first_date', 'head_consistent')
    Require (($periods.Keys -ccontains $tf.timeframe) -and $seen -cnotcontains $tf.timeframe) 'Unexpected/duplicate timeframe.'
    $seen += $tf.timeframe
    Require ($tf.period_seconds -eq $periods[$tf.timeframe]) 'Incorrect timeframe duration.'
    Require (@($tf.bars).Count -le 128) 'Unbounded sample.'
    if ($tf.status -ceq 'not_attempted') {
        Require ($null -eq $tf.copy_return -and $null -eq $tf.copy_error_code -and @($tf.bars).Count -eq 0) 'Fabricated data for unattempted acquisition.'
        continue
    }
    Require ($artifact.gate -ceq 'passed' -and $artifact.symbol.exists -ceq $true -and
        $artifact.symbol.custom -ceq $false -and $artifact.symbol.selected -ceq $true) 'Acquisition outside source/symbol gate.'
    Require ($tf.requested_count -ge 2 -and $tf.requested_count -le 128) 'Invalid sample bound.'
    $count = @($tf.bars).Count
    $expected = if ($tf.copy_return -lt 0) { 'copy_failed' } elseif ($tf.copy_return -eq 0) { 'empty' }
        elseif ($tf.copy_return -lt $tf.requested_count) { 'partial_sample' } else { 'sample_returned' }
    Require ($tf.status -ceq $expected -and $count -eq [math]::Max(0, $tf.copy_return) -and
        $tf.copy_return -le $tf.requested_count) 'Acquisition status/count mismatch.'
    foreach ($sync in @($tf.synchronized_before, $tf.synchronized_after)) {
        ExactKeys $sync @('value', 'error_code')
        Require ($null -eq $sync.value -or $sync.value -eq 0 -or $sync.value -eq 1) 'Invalid synchronization value.'
        Require ($sync.error_code -ge 0) 'Missing synchronization error.'
    }
    Require ($tf.copy_error_code -ge 0) 'Missing CopyRates error.'
    Require ($tf.copy_finished_elapsed_us -ge $tf.copy_started_elapsed_us) 'Invalid local acquisition bracket.'
    for ($i = 0; $i -lt $count; $i++) {
        $bar = $tf.bars[$i]
        ExactKeys $bar @('series_position', 'open_time_native', 'open', 'high', 'low', 'close', 'ohlc_valid',
            'role', 'newer_than_next_seconds', 'open_time_utc', 'provider_confirmed_closed', 'historical_available_at_utc')
        Require ($bar.series_position -eq $i) 'Invalid series position.'
        NativeTime $bar.open_time_native
        Require ($null -eq $bar.open_time_utc -and $null -eq $bar.provider_confirmed_closed -and
            $null -eq $bar.historical_available_at_utc) 'Fabricated UTC/closed/availability assertion.'
        $numbers = @($bar.open, $bar.high, $bar.low, $bar.close)
        $numeric = @($numbers | Where-Object { $_ -is [double] -or $_ -is [decimal] -or $_ -is [long] -or $_ -is [int] }).Count -eq 4
        $valid = $numeric -and $bar.high -ge $bar.open -and $bar.high -ge $bar.close -and
            $bar.high -ge $bar.low -and $bar.low -le $bar.open -and $bar.low -le $bar.close
        Require ($bar.ohlc_valid -ceq $valid) 'OHLC validation flag disagrees with prices.'
        if (-not $valid) { $invalidOhlcCount++ }
        $role = 'unverified_head'
        if ($tf.head_consistent -ceq $true) {
            Require ($tf.last_bar_before.value -gt 0 -and $tf.last_bar_before.value -eq $tf.last_bar_after.value -and
                $tf.bars[0].open_time_native.raw -eq $tf.last_bar_after.value) 'Unstable head labeled consistent.'
            if ($i -eq 0) { $role = 'current_bar_completion_unverified' }
            elseif ($bar.open_time_native.raw -lt $tf.last_bar_after.value) { $role = 'historical_preceding_bar' }
        }
        Require ($bar.role -ceq $role) 'Current/historical classification mismatch.'
        if ($i + 1 -lt $count) {
            $delta = $bar.open_time_native.raw - $tf.bars[$i + 1].open_time_native.raw
            Require ($delta -gt 0 -and $bar.newer_than_next_seconds -eq $delta) 'Invalid native chronology/delta.'
        } else { Require ($null -eq $bar.newer_than_next_seconds) 'Unexpected final delta.' }
    }
    $barCount += $count
}
[pscustomobject]@{
    ContractValid = $true
    DiagnosticOnly = $true
    CanonicalReady = $false
    Bars = $barCount
    InvalidOhlcBars = $invalidOhlcCount
    ProviderConnectivityAuthenticatedByValidator = $false
    SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
