# Synthetic, offline tests. Shared MQL scalar decisions run in C#, without terminal APIs.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tools = Join-Path $PSScriptRoot '../../tools/market-data/mt5'
. (Join-Path $tools 'Mt5CorrelatedTickEvidence.ps1')
. (Join-Path $PSScriptRoot 'Mt5Probe.SyntheticFixtures.ps1')
$kernel = [IO.File]::ReadAllText((Join-Path $tools 'MoneyWayCorrelatedTickDecision.mqh'))
$kernel = [regex]::Replace($kernel, '(?m)^(bool|int) (Tick\w+)\(', 'public static $1 $2(')
Add-Type -TypeDefinition ('public static class MoneyWayTickKernel {' + $kernel + '}')
$passed = 0; $failed = 0
function Check([bool]$Condition) { if (-not $Condition) { throw 'assertion_failed' } }
function Reject([scriptblock]$Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Check $rejected
}
function Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Output "PASS: $Name" }
    catch { $script:failed++; Write-Output "FAIL: $Name - $($_.Exception.Message)" }
}
$nonce = 'a' * 64; $offerHash = 'b' * 64; $requestHash = 'c' * 64
function Identity {
    [pscustomobject]@{ company = 'OANDA Global Markets Limited'; server = 'OANDA_Global-Demo-1'; symbol = 'US100'
        demo = $true; connected = $true; algo_trading_enabled = $false }
}
function Ready {
    [pscustomobject]@{ schema = 'moneyway.mt5-correlated-tick-ready'; version = 1; nonce = $nonce; offer_sha256 = $offerHash
        ready_token = '100-200-300'; source = Identity }
}
function Tick([string]$Display, [int]$Milliseconds) {
    $native = Native $Display
    [pscustomobject]@{ time = $native; time_msc = [long]$native.raw * 1000 + $Milliseconds
        bid = '29500.1234567890123456'; ask = '29501.1234567890123456'; last = '0.0000000000000000'
        volume = '18446744073709551615'; flags = 6; volume_real = '0.0000000000000000' }
}
function Response {
    [pscustomobject]@{ schema = 'moneyway.mt5-correlated-tick-response'; version = 1; nonce = $nonce
        ready_token = '100-200-300'; request_sha256 = $requestHash; source = Identity
        context_sha256 = 'd' * 64
        outcome = 'captured'; reason = 'subsequent_ontick_timestamp_progressed'; event = 'OnTick'
        started_ms = 100; armed_ms = 200; handler_ms = 210; finished_ms = 220
        armed_callback_count = 5; callback_count = 6; cached_callbacks = 0; tick_error = 0
        baseline = Tick '2026.11.13 19:00:00' 100; tick = Tick '2026.11.13 19:00:01' 125
        canonical_utc_conversion_authorized = $false }
}
function Bounds {
    [pscustomobject]@{ request_publish_before = [pscustomobject]@{ utc_candidate = '2026-11-13T17:00:00Z'; monotonic = 10 }
        receipt_after = [pscustomobject]@{ utc_candidate = '2026-11-13T17:00:02Z'; monotonic = 20 } }
}
function Context { [pscustomobject]@{ time_gmt = Native '2026.11.13 17:00:01' } }
$reference = [pscustomobject]@{ status = 'context_specific'; reference_authentication = 'unverified' }
$absent = [pscustomobject]@{ status = 'data_unavailable'; reference_authentication = 'unverified' }
$directory = Join-Path $env:TEMP ('moneyway-correlated-test-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($directory) | Out-Null
try {
    Case 'Cryptographic nonces have 256-bit lowercase encoding and distinct values' {
        $first = New-Mt5TickNonce; $second = New-Mt5TickNonce
        Check ($first -cmatch '^[0-9a-f]{64}$' -and $second -cne $first)
    }
    Case 'Successful ready/request/response correlation validates exact hashes and identity' {
        Assert-Mt5TickReady (Ready) $nonce $offerHash
        Assert-Mt5TickResponse (Response) $nonce '100-200-300' $requestHash
    }
    Case 'Actual EA decision requires a subsequent OnTick callback and timestamp progression' {
        Check ([MoneyWayTickKernel]::TickProgressed($true,6,5,1001,1000))
        Check (-not [MoneyWayTickKernel]::TickProgressed($false,6,5,1001,1000))
    }
    Case 'A cached lookup in a new callback remains ineligible' {
        Check (-not [MoneyWayTickKernel]::TickProgressed($true,6,5,1000,1000))
        $r = Response; $r.tick = $r.baseline
        Reject { Assert-Mt5TickResponse $r $nonce '100-200-300' $requestHash }
    }
    Case 'Callback from before arming remains ineligible' {
        Check (-not [MoneyWayTickKernel]::TickProgressed($true,5,5,1001,1000))
    }
    Case 'No tick at the exact session deadline terminates' { Check ([MoneyWayTickKernel]::TickStopReason($false,$true,$true,1000,1000,$false,0,500) -eq 2) }
    Case 'No tick at capture deadline terminates independently' { Check ([MoneyWayTickKernel]::TickStopReason($false,$true,$true,900,1000,$true,500,500) -eq 5) }
    Case 'Cancellation terminates even when source is disconnected' { Check ([MoneyWayTickKernel]::TickStopReason($true,$true,$false,0,1000,$true,0,500) -eq 1) }
    Case 'Disconnected terminal terminates' { Check ([MoneyWayTickKernel]::TickStopReason($false,$true,$false,0,1000,$true,0,500) -eq 4) }
    Case 'Changed or unavailable source terminates' { Check ([MoneyWayTickKernel]::TickStopReason($false,$false,$true,0,1000,$true,0,500) -eq 3) }
    Case 'Session before deadline waits without a retry acquisition' { Check ([MoneyWayTickKernel]::TickStopReason($false,$true,$true,999,1000,$false,0,500) -eq 0) }
    Case 'Duplicate nonce with existing ready is rejected by actual EA decision' { Check (-not [MoneyWayTickKernel]::TickNonceEligible($true,$false,$false,$false)) }
    Case 'Stale request existing before readiness is rejected' { Check (-not [MoneyWayTickKernel]::TickNonceEligible($false,$true,$false,$false)) }
    Case 'Replayed response makes nonce permanently ineligible' { Check (-not [MoneyWayTickKernel]::TickNonceEligible($false,$false,$true,$false)) }
    Case 'Cancelled nonce cannot restart' { Check (-not [MoneyWayTickKernel]::TickNonceEligible($false,$false,$false,$true)) }
    Case 'Fresh nonce directory is eligible' { Check ([MoneyWayTickKernel]::TickNonceEligible($false,$false,$false,$false)) }
    foreach ($field in @('nonce','ready_token','request_sha256')) {
        Case "Response mismatch $field is rejected" {
            $r = Response; $r.$field = 'wrong'
            Reject { Assert-Mt5TickResponse $r $nonce '100-200-300' $requestHash }
        }
    }
    Case 'Wrong readiness nonce or offer digest is rejected' {
        Reject { Assert-Mt5TickReady (Ready) ('d' * 64) $offerHash }
        Reject { Assert-Mt5TickReady (Ready) $nonce ('e' * 64) }
    }
    foreach ($field in @('company','server','symbol','demo','connected','algo_trading_enabled')) {
        Case "Invalid source field $field is rejected" {
            $r = Response
            $r.source.$field = switch ($field) { 'demo' { $false } 'connected' { $false } 'algo_trading_enabled' { $true } default { 'wrong' } }
            Reject { Assert-Mt5TickResponse $r $nonce '100-200-300' $requestHash }
        }
    }
    Case 'Fabricated snapshot event cannot qualify as OnTick' {
        $r = Response; $r.event = 'snapshot'
        Reject { Assert-Mt5TickResponse $r $nonce '100-200-300' $requestHash }
    }
    Case 'Native milliseconds and second coordinate must agree' {
        $r = Response; $r.tick.time_msc += 1000
        Reject { Assert-Mt5TickResponse $r $nonce '100-200-300' $requestHash }
    }
    Case 'Capture counters cannot regress or authorize before arming' {
        $r = Response; $r.handler_ms = 199
        Reject { Assert-Mt5TickResponse $r $nonce '100-200-300' $requestHash }
    }
    Case 'Partial JSON response is rejected' {
        $path = Join-Path $directory 'partial.json'; Publish-Mt5TickFile $path '{'
        Reject { Read-Mt5TickFile $path -Json }
    }
    Case 'Missing request cannot become completed evidence' { Reject { Read-Mt5TickFile (Join-Path $directory 'missing.txt') } }
    Case 'Atomic publication has no partial final or remaining pending file' {
        $path = Join-Path $directory 'atomic.json'; Publish-Mt5TickFile $path '{"nonce":"synthetic"}'
        Check ((Read-Mt5TickFile $path -Json).data.nonce -ceq 'synthetic')
        Check (-not (Test-Path -LiteralPath ($path + '.pending')))
        Reject { Publish-Mt5TickFile $path 'replacement' }
    }
    Case 'Orphan pending publication is rejected without overwrite' {
        $path = Join-Path $directory 'orphan.json'; Write-Mt5EvidenceBytes ($path + '.pending') ([byte[]]@(1))
        Reject { Publish-Mt5TickFile $path 'replacement' }
        Check (-not (Test-Path -LiteralPath $path))
    }
    Case 'Valid capture bounds handler observation while source UTC remains unresolved' {
        $a = Get-Mt5TickAssessment (Response) (Context) (Bounds) $reference $reference
        Check ($a.handler_observation_bounded -and $a.external_reference_before_and_after_present -and -not $a.source_event_UTC_bounded)
        Check ($a.classification -eq 'context_specific' -and $a.network_quote_generated_after_request -eq 'unresolved')
    }
    Case 'Unavailable NTP before or after prevents sufficient reference evidence' {
        foreach ($pair in @(@($reference,$absent),@($absent,$reference))) {
            $a = Get-Mt5TickAssessment (Response) (Context) (Bounds) $pair[0] $pair[1]
            Check ($a.classification -eq 'data_unavailable' -and -not $a.external_reference_before_and_after_present)
        }
    }
    Case 'Unsynchronized Windows clock and unsigned reference remain unverified' {
        $a = Get-Mt5TickAssessment (Response) (Context) (Bounds) $reference $reference
        Check ($a.host_synchronization -eq 'unverified' -and $a.reference_authentication -eq 'unverified' -and -not $a.offset_correction_applied)
    }
    Case 'Conflicting or regressing host UTC evidence cannot bound the handler' {
        $b = Bounds; $b.receipt_after.utc_candidate = '2026-11-13T16:59:59Z'
        $a = Get-Mt5TickAssessment (Response) (Context) $b $reference $reference
        Check (-not $a.handler_observation_bounded -and $a.classification -eq 'data_unavailable')
    }
    Case 'Nonmonotonic host order is rejected independently of UTC' {
        $b = Bounds; $b.receipt_after.monotonic = 9
        Check (-not (Get-Mt5TickAssessment (Response) (Context) $b $reference $reference).handler_observation_bounded)
    }
    Case 'GMT plus two is a conditional interval hypothesis only' {
        $a = Get-Mt5TickAssessment (Response) (Context) (Bounds) $reference $reference
        Check ($a.policy_hypotheses[0].within_uncorrected_host_observation_interval -and -not $a.policy_hypotheses[1].within_uncorrected_host_observation_interval)
        Check (-not $a.canonical_utc_conversion_authorized -and $a.policy_classification -eq 'candidate')
    }
    Case 'GMT plus three is a conditional interval hypothesis only' {
        $r = Response; $r.tick = Tick '2026.11.13 20:00:01' 125
        $a = Get-Mt5TickAssessment $r (Context) (Bounds) $reference $reference
        Check ($a.policy_hypotheses[1].within_uncorrected_host_observation_interval -and -not $a.canonical_ready)
    }
    Case 'No invented tolerance widens a mismatching interval' {
        $r = Response; $r.tick = Tick '2026.11.13 19:00:03' 0
        $a = Get-Mt5TickAssessment $r (Context) (Bounds) $reference $reference
        Check (-not $a.policy_hypotheses[0].within_uncorrected_host_observation_interval)
    }
    Case 'Historical DST selection and closure remain unresolved' {
        $a = Get-Mt5TickAssessment (Response) (Context) (Bounds) $reference $reference
        Check ($a.historical_mapping -eq 'unresolved' -and $a.provider_dst_transitions -eq 'unresolved')
        Check ($a.unresolved -contains 'provider_candle_closure' -and $a.unresolved -contains 'historical_availability')
    }
    Case 'Native millisecond delta retains decimal precision' {
        $a = Get-Mt5TickAssessment (Response) (Context) (Bounds) $reference $reference
        Check ($a.exact_native_tick_minus_computer_gmt_seconds -eq [decimal]'7200.125')
    }
    Case 'Raw prices, reference offset and bundle hashes retain precision' {
        $r = Response; $path = Join-Path $directory 'response.json'
        Publish-Mt5TickFile $path (($r | ConvertTo-Json -Depth 12) + "`n")
        $file = Read-Mt5TickFile $path -Json
        Check ($file.data.tick.bid -ceq $r.tick.bid -and $file.data.tick.volume -ceq '18446744073709551615')
        Check ($file.sha256 -ceq (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant())
        $ft = ([datetime]'2026-11-13T17:00:00Z').ToFileTimeUtc()
        $n = Get-Mt5StripchartEvidence "1, 2, $ft, +00.1234567, -00.7654321" 0 $false 'synthetic.test'
        Check ($n.samples[0].reported_ntp_offset_seconds -eq [decimal]'-0.7654321')
    }
    Case 'Scientific native double strings retain all supplied significant digits' {
        $r = Response; $r.tick.bid = '2.9500123456789012e+04'; $r.tick.volume_real = '1.2345678901234567e-17'
        Assert-Mt5TickResponse $r $nonce '100-200-300' $requestHash
        Check ($r.tick.volume_real -ceq '1.2345678901234567e-17')
    }
    Case 'Malformed UTF8 and empty raw files remain hashable failure evidence' {
        $path = Join-Path $directory 'malformed.bin'; Write-Mt5EvidenceBytes $path ([byte[]]@(255,254))
        Reject { Read-Mt5TickFile $path -Json }
        Check ((Read-Mt5TickFile $path -Raw).sha256 -ceq (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant())
        $empty = Join-Path $directory 'empty.bin'; Write-Mt5EvidenceBytes $empty ([byte[]]@())
        Check ((Read-Mt5TickFile $empty -Raw).sha256.Length -eq 64)
    }
    Case 'Source-like receipt and checkpoint files survive attempted publication' {
        foreach ($name in @('record.json','checkpoint.json')) {
            $path = Join-Path $directory $name; Publish-Mt5TickFile $path '{"immutable":true}'
            $hash = (Read-Mt5TickFile $path).sha256
            Reject { Publish-Mt5TickFile $path 'replacement' }
            Check ((Read-Mt5TickFile $path).sha256 -ceq $hash)
        }
    }
    Case 'No response produces explicit data unavailable' {
        $a = Get-Mt5TickAssessment $null $null (Bounds) $absent $reference
        Check ($a.classification -eq 'data_unavailable' -and $a.fresh_tick_evidence -eq 'data_unavailable' -and -not $a.nonce_correlated)
    }
    Case 'Unobserved request or receipt boundaries stay absent' {
        $b = Bounds; $b.request_publish_before = $null; $b.receipt_after = $null
        $a = Get-Mt5TickAssessment $null $null $b $absent $reference
        Check ($null -eq $a.uncorrected_host_interval_milliseconds -and -not $a.handler_observation_bounded)
    }
    Case 'Data unavailable response cannot become fresh tick evidence' {
        $r = Response; $r.outcome = 'data_unavailable'; $r.tick = $null; $r.event = 'none'
        Assert-Mt5TickResponse $r $nonce '100-200-300' $requestHash
        Check ((Get-Mt5TickAssessment $r $null (Bounds) $reference $reference).fresh_tick_evidence -eq 'data_unavailable')
    }
    Case 'Execution and canonical ingestion APIs stay absent from actual diagnostic sources' {
        $ea = [IO.File]::ReadAllText((Join-Path $tools 'MoneyWayCorrelatedTick.mq5'))
        $coordinator = [IO.File]::ReadAllText((Join-Path $tools 'Invoke-Mt5CorrelatedTickCapture.ps1')) +
            [IO.File]::ReadAllText((Join-Path $tools 'Mt5CorrelatedTickEvidence.ps1'))
        Check ($ea -notmatch 'OrderSend|CTrade|Buy\(|Sell\(|WebRequest|FILE_REWRITE')
        Check ($coordinator -notmatch 'StrategyReplayContext|MoneyWayAutomaticReadOnly|checkpoint|/resync|/config')
        Check ($ea -match 'void OnTick\(' -and $ea -match 'SymbolInfoTick\(_Symbol, captured\)' -and $ea -match 'TERMINAL_TRADE_ALLOWED')
        Check ($ea -match 'FILE_READ \| FILE_BIN \| FILE_COMMON \| FILE_SHARE_READ')
        Check ($coordinator -match 'Get-Mt5TickReference' -and $coordinator -match 'Elapsed.TotalSeconds -lt \$SessionSeconds')
    }
    # Exercise the real coordinator with a simulated file peer and a fake Windows reference.
    # These fixtures never call MT5, W32Time or the network.
    $realTimeQuery = (Get-Item Function:Invoke-Mt5BoundedTimeQuery).ScriptBlock
    function Invoke-Mt5BoundedTimeQuery([string]$Arguments, [int]$TimeoutMilliseconds) {
        $stamp = [datetimeoffset]::UtcNow.ToString('o')
        $ft = [datetime]::UtcNow.ToFileTimeUtc()
        [pscustomobject]@{ raw = "synthetic unsynchronized reference`n1, 2, $ft, +00.1234567, -00.7654321"
            exit_code = 0; timed_out = $false; utc_before_candidate = $stamp; utc_after_candidate = $stamp }
    }
    function Start-Sleep([int]$Milliseconds) {
        $session = @(Get-ChildItem -LiteralPath $script:fakeRoot -Directory)[0].FullName
        $offer = Read-Mt5TickFile (Join-Path $session 'offer.txt')
        $sessionNonce = [IO.Path]::GetFileName($session)
        if ($script:fakeMode -eq 'cancel') {
            if (-not (Test-Path -LiteralPath (Join-Path $session 'cancel.txt'))) { Publish-Mt5TickFile (Join-Path $session 'cancel.txt') $sessionNonce }
            return
        }
        if (-not (Test-Path -LiteralPath (Join-Path $session 'ready.json'))) {
            $r = Ready; $r.nonce = $sessionNonce; $r.offer_sha256 = $offer.sha256
            Publish-Mt5TickFile (Join-Path $session 'ready.json') ($r | ConvertTo-Json -Depth 8)
            return
        }
        if ((Test-Path -LiteralPath (Join-Path $session 'request.txt')) -and -not (Test-Path -LiteralPath (Join-Path $session 'response.json'))) {
            $r = Response; $r.nonce = $sessionNonce
            $r.request_sha256 = (Read-Mt5TickFile (Join-Path $session 'request.txt')).sha256
            if ($script:fakeMode -eq 'wrong_nonce') { $r.nonce = 'f' * 64 }
            if ($script:fakeMode -eq 'no_tick') { $r.outcome = 'data_unavailable'; $r.reason = 'no_tick'; $r.tick = $null; $r.event = 'none' }
            $c = Context
            foreach ($name in @('time_current','time_trade_server','time_local','m1_head')) {
                $c | Add-Member -NotePropertyName $name -NotePropertyValue (Native '2026.11.13 19:00:00')
            }
            Publish-Mt5TickFile (Join-Path $session 'context.json') ($c | ConvertTo-Json -Depth 8)
            $r.context_sha256 = (Read-Mt5TickFile (Join-Path $session 'context.json')).sha256
            if ($script:fakeMode -eq 'wrong_context') { $r.context_sha256 = 'f' * 64 }
            Publish-Mt5TickFile (Join-Path $session 'response.json') ($r | ConvertTo-Json -Depth 12)
        }
    }
    foreach ($mode in @('success','wrong_nonce','wrong_context','no_tick','cancel')) {
        Case "Real coordinator synthetic session $mode preserves ordering and evidence" {
            $script:fakeMode = $mode
            $script:fakeRoot = Join-Path $directory $mode
            $result = Invoke-Mt5TickSession -Root $script:fakeRoot -SessionSeconds 5 -ReferenceHost synthetic.test -QueryTimeoutMilliseconds 1000
            $file = Read-Mt5TickFile $result.ReportPath -Json
            Check ($file.data.nonce -ceq $result.Nonce -and $result.SHA256 -ceq $file.sha256)
            Check (-not $file.data.canonical_utc_conversion_authorized)
            foreach ($inputFile in $file.data.files) {
                Check ($inputFile.sha256 -ceq (Read-Mt5TickFile (Join-Path ([IO.Path]::GetDirectoryName($result.ReportPath)) $inputFile.file_name) -Raw).sha256)
            }
            if ($mode -eq 'success') {
                Check ($result.Outcome -eq 'captured' -and $file.data.assessment.handler_observation_bounded)
                Check ($file.data.bounds.request_publish_before.monotonic -le $file.data.bounds.receipt_after.monotonic)
                Check ($file.data.reference_before.parsed.status -eq 'context_specific' -and $file.data.reference_after.parsed.status -eq 'context_specific')
            } else {
                Check ($result.Outcome -eq 'data_unavailable')
                if ($mode -eq 'no_tick') { Check ($result.Reason -eq 'no_tick') }
                if ($mode -eq 'cancel') { Check ($result.Reason -eq 'cancelled') }
            }
        }
    }
    Set-Item Function:Invoke-Mt5BoundedTimeQuery $realTimeQuery
    Remove-Item Function:Start-Sleep
} finally {
    # Delete only the explicitly owned synthetic workspace after verifying its resolved boundary.
    $resolved = (Resolve-Path -LiteralPath $directory).Path
    $expectedParent = [IO.Path]::GetFullPath($env:TEMP).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notlike 'moneyway-correlated-test-*') { throw 'unsafe_test_cleanup_path' }
    Remove-Item -LiteralPath $resolved -Recurse
}
Write-Output "Passed: $passed; Failed: $failed; Skipped: 0"
if ($failed -gt 0) { throw 'Correlated MT5 evidence checks failed.' }
