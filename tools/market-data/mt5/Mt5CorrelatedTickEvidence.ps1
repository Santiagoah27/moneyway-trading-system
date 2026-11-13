Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Mt5NativeUtcEvidence.ps1')

function New-Mt5TickNonce {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}

function Get-Mt5TickHash([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Assert-Mt5TickLocalPath([string]$Path) {
    if ($Path -notmatch '^[A-Za-z]:[\\/]' -or $Path.Substring(2).Contains(':')) { throw 'absolute_local_path_required' }
    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    while ($null -ne $item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'reparse_path_rejected' }
        $item = if ($item -is [IO.DirectoryInfo]) { $item.Parent } else { $item.Directory }
    }
}

function Read-Mt5TickFile([string]$Path, [switch]$Json, [switch]$Raw) {
    Assert-Mt5TickLocalPath $Path
    $stream = New-Object IO.FileStream($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ((-not $Raw -and $stream.Length -lt 1) -or $stream.Length -gt 32768) { throw 'invalid_diagnostic_file_size' }
        $bytes = New-Object byte[] ([int]$stream.Length)
        $read = 0
        while ($read -lt $bytes.Length) {
            $count = $stream.Read($bytes, $read, $bytes.Length - $read)
            if ($count -eq 0) { throw 'incomplete_diagnostic_file' }
            $read += $count
        }
        $text = if ($Raw) { $null } else { (New-Object Text.UTF8Encoding($false, $true)).GetString($bytes) }
        $data = if ($Json) { $text | ConvertFrom-Json -ErrorAction Stop } else { $null }
        [pscustomobject]@{ file_name = [IO.Path]::GetFileName($Path); sha256 = Get-Mt5TickHash $bytes; text = $text; data = $data }
    } finally { $stream.Dispose() }
}

function Publish-Mt5TickFile([string]$Path, [string]$Text) {
    $pending = $Path + '.pending'
    if ((Test-Path -LiteralPath $Path) -or (Test-Path -LiteralPath $pending)) { throw 'diagnostic_path_already_exists' }
    Assert-Mt5TickLocalPath ([IO.Path]::GetDirectoryName($Path))
    Write-Mt5EvidenceBytes $pending ((New-Object Text.UTF8Encoding($false)).GetBytes($Text))
    [IO.File]::Move($pending, $Path)
}

function Assert-Mt5TickIdentity($Source) {
    if ($Source.company -cne 'OANDA Global Markets Limited' -or $Source.server -cne 'OANDA_Global-Demo-1' -or
        $Source.symbol -cne 'US100' -or $Source.demo -cne $true -or $Source.algo_trading_enabled -cne $false) {
        throw 'invalid_source_identity_or_execution_enabled'
    }
    if ($Source.connected -cne $true) { throw 'disconnected_terminal' }
}

function Assert-Mt5TickReady($Ready, [string]$Nonce, [string]$OfferHash) {
    if ($Ready.schema -cne 'moneyway.mt5-correlated-tick-ready' -or $Ready.version -ne 1 -or
        $Ready.nonce -cne $Nonce -or $Ready.offer_sha256 -cne $OfferHash -or
        $Ready.ready_token -cnotmatch '^\d+-\d+-\d+$') { throw 'invalid_ready_or_nonce' }
    Assert-Mt5TickIdentity $Ready.source
}

function Assert-Mt5TickResponse($Response, [string]$Nonce, [string]$ReadyToken, [string]$RequestHash) {
    if ($Response.schema -cne 'moneyway.mt5-correlated-tick-response' -or $Response.version -ne 1 -or
        $Response.nonce -cne $Nonce -or $Response.ready_token -cne $ReadyToken -or
        $Response.request_sha256 -cne $RequestHash -or $Response.canonical_utc_conversion_authorized -cne $false) {
        throw 'invalid_response_correlation_or_authorization'
    }
    Assert-Mt5TickIdentity $Response.source
    if ($Response.outcome -ceq 'data_unavailable') {
        if ($Response.event -cne 'none' -or $null -ne $Response.tick -or [string]::IsNullOrWhiteSpace($Response.reason)) {
            throw 'fabricated_tick_on_unavailable_response'
        }
        return
    }
    if ($Response.outcome -cne 'captured' -or $Response.event -cne 'OnTick' -or
        $Response.reason -cne 'subsequent_ontick_timestamp_progressed' -or $Response.tick_error -ne 0 -or
        $Response.context_sha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'invalid_tick_event' }
    foreach ($value in @($Response.started_ms, $Response.armed_ms, $Response.handler_ms, $Response.finished_ms,
        $Response.callback_count, $Response.armed_callback_count, $Response.tick.time_msc, $Response.baseline.time_msc)) {
        if (($value -isnot [long] -and $value -isnot [int]) -or $value -lt 0) { throw 'invalid_event_counter' }
    }
    if ($Response.started_ms -gt $Response.armed_ms -or $Response.armed_ms -gt $Response.handler_ms -or
        $Response.handler_ms -gt $Response.finished_ms -or $Response.callback_count -le $Response.armed_callback_count -or
        $Response.tick.time_msc -le $Response.baseline.time_msc -or $Response.baseline.time_msc -le 0) {
        throw 'cached_tick_or_invalid_event_order'
    }
    foreach ($tick in @($Response.tick, $Response.baseline)) {
        $native = Get-Mt5NativeCoordinate $tick.time
        if ($null -eq $native -or [decimal]::Floor([decimal]$tick.time_msc / 1000) -ne $native.raw) { throw 'tick_timestamp_mismatch' }
        foreach ($name in @('bid', 'ask', 'last', 'volume_real')) {
            if ($tick.$name -isnot [string] -or $tick.$name -cnotmatch '^[+-]?\d+\.\d+(?:[eE][+-]?\d+)?$') { throw 'invalid_tick_decimal' }
            $number = [double]::Parse($tick.$name, [Globalization.NumberStyles]::Float, [cultureinfo]::InvariantCulture)
            if ([double]::IsNaN($number) -or [double]::IsInfinity($number)) { throw 'invalid_tick_decimal' }
        }
        if ($tick.volume -isnot [string] -or $tick.volume -cnotmatch '^\d+$' -or
            ($tick.flags -isnot [int] -and $tick.flags -isnot [long]) -or $tick.flags -lt 0) { throw 'invalid_tick_volume_or_flags' }
        [uint64]::Parse($tick.volume, [cultureinfo]::InvariantCulture) | Out-Null
    }
}

function Get-Mt5TickAssessment($Response, $Context, $Bounds, $BeforeReference, $AfterReference) {
    $captured = $null -ne $Response -and $Response.outcome -ceq 'captured'
    $hasBounds = $null -ne $Bounds.request_publish_before -and $null -ne $Bounds.receipt_after
    $hostBefore = if ($hasBounds) { [datetimeoffset]::Parse($Bounds.request_publish_before.utc_candidate) } else { $null }
    $hostAfter = if ($hasBounds) { [datetimeoffset]::Parse($Bounds.receipt_after.utc_candidate) } else { $null }
    $ordered = $hasBounds -and $Bounds.request_publish_before.monotonic -le $Bounds.receipt_after.monotonic -and $hostBefore -le $hostAfter
    $referencesPresent = $BeforeReference.status -ceq 'context_specific' -and $AfterReference.status -ceq 'context_specific'
    $offset = $null
    $hypotheses = @()
    if ($captured) {
        $gmt = Get-Mt5NativeCoordinate $Context.time_gmt
        if ($null -ne $gmt) { $offset = [decimal]$Response.tick.time_msc / 1000 - $gmt.raw }
        if ($hasBounds) {
            $epoch = [datetimeoffset]'1970-01-01T00:00:00Z'
            $lower = [decimal]($hostBefore - $epoch).Ticks / [timespan]::TicksPerMillisecond
            $upper = [decimal]($hostAfter - $epoch).Ticks / [timespan]::TicksPerMillisecond
            foreach ($seconds in @(7200, 10800)) {
                $coordinate = [decimal]$Response.tick.time_msc - $seconds * 1000
                $hypotheses += [pscustomobject]@{ policy_offset_seconds = $seconds; assumed_utc_coordinate_milliseconds = $coordinate
                    within_uncorrected_host_observation_interval = $ordered -and $coordinate -ge $lower -and $coordinate -le $upper }
            }
        }
    }
    [pscustomobject][ordered]@{
        classification = $(if ($captured -and $ordered -and $referencesPresent) { 'context_specific' } else { 'data_unavailable' })
        nonce_correlated = $null -ne $Response; source_identity_contract_valid = $null -ne $Response
        fresh_tick_evidence = $(if ($captured) { 'context_specific_subsequent_OnTick_with_native_timestamp_progression' } else { 'data_unavailable' })
        network_quote_generated_after_request = 'unresolved'; queued_event_to_latest_lookup_identity = 'unresolved'
        handler_observation_bounded = $captured -and $ordered; source_event_UTC_bounded = $false
        external_reference_before_and_after_present = $referencesPresent; reference_authentication = 'unverified'; host_synchronization = 'unverified'
        exact_native_tick_minus_computer_gmt_seconds = $offset
        uncorrected_host_interval_milliseconds = $(if ($hasBounds) { [decimal]($hostAfter - $hostBefore).Ticks / [timespan]::TicksPerMillisecond } else { $null })
        policy_hypotheses = @($hypotheses); policy_classification = 'candidate'
        policy_source = 'https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm'
        offset_correction_applied = $false; historical_mapping = 'unresolved'; provider_dst_transitions = 'unresolved'
        unresolved = @('reference_authentication', 'network_quote_generation_time', 'queued_event_to_latest_lookup_identity',
            'provider_historical_DST_selection', 'ambiguous_native_times', 'provider_candle_closure', 'historical_availability', 'atomic_multi_timeframe_acquisition', 'canonical_integration')
        canonical_utc_conversion_authorized = $false; canonical_ready = $false; utc_conversion_authenticated = $false
        strategy_timezone = 'America/Bogota'
    }
}

function Get-Mt5TickReference([string]$ReferenceHost, [int]$TimeoutMilliseconds) {
    $query = Invoke-Mt5BoundedTimeQuery "/stripchart /computer:$ReferenceHost /samples:1 /period:1 /rdtsc /dataonly" $TimeoutMilliseconds
    [pscustomobject]@{ query = $query; parsed = Get-Mt5StripchartEvidence $query.raw $query.exit_code $query.timed_out $ReferenceHost }
}

function Invoke-Mt5TickSession {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][ValidateRange(1, 900)][int]$SessionSeconds,
        [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$')][string]$ReferenceHost,
        [Parameter(Mandatory = $true)][ValidateRange(1000, 30000)][int]$QueryTimeoutMilliseconds
    )
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')).TrimEnd('\') + '\'
    if ([IO.Path]::GetFullPath($root).StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) { throw 'diagnostic_output_must_be_external' }
    $parent = [IO.Path]::GetDirectoryName($root)
    Assert-Mt5TickLocalPath $parent
    [IO.Directory]::CreateDirectory($root) | Out-Null
    Assert-Mt5TickLocalPath $root
    $nonce = New-Mt5TickNonce
    $directory = Join-Path $root $nonce
    if (Test-Path -LiteralPath $directory) { throw 'duplicate_nonce_directory' }
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $watch = [Diagnostics.Stopwatch]::StartNew()
    function Boundary {
        [pscustomobject]@{ utc_candidate = [datetimeoffset]::UtcNow.ToString('o'); monotonic = [Diagnostics.Stopwatch]::GetTimestamp() }
    }
    function WaitForFile([string]$Name) {
        while ($watch.Elapsed.TotalSeconds -lt $SessionSeconds) {
            if (Test-Path -LiteralPath (Join-Path $directory 'cancel.txt')) { return $false }
            if (Test-Path -LiteralPath (Join-Path $directory $Name)) { return $true }
            if ($Name -ceq 'ready.json' -and (Test-Path -LiteralPath (Join-Path $directory 'response.json'))) { return $false }
            Start-Sleep -Milliseconds 100
        }
        return $false
    }

    $files = @()
    $ready = $null; $response = $null; $context = $null
    $request = $null; $failure = $null; $outcome = 'data_unavailable'
    $created = Boundary
    $offerAfter = $null; $requestBefore = $null; $requestAfter = $null; $receiptBefore = $null; $receiptAfter = $null
    $before = $null; $after = $null; $source = $null; $status = $null
    try {
        Publish-Mt5TickFile (Join-Path $directory 'offer.txt') "moneyway.mt5-correlated-tick-offer/v1`n$nonce`n$SessionSeconds`nUS100`n"
        $offerAfter = Boundary
        $files += Read-Mt5TickFile (Join-Path $directory 'offer.txt')
        Write-Host "SessionNonce = $nonce"
        Write-Host "Session directory = $directory"
        Write-Host "Attach MoneyWayCorrelatedTick to a separate US100 chart with this nonce. Keep Algo Trading disabled."
        if (WaitForFile 'ready.json') {
            $ready = Read-Mt5TickFile (Join-Path $directory 'ready.json') -Json
            $files += $ready
            Assert-Mt5TickReady $ready.data $nonce $files[0].sha256
            $source = Invoke-Mt5BoundedTimeQuery '/query /source' $QueryTimeoutMilliseconds
            $status = Invoke-Mt5BoundedTimeQuery '/query /status /verbose' $QueryTimeoutMilliseconds
            $before = Get-Mt5TickReference $ReferenceHost $QueryTimeoutMilliseconds
            $remaining = [long][math]::Floor($SessionSeconds * 1000 - $watch.Elapsed.TotalMilliseconds)
            if ($remaining -le 0 -or (Test-Path -LiteralPath (Join-Path $directory 'cancel.txt'))) { $failure = 'session_expired_or_cancelled_before_request' }
            else {
                $requestBefore = Boundary
                Publish-Mt5TickFile (Join-Path $directory 'request.txt') "moneyway.mt5-correlated-tick-request/v1`n$nonce`n$($ready.data.ready_token)`n$remaining`nUS100`n"
                $requestAfter = Boundary
                $request = Read-Mt5TickFile (Join-Path $directory 'request.txt')
                $files += $request
                if (WaitForFile 'response.json') {
                    $receiptBefore = Boundary
                    $responseFile = Read-Mt5TickFile (Join-Path $directory 'response.json') -Json
                    $receiptAfter = Boundary
                    $files += $responseFile
                    Assert-Mt5TickResponse $responseFile.data $nonce $ready.data.ready_token $request.sha256
                    $response = $responseFile.data
                    if ($response.outcome -ceq 'captured') {
                        $contextFile = Read-Mt5TickFile (Join-Path $directory 'context.json') -Json
                        if ($contextFile.sha256 -cne $response.context_sha256) { throw 'context_hash_mismatch' }
                        $files += $contextFile; $context = $contextFile.data
                        foreach ($name in @('time_current', 'time_trade_server', 'time_gmt', 'time_local', 'm1_head')) {
                            Get-Mt5NativeCoordinate $context.$name | Out-Null
                        }
                    }
                    $outcome = $response.outcome
                    $failure = $response.reason
                } else {
                    $failure = if (Test-Path -LiteralPath (Join-Path $directory 'cancel.txt')) { 'cancelled' } else { 'response_timeout' }
                }
            }
        } else {
            if (Test-Path -LiteralPath (Join-Path $directory 'response.json')) {
                $files += Read-Mt5TickFile (Join-Path $directory 'response.json') -Json
                $failure = 'ea_response_before_readiness'
            } else {
                $failure = if (Test-Path -LiteralPath (Join-Path $directory 'cancel.txt')) { 'cancelled' } else { 'ea_unavailable_timeout' }
            }
        }
    } catch {
        $response = $null; $context = $null; $outcome = 'data_unavailable'
        $failure = 'invalid_or_unreadable_diagnostic_evidence'
        Write-Warning ($_.Exception.Message)
    } finally {
        # Cancellation is scoped to this nonce. Preserve every prior file.
        if (-not (Test-Path -LiteralPath (Join-Path $directory 'cancel.txt'))) {
            Publish-Mt5TickFile (Join-Path $directory 'cancel.txt') "$nonce`n"
        }
    }

    $sessionEnded = Boundary
    $after = Get-Mt5TickReference $ReferenceHost $QueryTimeoutMilliseconds
    if ($null -eq $before) {
        $before = [pscustomobject]@{ query = $null; parsed = Get-Mt5StripchartEvidence '' -1 $false $ReferenceHost }
    }
    $bounds = [pscustomobject]@{ offer_publish_before = $created; offer_publish_after = $offerAfter; session_ended = $sessionEnded
        request_publish_before = $requestBefore; request_publish_after = $requestAfter
        receipt_before = $receiptBefore; receipt_after = $receiptAfter; monotonic_frequency = [Diagnostics.Stopwatch]::Frequency }
    $integrity = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -ne 'session.lock' })) {
        try {
            $retained = Read-Mt5TickFile $file.FullName -Raw
            $known = @($files | Where-Object file_name -CEQ $file.Name)
            if ($known.Count -gt 1 -or ($known.Count -eq 1 -and $known[0].sha256 -cne $retained.sha256)) { throw 'diagnostic_file_changed_or_duplicate' }
            $integrity += $retained | Select-Object file_name,sha256
        } catch {
            $failure = 'diagnostic_integrity_failed'; $outcome = 'data_unavailable'; $response = $null; $context = $null
            $integrity += [pscustomobject]@{ file_name = $file.Name; sha256 = $null; status = 'unverified' }
        }
    }
    $assessment = Get-Mt5TickAssessment $response $context $bounds $before.parsed $after.parsed
    $artifact = [pscustomobject][ordered]@{
        schema = 'moneyway.mt5-correlated-tick-evidence'; version = 1; nonce = $nonce; outcome = $outcome; reason = $failure
        method = 'ready_then_nonce_request_then_subsequent_OnTick_then_host_receipt'; session_limit_seconds = $SessionSeconds
        query_timeout_milliseconds = $QueryTimeoutMilliseconds; bounds = $bounds
        host_clock_provenance = 'DateTimeOffset.UtcNow_computer_clock_unverified'; source_query = $source; status_query = $status
        reference_before = $before; reference_after = $after; response = $response; context = $context
        assessment = $assessment; files = @($integrity)
        tool_sources = @(@((Join-Path $PSScriptRoot 'Invoke-Mt5CorrelatedTickCapture.ps1'), (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1'),
            (Join-Path $PSScriptRoot 'Mt5NativeUtcEvidence.ps1'), (Join-Path $PSScriptRoot 'MoneyWayCorrelatedTick.mq5'),
            (Join-Path $PSScriptRoot 'MoneyWayCorrelatedTickDecision.mqh')) | ForEach-Object {
            [pscustomobject]@{ file_name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
        })
        canonical_utc_conversion_authorized = $false
    }
    $report = Join-Path $directory 'evidence.json'
    Publish-Mt5TickFile $report (($artifact | ConvertTo-Json -Depth 20) + "`n")
    [pscustomobject]@{ Nonce = $nonce; ReportPath = $report; Outcome = $outcome; Reason = $failure
        SHA256 = (Get-FileHash -LiteralPath $report -Algorithm SHA256).Hash.ToLowerInvariant(); CanonicalUtcConversionAuthorized = $false }
}
