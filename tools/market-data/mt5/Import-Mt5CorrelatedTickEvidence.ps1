[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [Parameter(Mandatory = $true)][string]$RetentionRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedEvidenceSHA256,
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedResponseSHA256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5CorrelatedTickEvidence.ps1')

function Require([bool]$Condition, [string]$Reason) {
    if (-not $Condition) { throw $Reason }
}

# Value comparison only. Contract validation remains in the existing diagnostic helpers.
function SameValue($Left, $Right) {
    if ($null -eq $Left -or $null -eq $Right) { return $null -eq $Left -and $null -eq $Right }
    if ($Left -is [System.Array]) {
        if ($Right -isnot [System.Array] -or $Left.Count -ne $Right.Count) { return $false }
        for ($index = 0; $index -lt $Left.Count; $index++) {
            if (-not (SameValue $Left[$index] $Right[$index])) { return $false }
        }
        return $true
    }
    if ($Left.GetType() -eq [System.Management.Automation.PSCustomObject]) {
        if ($Right.GetType() -ne [System.Management.Automation.PSCustomObject]) { return $false }
        $keys = @($Left.PSObject.Properties.Name | Sort-Object)
        $otherKeys = @($Right.PSObject.Properties.Name | Sort-Object)
        if (-not (SameValue $keys $otherKeys)) { return $false }
        foreach ($key in $keys) {
            if (-not (SameValue $Left.$key $Right.$key)) { return $false }
        }
        return $true
    }
    return $Left -ceq $Right
}

$handles = New-Object System.Collections.Generic.List[System.IDisposable]
function ReadOriginal([string]$Name, [bool]$Json) {
    $path = Join-Path $source $Name
    Assert-Mt5TickLocalPath $path
    $stream = New-Object IO.FileStream($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $handles.Add($stream)
    $item = Read-Mt5TickFile $path -Json:$Json -Raw:($Name -ceq 'cancel.txt')
    $bytes = New-Object byte[] ([int]$stream.Length)
    $offset = 0
    while ($offset -lt $bytes.Length) {
        $count = $stream.Read($bytes, $offset, $bytes.Length - $offset)
        Require ($count -gt 0) 'incomplete_source_read'
        $offset += $count
    }
    Require ((Get-Mt5TickHash $bytes) -ceq $item.sha256) 'source_read_hash_mismatch'
    if ($Name -ceq 'cancel.txt') { $item.text = (New-Object Text.UTF8Encoding($false, $true)).GetString($bytes) }
    $item | Add-Member -NotePropertyName bytes -NotePropertyValue $bytes
    return $item
}

try {
    Assert-Mt5TickLocalPath $SourceDirectory
    Assert-Mt5TickLocalPath $RetentionRoot
    $source = (Get-Item -LiteralPath $SourceDirectory).FullName.TrimEnd('\')
    $root = (Get-Item -LiteralPath $RetentionRoot).FullName.TrimEnd('\')
    Require ((Get-Item -LiteralPath $source) -is [IO.DirectoryInfo]) 'source_directory_required'
    Require ((Get-Item -LiteralPath $root) -is [IO.DirectoryInfo]) 'retention_directory_required'
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')).TrimEnd('\')
    $common = [IO.Path]::GetFullPath((Join-Path $env:APPDATA 'MetaQuotes\Terminal\Common\Files')).TrimEnd('\')
    foreach ($excluded in @($source, $repo, $common)) {
        Require (-not $root.Equals($excluded, [StringComparison]::OrdinalIgnoreCase) -and
            -not $root.StartsWith($excluded + '\', [StringComparison]::OrdinalIgnoreCase)) 'retention_location_overlaps_source_repository_or_terminal'
    }
    Require (-not $source.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) 'retention_location_contains_source'
    $names = @('offer.txt','ready.json','request.txt','context.json','response.json','cancel.txt','evidence.json')
    $actualNames = @(Get-ChildItem -LiteralPath $source -Force | Select-Object -ExpandProperty Name)
    Require (@($actualNames | Where-Object { $_ -cnotin $names -and $_ -cne 'session.lock' }).Count -eq 0) 'partial_or_unexpected_source_bundle'
    $inputs = @{}
    foreach ($name in $names) { $inputs[$name] = ReadOriginal $name ($name.EndsWith('.json')) }
    $evidence = $inputs['evidence.json'].data
    $response = $inputs['response.json'].data
    $ready = $inputs['ready.json'].data
    $context = $inputs['context.json'].data
    Require ($inputs['evidence.json'].sha256 -ceq $ExpectedEvidenceSHA256) 'evidence_hash_mismatch'
    Require ($inputs['response.json'].sha256 -ceq $ExpectedResponseSHA256) 'response_hash_mismatch'
    Require ($evidence.schema -ceq 'moneyway.mt5-correlated-tick-evidence' -and $evidence.version -eq 1 -and
        $evidence.nonce -cmatch '^[0-9a-f]{64}$' -and [IO.Path]::GetFileName($source) -ceq $evidence.nonce) 'invalid_evidence_schema_or_session_nonce'
    $nonce = $evidence.nonce
    Require ($evidence.outcome -ceq 'captured' -and $evidence.reason -ceq 'subsequent_ontick_timestamp_progressed' -and
        $evidence.canonical_utc_conversion_authorized -ceq $false) 'unapproved_capture_outcome_or_authorization'
    $manifestNames = @($evidence.files | Select-Object -ExpandProperty file_name)
    Require ($manifestNames.Count -eq 6 -and @($manifestNames | Select-Object -Unique).Count -eq 6 -and
        @($manifestNames | Where-Object { $_ -cnotin $names -or $_ -ceq 'evidence.json' }).Count -eq 0) 'invalid_source_manifest'
    foreach ($entry in $evidence.files) {
        Require ($entry.sha256 -ceq $inputs[$entry.file_name].sha256) 'source_manifest_hash_mismatch'
    }
    Assert-Mt5TickReady $ready $nonce $inputs['offer.txt'].sha256
    Assert-Mt5TickResponse $response $nonce $ready.ready_token $inputs['request.txt'].sha256
    Require ($response.outcome -ceq 'captured' -and $response.context_sha256 -ceq $inputs['context.json'].sha256 -and
        $response.lookup_event_identity -ceq 'unresolved_queued_event_and_latest_lookup') 'context_or_event_identity_mismatch'
    Require (SameValue $ready.source $response.source) 'source_identity_changed'
    Require (SameValue $evidence.response $response) 'embedded_response_mismatch'
    Require (SameValue $evidence.context $context) 'embedded_context_mismatch'
    Require ($evidence.session_limit_seconds -ge 1 -and $evidence.session_limit_seconds -le 900 -and
        $evidence.query_timeout_milliseconds -ge 1000 -and $evidence.query_timeout_milliseconds -le 30000) 'invalid_operator_limits'
    $offer = "moneyway.mt5-correlated-tick-offer/v1`n$nonce`n$($evidence.session_limit_seconds)`nUS100`n"
    Require ($inputs['offer.txt'].text -ceq $offer) 'invalid_offer'
    $requestLines = $inputs['request.txt'].text -split "`n"
    Require ($requestLines.Count -eq 6) 'partial_request'
    $remaining = [long]::Parse($requestLines[3], [cultureinfo]::InvariantCulture)
    Require ($remaining -gt 0 -and $remaining -le $evidence.session_limit_seconds * 1000) 'invalid_request_duration'
    $request = "moneyway.mt5-correlated-tick-request/v1`n$nonce`n$($ready.ready_token)`n$remaining`nUS100`n"
    Require ($inputs['request.txt'].text -ceq $request) 'invalid_request_binding'
    Require ($inputs['cancel.txt'].text -ceq "$nonce`n" -or $inputs['cancel.txt'].text -ceq '') 'invalid_cancellation_marker'
    foreach ($name in @('time_current','time_trade_server','time_gmt','time_local','m1_head')) {
        Get-Mt5NativeCoordinate $context.$name | Out-Null
    }
    $previous = $null
    foreach ($name in @('offer_publish_before','offer_publish_after','request_publish_before','request_publish_after','receipt_before','receipt_after','session_ended')) {
        $boundary = $evidence.bounds.$name
        $utc = [datetimeoffset]::Parse($boundary.utc_candidate, [cultureinfo]::InvariantCulture)
        Require ($utc.Offset -eq [timespan]::Zero -and ($boundary.monotonic -is [long] -or $boundary.monotonic -is [int])) 'invalid_host_boundary'
        if ($null -ne $previous) {
            Require ($previous.utc -le $utc -and $previous.monotonic -le $boundary.monotonic) 'host_observation_order_conflict'
        }
        $previous = [pscustomobject]@{ utc = $utc; monotonic = $boundary.monotonic }
    }
    Require ($evidence.bounds.monotonic_frequency -gt 0) 'invalid_monotonic_frequency'
    $references = @()
    foreach ($name in @('reference_before','reference_after')) {
        $reference = $evidence.$name
        $hostName = $reference.parsed.reference_host
        Require ($hostName -cmatch '^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$' -and $reference.query.arguments -ceq
            "/stripchart /computer:$hostName /samples:1 /period:1 /rdtsc /dataonly") 'invalid_reference_provenance'
        $parsed = Get-Mt5StripchartEvidence $reference.query.raw $reference.query.exit_code $reference.query.timed_out $reference.parsed.reference_host
        Require (SameValue $reference.parsed $parsed) 'reference_raw_parse_mismatch'
        Require ($parsed.status -ceq 'context_specific') 'reference_unavailable'
        $queryBefore = [datetimeoffset]::Parse($reference.query.utc_before_candidate)
        $queryAfter = [datetimeoffset]::Parse($reference.query.utc_after_candidate)
        Require ($queryBefore -le $queryAfter) 'reference_query_order_conflict'
        if ($name -ceq 'reference_before') {
            Require ($queryAfter -le [datetimeoffset]::Parse($evidence.bounds.request_publish_before.utc_candidate)) 'reference_not_before_request'
        } else {
            Require ($queryBefore -ge [datetimeoffset]::Parse($evidence.bounds.receipt_after.utc_candidate)) 'reference_not_after_receipt'
        }
        $references += $parsed
    }
    $assessment = Get-Mt5TickAssessment $response $context $evidence.bounds $references[0] $references[1]
    Require (SameValue $evidence.assessment $assessment) 'reported_assessment_mismatch'
    $toolNames = @('Invoke-Mt5CorrelatedTickCapture.ps1','Mt5CorrelatedTickEvidence.ps1','Mt5NativeUtcEvidence.ps1','MoneyWayCorrelatedTick.mq5','MoneyWayCorrelatedTickDecision.mqh')
    Require ($evidence.tool_sources.Count -eq $toolNames.Count -and
        @($evidence.tool_sources.file_name | Select-Object -Unique).Count -eq $toolNames.Count) 'invalid_tool_provenance'
    $tools = @()
    foreach ($tool in $evidence.tool_sources) {
        Require ($tool.file_name -cin $toolNames -and $tool.sha256 -cmatch '^[0-9a-f]{64}$') 'invalid_tool_reference'
        $current = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $tool.file_name) -Algorithm SHA256).Hash.ToLowerInvariant()
        $tools += [pscustomobject]@{ file_name = $tool.file_name; captured_sha256 = $tool.sha256; reviewed_repository_sha256 = $current
            matches_reviewed_repository = $current -ceq $tool.sha256; deployed_binary_authenticated = $false }
    }
    $fileManifest = @($names | ForEach-Object { $inputs[$_] | Select-Object file_name,sha256 })
    $verification = [pscustomobject]@{ source_hash_pins = $true; source_manifest = $true; nonce_and_request_binding = $true
        response_context_binding = $true; embedded_values = $true; host_order = $true; raw_ntp_reparsed = $true; assessment_recomputed = $true }
    $target = Join-Path $root $nonce
    if (Test-Path -LiteralPath $target) {
        Assert-Mt5TickLocalPath $target
        $retainedNames = @(Get-ChildItem -LiteralPath $target -Force | Select-Object -ExpandProperty Name)
        Require ($retainedNames.Count -eq 9 -and
            @($retainedNames | Where-Object { $_ -cnotin ($names + @('retention.json','retention.sha256')) }).Count -eq 0) 'partial_or_conflicting_retained_session'
        $stored = Read-Mt5TickFile (Join-Path $target 'retention.json') -Json
        $checksum = Read-Mt5TickFile (Join-Path $target 'retention.sha256')
        Require ($checksum.text -ceq ($stored.sha256 + "`n")) 'retention_manifest_hash_mismatch'
        Require ($stored.data.schema -ceq 'moneyway.mt5-correlated-tick-retention' -and $stored.data.version -eq 1 -and
            $stored.data.nonce -ceq $nonce -and (SameValue $stored.data.files $fileManifest) -and
            $stored.data.canonical_utc_conversion_authorized -ceq $false -and
            $stored.data.source_authenticity -ceq 'human_attested_local_artifacts' -and
            $stored.data.review_method -ceq 'offline_hash_contract_lineage_and_assessment_revalidation' -and
            $stored.data.captured_time_msc -eq $response.tick.time_msc -and
            (SameValue $stored.data.captured_native_tick $response.tick.time) -and
            (SameValue $stored.data.original_host_observation_bounds $evidence.bounds) -and
            (SameValue $stored.data.verification $verification) -and
            (SameValue $stored.data.recomputed_assessment $assessment)) 'conflicting_retained_session'
        Require ([datetimeoffset]::Parse($stored.data.reviewed_at_host_utc_candidate).Offset -eq [timespan]::Zero -and
            [IO.Path]::IsPathRooted($stored.data.original_source_directory) -and
            [IO.Path]::GetFileName($stored.data.original_source_directory) -ceq $nonce) 'invalid_retained_review_provenance'
        Require ($stored.data.tool_provenance.Count -eq $toolNames.Count -and
            @($stored.data.tool_provenance.file_name | Select-Object -Unique).Count -eq $toolNames.Count) 'invalid_retained_tool_provenance'
        foreach ($tool in $stored.data.tool_provenance) {
            $original = @($evidence.tool_sources | Where-Object file_name -CEQ $tool.file_name)
            Require ($original.Count -eq 1 -and $original[0].sha256 -ceq $tool.captured_sha256 -and
                $tool.reviewed_repository_sha256 -cmatch '^[0-9a-f]{64}$' -and $tool.deployed_binary_authenticated -ceq $false -and
                $tool.matches_reviewed_repository -ceq ($tool.captured_sha256 -ceq $tool.reviewed_repository_sha256)) 'conflicting_retained_tool_provenance'
        }
        foreach ($file in $fileManifest) {
            Require ((Read-Mt5TickFile (Join-Path $target $file.file_name) -Raw).sha256 -ceq $file.sha256) 'conflicting_retained_file'
        }
        $manifestHash = $stored.sha256
        $result = 'already_retained'
    } else {
        $pending = Join-Path $root ('.pending-' + [guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($pending) | Out-Null
        foreach ($name in $names) { Write-Mt5EvidenceBytes (Join-Path $pending $name) $inputs[$name].bytes }
        $review = [pscustomobject][ordered]@{
            schema = 'moneyway.mt5-correlated-tick-retention'; version = 1; nonce = $nonce
            original_source_directory = $source; source_authenticity = 'human_attested_local_artifacts'
            reviewed_at_host_utc_candidate = [datetimeoffset]::UtcNow.ToString('o')
            review_method = 'offline_hash_contract_lineage_and_assessment_revalidation'
            captured_native_tick = $response.tick.time; captured_time_msc = $response.tick.time_msc
            original_host_observation_bounds = $evidence.bounds; files = $fileManifest; tool_provenance = $tools
            verification = $verification
            recomputed_assessment = $assessment; canonical_utc_conversion_authorized = $false
        }
        Publish-Mt5TickFile (Join-Path $pending 'retention.json') (($review | ConvertTo-Json -Depth 20) + "`n")
        $manifestHash = (Read-Mt5TickFile (Join-Path $pending 'retention.json')).sha256
        Publish-Mt5TickFile (Join-Path $pending 'retention.sha256') ($manifestHash + "`n")
        foreach ($file in $fileManifest) {
            Require ((Read-Mt5TickFile (Join-Path $pending $file.file_name) -Raw).sha256 -ceq $file.sha256) 'retained_copy_hash_mismatch'
        }
        [IO.Directory]::Move($pending, $target)
        $result = 'retained'
    }
    [pscustomobject]@{ Result = $result; Nonce = $nonce; RetainedDirectory = $target; EvidenceSHA256 = $ExpectedEvidenceSHA256
        ResponseSHA256 = $ExpectedResponseSHA256; RetentionManifestSHA256 = $manifestHash
        NativeProgressionMilliseconds = $response.tick.time_msc - $response.baseline.time_msc
        Assessment = $assessment; ToolProvenance = $tools; CanonicalUtcConversionAuthorized = $false }
} finally {
    foreach ($handle in $handles) { $handle.Dispose() }
}
