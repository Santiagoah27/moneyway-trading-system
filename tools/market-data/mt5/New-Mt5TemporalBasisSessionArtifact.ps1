[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WindowStartHostUtcCandidate,
    [Parameter(Mandatory = $true)][string]$WindowEndHostUtcCandidate,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$ReceiptCutReport,
    [ValidatePattern('^[0-9a-f]{64}$')][string]$ReceiptCutReportSHA256,
    [string]$CorrelationSourceDirectory,
    [string]$RetentionRoot,
    [ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedEvidenceSHA256,
    [ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedResponseSHA256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1')

function Require([bool]$Condition, [string]$Reason) {
    if (-not $Condition) { throw $Reason }
}

function HostLabel([string]$Value) {
    Require ($Value -cmatch '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|\+00:00)$') 'explicit_host_utc_candidate_required'
    return [datetimeoffset]::Parse($Value, [cultureinfo]::InvariantCulture)
}

function InWindow([datetimeoffset]$Value) {
    return $Value -ge $start -and $Value -lt $end
}

function ReadCut([string]$Path, [string]$ExpectedHash) {
    Assert-Mt5TickLocalPath $Path
    $stream = [IO.File]::Open($Path, 'Open', 'Read', 'Read')
    try {
        Require ($stream.Length -gt 0 -and $stream.Length -le 16777216) 'receipt_cut_size_invalid'
        $buffer = New-Object byte[] ([int]$stream.Length)
        for ($read = 0; $read -lt $buffer.Length; $read += $count) {
            $count = $stream.Read($buffer, $read, $buffer.Length - $read)
            Require ($count -gt 0) 'receipt_cut_incomplete'
        }
        Require ($stream.ReadByte() -eq -1 -and (Get-Mt5TickHash $buffer) -ceq $ExpectedHash) 'receipt_cut_hash_mismatch'
        $audit = ((New-Object Text.UTF8Encoding($false, $true)).GetString($buffer) | ConvertFrom-Json).audit
    } finally { $stream.Dispose() }
    Require ($audit.schema -ceq 'moneyway.nasdaq-prospective-receipt-cut-diagnostic' -and $audit.version -eq 1 -and
        $audit.integrity -ceq 'Validated' -and $audit.canonical_ready -is [bool] -and -not $audit.canonical_ready -and
        $audit.canonical_admission_permitted -is [bool] -and -not $audit.canonical_admission_permitted -and
        $audit.operational_signal -is [bool] -and -not $audit.operational_signal -and
        $audit.standard_evaluation -ceq 'NotEvaluated') 'invalid_receipt_cut_contract'
    Require ($audit.expected_source.company -ceq $identity.company -and $audit.expected_source.server -ceq $identity.server -and
        $audit.expected_source.native_symbol -ceq $identity.symbol -and $audit.expected_source.demo -ceq $true) 'receipt_cut_source_mismatch'
    $cut = $audit.cut_receipt
    Require ($cut.sequence -gt 0 -and $cut.sequence -eq $audit.boundary.sequence -and
        $cut.file_name -ceq ('record-{0:D12}.json' -f [long]$cut.sequence) -and
        $cut.sha256 -cmatch '^[0-9a-f]{64}$' -and $cut.sha256 -ceq $audit.boundary.record_sha256 -and
        $audit.prefix_manifest_sha256 -cmatch '^[0-9a-f]{64}$') 'receipt_cut_identity_mismatch'
    $anchor = 0
    if ($null -ne $audit.anchor) {
        $anchor = $audit.anchor.sequence
        Require ($anchor -gt 0 -and $anchor -lt $cut.sequence -and
            $audit.anchor.record_sha256 -cmatch '^[0-9a-f]{64}$') 'receipt_cut_anchor_invalid'
    }
    Require ($null -ne $audit.cut_local_receipt_clock_label) 'receipt_cut_clock_label_unavailable'
    $cutTime = HostLabel $audit.cut_local_receipt_clock_label
    Require (InWindow $cutTime) 'receipt_cut_outside_session'
    Require (@($audit.notices | Where-Object { $_.reason -cin @('local_receipt_clock_regressed',
        'collector_clock_candidate_regressed', 'local_receipt_label_conflict') }).Count -eq 0) 'receipt_cut_clock_order_unresolved'
    $coordinates = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($sample in $audit.samples) {
        $first = $sample.first_usable_historical
        $receipt = $first.receipt
        Require ($sample.timeframe -cin @('H4','H1','M5','M1') -and $first.source_position -gt 0 -and
            $first.usable -ceq $true -and $first.complete_native_sample_observed -ceq $true -and
            $first.usable_frame -ceq $true -and $first.ohlc_valid -ceq $true -and
            $receipt.sequence -gt $anchor -and $receipt.sequence -le $cut.sequence -and
            $receipt.record_sha256 -cmatch '^[0-9a-f]{64}$' -and $receipt.probe_sha256 -cmatch '^[0-9a-f]{64}$' -and
            $receipt.record_file -ceq ('record-{0:D12}.json' -f [long]$receipt.sequence) -and
            $receipt.probe_file -ceq ('probe-{0:D12}.json' -f [long]$receipt.sequence)) 'invalid_or_future_frozen_sample'
        Require ($null -ne (Get-Mt5NativeCoordinate $sample.open_time_native)) 'invalid_sample_native_open'
        Require ($coordinates.Add("$($sample.timeframe):$($sample.open_time_native.raw)")) 'duplicate_frozen_sample'
        Require ($null -ne $sample.local_receipt_clock_label) 'sample_local_receipt_label_unavailable'
        $observed = HostLabel $sample.local_receipt_clock_label
        Require ((InWindow $observed) -and $observed -le $cutTime) 'sample_label_outside_session_or_after_cut'
        if ($receipt.sequence -eq $cut.sequence) {
            Require ($receipt.record_sha256 -ceq $cut.sha256 -and $observed -eq $cutTime) 'cut_receipt_sample_identity_mismatch'
        }
    }
    return $audit
}

function NativeSample($Sample, [int]$Period) {
    if ($null -eq $Sample) { return $null }
    $nominalClose = [long]$Sample.open_time_native.raw + $Period
    $epoch = [datetime]::new(1970, 1, 1)
    return [ordered]@{
        native_open = $Sample.open_time_native
        native_period_seconds = $Period
        nominal_close_native = [ordered]@{ raw = $nominalClose; display = $epoch.AddSeconds($nominalClose).ToString('yyyy.MM.dd HH:mm:ss') }
        first_usable_historical = $Sample.first_usable_historical
        local_receipt_clock_label = $Sample.local_receipt_clock_label
        provider_finalization = 'Unresolved'
    }
}

$start = HostLabel $WindowStartHostUtcCandidate
$end = HostLabel $WindowEndHostUtcCandidate
$bogota = [TimeZoneInfo]::FindSystemTimeZoneById('SA Pacific Standard Time')
$localStart = [TimeZoneInfo]::ConvertTime($start, $bogota)
$localEnd = [TimeZoneInfo]::ConvertTime($end, $bogota)
Require ($start -lt $end -and $localStart.Date -eq $localEnd.Date) 'single_bogota_session_window_required'
Require ([string]::IsNullOrWhiteSpace($ReceiptCutReport) -eq [string]::IsNullOrWhiteSpace($ReceiptCutReportSHA256)) 'receipt_cut_pin_required'
$correlationArgs = @($CorrelationSourceDirectory, $RetentionRoot, $ExpectedEvidenceSHA256, $ExpectedResponseSHA256)
$supplied = @($correlationArgs | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
Require ($supplied -eq 0 -or $supplied -eq $correlationArgs.Count) 'complete_correlation_bundle_pins_required'
Require ($OutputPath -match '^[A-Za-z]:[\\/]' -and -not $OutputPath.Substring(2).Contains(':')) 'absolute_local_output_path_required'
$output = [IO.Path]::GetFullPath($OutputPath)
Assert-Mt5TickLocalPath ([IO.Path]::GetDirectoryName($output))
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')).TrimEnd('\')
$common = [IO.Path]::GetFullPath((Join-Path $env:APPDATA 'MetaQuotes\Terminal\Common\Files')).TrimEnd('\')
foreach ($directory in @($repo, $common)) {
    Require (-not $output.StartsWith($directory + '\', [StringComparison]::OrdinalIgnoreCase)) 'output_must_be_outside_repository_and_terminal'
}
Require (-not (Test-Path -LiteralPath $output)) 'existing_sidecar_preserved'

$identity = [ordered]@{ company = 'OANDA Global Markets Limited'; server = 'OANDA_Global-Demo-1'; symbol = 'US100'; demo = $true }
$blockers = New-Object 'System.Collections.Generic.List[string]'
$audit = $null
$cutEvidence = $null
$tickEvidence = $null
$link = $null
$candidate = $false
$firstByFrame = [ordered]@{ H4 = $null; H1 = $null; M5 = $null; M1 = $null }
if ($ReceiptCutReport) {
    try {
        $audit = ReadCut $ReceiptCutReport $ReceiptCutReportSHA256
        $cutEvidence = [ordered]@{ report_sha256 = $ReceiptCutReportSHA256; prefix_manifest_sha256 = $audit.prefix_manifest_sha256
            anchor = $audit.anchor; cut_receipt = $audit.cut_receipt; local_receipt_clock_label = $audit.cut_local_receipt_clock_label
            continuity = $audit.observation_continuity; integrity = $audit.integrity }
        $periods = [ordered]@{ H4 = 14400; H1 = 3600; M5 = 300; M1 = 60 }
        foreach ($frame in $periods.Keys) {
            $first = $audit.samples | Where-Object timeframe -CEQ $frame |
                Sort-Object @{Expression = { $_.first_usable_historical.receipt.sequence }}, @{Expression = { $_.open_time_native.raw }} |
                Select-Object -First 1
            $firstByFrame[$frame] = NativeSample $first $periods[$frame]
        }
    } catch {
        $audit = $null; $cutEvidence = $null
        foreach ($frame in @('H4','H1','M5','M1')) { $firstByFrame[$frame] = $null }
        $blockers.Add($_.Exception.Message)
    }
} else { $blockers.Add('receipt_cut_pending') }

if ($CorrelationSourceDirectory) {
    try {
        $original = Read-Mt5TickFile (Join-Path $CorrelationSourceDirectory 'evidence.json') -Json
        Require ($original.sha256 -ceq $ExpectedEvidenceSHA256) 'evidence_hash_mismatch'
        $evidence = $original.data
        $captureStart = HostLabel $evidence.bounds.offer_publish_before.utc_candidate
        $requestBefore = HostLabel $evidence.bounds.request_publish_before.utc_candidate
        $receiptAfter = HostLabel $evidence.bounds.receipt_after.utc_candidate
        $sessionEnded = HostLabel $evidence.bounds.session_ended.utc_candidate
        Require ((InWindow $captureStart) -and (InWindow $requestBefore) -and (InWindow $receiptAfter) -and
            (InWindow $sessionEnded)) 'correlated_evidence_outside_session'
        Require ($evidence.reference_before.parsed.status -ceq 'context_specific' -and
            $evidence.reference_after.parsed.status -ceq 'context_specific') 'utc_reference_unavailable'
        $referenceAfter = HostLabel $evidence.reference_after.query.utc_after_candidate
        $referenceBefore = HostLabel $evidence.reference_before.query.utc_before_candidate
        Require ((InWindow $referenceBefore) -and (InWindow $referenceAfter)) 'correlated_reference_outside_session'
        if ($null -ne $audit) {
            $cutTime = HostLabel $audit.cut_local_receipt_clock_label
            Require ($referenceAfter -le $cutTime -and $sessionEnded -le $cutTime -and $receiptAfter -le $cutTime) 'correlation_evidence_after_cut'
        }
        $retained = & (Join-Path $PSScriptRoot 'Import-Mt5CorrelatedTickEvidence.ps1') -SourceDirectory $CorrelationSourceDirectory `
            -RetentionRoot $RetentionRoot -ExpectedEvidenceSHA256 $ExpectedEvidenceSHA256 -ExpectedResponseSHA256 $ExpectedResponseSHA256
        $assessment = $retained.Assessment
        $tickEvidence = [ordered]@{
            nonce = $retained.Nonce; evidence_sha256 = $retained.EvidenceSHA256; response_sha256 = $retained.ResponseSHA256
            retention_manifest_sha256 = $retained.RetentionManifestSHA256
            original_file_hashes = $evidence.files; tool_provenance = $retained.ToolProvenance
            source = $evidence.response.source; tick_native = $evidence.response.tick; baseline_native = $evidence.response.baseline
            native_context = $evidence.context; host_brackets = $evidence.bounds
            ea_counter_milliseconds = [ordered]@{ started = $evidence.response.started_ms; armed = $evidence.response.armed_ms
                handler = $evidence.response.handler_ms; finished = $evidence.response.finished_ms }
            utc_reference = [ordered]@{ before = $evidence.reference_before; after = $evidence.reference_after
                authentication = $assessment.reference_authentication; absolute_error_bound_seconds = $null }
            hypotheses = $assessment.policy_hypotheses
            hypothesis_basis = $assessment.policy_source
            assessment = $assessment
        }
        if ($null -ne $audit) {
            $head = Get-Mt5NativeCoordinate $evidence.context.m1_head
            Require ($null -ne $head -and $evidence.context.m1_error -eq 0 -and
                $evidence.response.tick.time_msc -ge [long]$head.raw * 1000 -and
                $evidence.response.tick.time_msc -lt ([long]$head.raw + 60) * 1000) 'tick_native_m1_head_unresolved'
            $matches = @($audit.samples | Where-Object { $_.timeframe -ceq 'M1' -and $_.open_time_native.raw -eq $head.raw })
            Require ($matches.Count -eq 1) 'matching_native_m1_not_in_cut'
            $sample = $matches[0]
            Require ((HostLabel $sample.local_receipt_clock_label) -ge $receiptAfter) 'm1_historical_observation_not_after_tick_capture'
            $link = [ordered]@{ status = 'SameSessionNativeM1AndLocalReceiptOrder'
                matching_m1 = (NativeSample $sample 60); scope = 'captured_tick_and_exact_frozen_m1_only'
                provider_publication_link = 'Unresolved'; utc_clock_continuity = 'Unresolved' }
            $candidate = $assessment.classification -ceq 'context_specific' -and
                @($assessment.policy_hypotheses | Where-Object within_uncorrected_host_observation_interval).Count -eq 1
            if (-not $candidate) { $blockers.Add('utc_offset_candidate_not_unique_or_reference_unavailable') }
        }
    } catch { $blockers.Add($_.Exception.Message) }
} else { $blockers.Add('correlated_evidence_pending') }

$blockers.Add('no_verified_native_to_utc_mapping_provider_finalization_or_utc_availability')
$artifact = [pscustomobject][ordered]@{
    schema = 'moneyway.nasdaq-prospective-temporal-basis-sidecar'; version = 1
    source = $identity; evaluation_mode = 'diagnostic_only'; standard_evaluation = 'NotEvaluated'
    session = [ordered]@{ date = $localStart.ToString('yyyy-MM-dd'); timezone = 'America/Bogota'
        start_host_utc_candidate = $WindowStartHostUtcCandidate; end_exclusive_host_utc_candidate = $WindowEndHostUtcCandidate }
    input_pins = [ordered]@{ receipt_cut_report_sha256 = $ReceiptCutReportSHA256
        evidence_sha256 = $ExpectedEvidenceSHA256; response_sha256 = $ExpectedResponseSHA256 }
    receipt_cut = $cutEvidence; first_usable_by_timeframe = $firstByFrame
    correlated_tick = $tickEvidence; same_session_link = $link; blockers = $blockers.ToArray()
    NativeTimeObserved = $null -ne $tickEvidence
    UtcCorrelationCandidate = $candidate
    UtcMappingVerified = $false
    ProviderFinalizationVerified = $false
    ReceiptVisibilityVerified = $null -ne $audit
    StandardEvaluationPermitted = $false
    CanonicalReady = $false
    CanonicalAdmissionPermitted = $false
    OperationalSignal = $false
}
Publish-Mt5TickFile $output (($artifact | ConvertTo-Json -Depth 24) + "`n")
$artifact
