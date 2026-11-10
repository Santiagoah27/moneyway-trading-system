[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EarlierPath,
    [Parameter(Mandatory = $true)][string]$LaterPath,
    [switch]$CaptureOrderAttested,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$invariant = [cultureinfo]::InvariantCulture

# Exact decimal arithmetic on JSON literals, with arbitrary-size decimal exponents.
# No binary floating point, precision threshold, epsilon, or source-value rounding.
function NumberParts([string]$Literal) {
    $m = [regex]::Match($Literal, '^(?<sign>-?)(?<whole>0|[1-9][0-9]*)(?:\.(?<fraction>[0-9]+))?(?:[eE](?<exponent>[+-]?[0-9]+))?$')
    if (-not $m.Success) { throw 'Invalid OHLC numeric literal.' }
    $digits = ($m.Groups['whole'].Value + $m.Groups['fraction'].Value).TrimStart('0')
    if ($digits.Length -eq 0) { return @{ Sign = 1; Digits = '0'; Exponent = [bigint]0 } }
    $exponent = [bigint]0
    if ($m.Groups['exponent'].Success) { $exponent = [bigint]::Parse($m.Groups['exponent'].Value, $invariant) }
    $exponent -= $m.Groups['fraction'].Length
    $trimmed = $digits.TrimEnd('0')
    $exponent += $digits.Length - $trimmed.Length
    return @{ Sign = $(if ($m.Groups['sign'].Value -eq '-') { -1 } else { 1 }); Digits = $trimmed; Exponent = $exponent }
}

function CompareNumber([string]$Left, [string]$Right) {
    $a = NumberParts $Left
    $b = NumberParts $Right
    if ($a.Digits -ceq '0' -and $b.Digits -ceq '0') { return 0 }
    if ($a.Digits -ceq '0') { return -$b.Sign }
    if ($b.Digits -ceq '0') { return $a.Sign }
    if ($a.Sign -ne $b.Sign) { return $a.Sign }
    $magnitudeA = $a.Exponent + $a.Digits.Length
    $magnitudeB = $b.Exponent + $b.Digits.Length
    if ($magnitudeA -lt $magnitudeB) { return -$a.Sign }
    if ($magnitudeA -gt $magnitudeB) { return $a.Sign }
    $length = [math]::Max($a.Digits.Length, $b.Digits.Length)
    return $a.Sign * [math]::Sign([string]::CompareOrdinal($a.Digits.PadRight($length, '0'), $b.Digits.PadRight($length, '0')))
}

function SameOhlc($Left, $Right) {
    foreach ($field in @('open', 'high', 'low', 'close')) {
        if ((CompareNumber $Left.$field $Right.$field) -ne 0) { return $false }
    }
    return $true
}

function ReadSample([string]$Path, [string]$Label) {
    # Deny writes/deletes while the existing validator and exact-literal reader run.
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
    try {
        if ($stream.Length -gt 2097152) { throw "$Label input exceeds the existing 2 MiB diagnostic limit." }
        $reader = New-Object System.IO.StreamReader($stream, (New-Object System.Text.UTF8Encoding($false, $true)), $true, 4096, $true)
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $data = $text | ConvertFrom-Json
        try { $validation = & (Join-Path $PSScriptRoot 'Test-Mt5ProbeArtifact.ps1') -Path $Path }
        catch {
            # The existing validator rejects duplicate chronology; expose its actual identity cause.
            if ($_.Exception.Message -match 'Invalid native chronology/delta') {
                foreach ($tf in $data.timeframes) {
                    $duplicates = @($tf.bars | Group-Object { $_.open_time_native.raw } | Where-Object Count -gt 1)
                    if ($duplicates.Count -gt 0) { throw "$Label input: duplicate/conflicting native bar identity in $($tf.timeframe)." }
                }
            }
            throw "$Label input: $($_.Exception.Message)"
        }
        if ($validation.InvalidOhlcBars -ne 0) { throw "$Label input contains invalid OHLC; comparison refused." }
        # Tokenize strings atomically before quoting numeric tokens. Escaped string contents
        # cannot become properties/numbers. The existing validator owns schema validation.
        $literalText = [regex]::Replace($text, '"(?:\\.|[^"\\])*"|(?<number>-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)',
            [System.Text.RegularExpressions.MatchEvaluator]{ param($m)
                if ($m.Groups['number'].Success) { return '"' + $m.Value + '"' }
                return $m.Value
            })
        $literalData = $literalText | ConvertFrom-Json
        foreach ($tf in $literalData.timeframes) {
            foreach ($bar in $tf.bars) {
                if ((CompareNumber $bar.high $bar.open) -lt 0 -or (CompareNumber $bar.high $bar.close) -lt 0 -or
                    (CompareNumber $bar.high $bar.low) -lt 0 -or (CompareNumber $bar.low $bar.open) -gt 0 -or
                    (CompareNumber $bar.low $bar.close) -gt 0) { throw "$Label input: exact OHLC inequality violated in $($tf.timeframe)." }
            }
        }
        return @{ Data = $data; Literals = $literalData; Hash = $validation.SHA256 }
    } finally { $stream.Dispose() }
}

function NativeStamp($Value) {
    if ($null -eq $Value) { return $null }
    return [pscustomobject][ordered]@{ raw = $Value.raw; display = $Value.display }
}

function InputMetadata($Sample) {
    $d = $Sample.Data
    return [pscustomobject][ordered]@{
        sha256 = $Sample.Hash
        terminal_build = $d.source.terminal_build
        gate = $d.gate
        connected_before = $d.source.connected_before
        connected_after = $d.connected_after
        generation_time_basis = $d.generation_time_basis
        generated_gmt_candidate = (NativeStamp $d.generated_gmt_candidate)
        server_quote_before = (NativeStamp $d.server_quote_before)
        server_quote_after = (NativeStamp $d.server_quote_after)
    }
}

function BarView($Bar) {
    return [pscustomobject][ordered]@{
        open_time_native = [pscustomobject][ordered]@{ raw = [long]$Bar.open_time_native.raw; display = $Bar.open_time_native.display }
        series_position = [int]$Bar.series_position
        role = $Bar.role
        # Strings deliberately preserve the exact source JSON number tokens.
        ohlc_literals = [pscustomobject][ordered]@{ open = $Bar.open; high = $Bar.high; low = $Bar.low; close = $Bar.close }
    }
}

function SampleQuality($Frame) {
    $result = [ordered]@{ status = $Frame.status; requested_count = $Frame.requested_count
        copy_return = $Frame.copy_return; copy_error_code = $Frame.copy_error_code }
    if ($Frame.status -cne 'not_attempted') {
        foreach ($field in @('synchronized_before', 'synchronized_after', 'head_consistent', 'last_bar_before', 'last_bar_after',
            'copy_started_elapsed_us', 'copy_finished_elapsed_us', 'terminal_bar_count', 'terminal_first_date', 'server_first_date')) {
            $result[$field] = $Frame.$field
        }
    }
    return [pscustomobject]$result
}

function Discontinuities($Frame) {
    $items = @()
    foreach ($bar in $Frame.bars) {
        if ($null -ne $bar.newer_than_next_seconds -and [long]$bar.newer_than_next_seconds -ne [long]$Frame.period_seconds) {
            $items += [pscustomobject][ordered]@{ open_time_native = (NativeStamp $bar.open_time_native)
                observed_delta_seconds = $bar.newer_than_next_seconds; declared_period_seconds = $Frame.period_seconds
                classification = 'native_interval_difference_cause_unresolved' }
        }
    }
    return $items
}

$earlier = ReadSample $EarlierPath 'Earlier'
$later = ReadSample $LaterPath 'Later'
foreach ($field in @('platform', 'server', 'company', 'demo_verified')) {
    if ($earlier.Data.source.$field -cne $later.Data.source.$field) { throw "Incompatible source identity: $field differs." }
}
foreach ($field in @('native', 'custom', 'profit_currency')) {
    if ($earlier.Data.symbol.$field -cne $later.Data.symbol.$field) { throw "Incompatible symbol identity: $field differs." }
}
$frames = @()
foreach ($name in @('M1', 'M5', 'H1', 'H4')) {
    $a = @($earlier.Data.timeframes | Where-Object timeframe -CEQ $name)[0]
    $b = @($later.Data.timeframes | Where-Object timeframe -CEQ $name)[0]
    if ($a.period_seconds -ne $b.period_seconds) { throw "Incompatible timeframe duration: $name." }
    $la = @($earlier.Literals.timeframes | Where-Object timeframe -CEQ $name)[0]
    $lb = @($later.Literals.timeframes | Where-Object timeframe -CEQ $name)[0]
    $comparable = @($a.bars).Count -gt 0 -and @($b.bars).Count -gt 0
    $overlap = @(); $new = @(); $absent = @(); $transitions = @(); $limitations = @()
    $unchanged = 0; $changed = 0
    $indexA = @{}; $indexB = @{}
    foreach ($bar in $la.bars) { $indexA[[string]$bar.open_time_native.raw] = $bar }
    foreach ($bar in $lb.bars) { $indexB[[string]$bar.open_time_native.raw] = $bar }
    if ($comparable) {
        foreach ($bar in $la.bars) {
            $key = [string]$bar.open_time_native.raw
            if (-not $indexB.ContainsKey($key)) { $absent += BarView $bar; continue }
            $next = $indexB[$key]
            $same = SameOhlc $bar $next
            $kind = 'unchanged_ohlc'
            if ($same) { $unchanged++ } else {
                $changed++
                $kind = if ($bar.role -ceq 'historical_preceding_bar' -and $next.role -ceq 'historical_preceding_bar') {
                    'historical_value_discrepancy_requires_investigation'
                } elseif ($bar.role -ceq 'current_bar_completion_unverified' -and $next.role -ceq 'current_bar_completion_unverified') {
                    'current_bar_change_may_be_forming_evolution'
                } elseif ($bar.role -ceq 'current_bar_completion_unverified' -and $next.role -ceq 'historical_preceding_bar') {
                    'current_to_historical_changed_completion_unverified'
                } else { 'unverified_or_changed_role_discrepancy' }
            }
            $overlap += [pscustomobject][ordered]@{ classification = $kind; earlier = (BarView $bar); later = (BarView $next) }
            if ($bar.role -cne $next.role) {
                $transitions += [pscustomobject][ordered]@{ raw_open_time = [long]$key; earlier_role = $bar.role; later_role = $next.role }
            }
        }
        foreach ($bar in $lb.bars) {
            if (-not $indexA.ContainsKey([string]$bar.open_time_native.raw)) { $new += BarView $bar }
        }
        if ($overlap.Count -eq 0) { $limitations += 'no_overlap_cannot_assess_value_changes' }
        if ([long]$lb.bars[0].open_time_native.raw -lt [long]$la.bars[0].open_time_native.raw) {
            $limitations += 'native_head_regressed_capture_order_or_history_requires_review'
        }
    } else { $limitations += 'empty_failed_or_unattempted_sample_counts_are_not_change_evidence' }
    foreach ($pair in @(@('earlier', $a), @('later', $b))) {
        $f = $pair[1]
        if ($f.status -cne 'sample_returned') { $limitations += "$($pair[0])_$($f.status)" }
        if ($f.status -cne 'not_attempted' -and ($f.synchronized_after.value -ne 1 -or -not $f.head_consistent -or $f.copy_error_code -ne 0)) {
            $limitations += "$($pair[0])_synchronization_head_or_error_requires_review"
        }
    }
    $frames += [pscustomobject][ordered]@{
        timeframe = $name; period_seconds = $a.period_seconds; comparable = $comparable
        earlier_sample = (SampleQuality $a); later_sample = (SampleQuality $b)
        unchanged_count = $(if ($comparable) { $unchanged } else { $null })
        changed_count = $(if ($comparable) { $changed } else { $null })
        newly_present_count = $(if ($comparable) { $new.Count } else { $null })
        absent_from_later_sample_count = $(if ($comparable) { $absent.Count } else { $null })
        overlapping = $overlap; newly_present = $new; absent_from_later_sample = $absent; role_transitions = $transitions
        earlier_native_discontinuities = @(Discontinuities $a); later_native_discontinuities = @(Discontinuities $b)
        limitations = $limitations
    }
}
$report = [pscustomobject][ordered]@{
    schema = 'moneyway.mt5-offline-comparison'; version = 1; diagnostic_only = $true; canonical_ready = $false
    capture_order = $(if ($CaptureOrderAttested) { 'caller_attested_not_independently_verified' } else { 'unverified_caller_labels_only' })
    utc_conversion_authenticated = $false; provider_confirmed_closed = $null; historical_available_at_utc = $null
    source = [pscustomobject][ordered]@{ platform = $earlier.Data.source.platform; company = $earlier.Data.source.company
        server = $earlier.Data.source.server; demo = $earlier.Data.source.demo_verified; native_symbol = $earlier.Data.symbol.native }
    earlier = (InputMetadata $earlier); later = (InputMetadata $later); timeframes = $frames
    limitations = @('capture_order_not_authenticated_by_clocks', 'no_authenticated_utc_conversion', 'no_provider_close_or_historical_availability',
        'absence_is_not_provider_deletion', 'newly_present_is_not_provider_first_creation', 'no_new_bars_is_not_disconnection',
        'no_staleness_latency_session_or_correction_policy_inferred', 'no_canonical_ingestion_or_strategy_evaluation')
}
if ($ReportPath) {
    # CreateNew is atomic: an existing output (including either input) cannot be overwritten.
    $output = [System.IO.File]::Open($ReportPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
    try {
        $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes(($report | ConvertTo-Json -Depth 24) + "`n")
        $output.Write($bytes, 0, $bytes.Length)
    } finally { $output.Dispose() }
}
Write-Information "MT5 diagnostic comparison; order=$($report.capture_order); canonical_ready=false" -InformationAction Continue
Write-Information "Earlier SHA256: $($report.earlier.sha256)" -InformationAction Continue
Write-Information "Later SHA256: $($report.later.sha256)" -InformationAction Continue
foreach ($frame in $frames) {
    Write-Information "$($frame.timeframe): comparable=$($frame.comparable) unchanged=$($frame.unchanged_count) changed=$($frame.changed_count) newly_present=$($frame.newly_present_count) absent_from_sample=$($frame.absent_from_later_sample_count)" -InformationAction Continue
}
$report
