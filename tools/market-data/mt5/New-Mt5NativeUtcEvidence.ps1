[CmdletBinding(DefaultParameterSetName = 'Probe')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Probe')][string]$ProbePath,
    [Parameter(Mandatory = $true, ParameterSetName = 'Record')][string]$RecordPath,
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$')][string]$ReferenceHost,
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [ValidateRange(1, 5)][int]$ReferenceSamples = 3,
    [ValidateRange(1000, 30000)][int]$QueryTimeoutMilliseconds = 10000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Mt5NativeUtcEvidence.ps1')
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
$handles = New-Object System.Collections.Generic.List[System.IDisposable]
$inputs = @()
function ReadEvidence([string]$Path, [string]$Role) {
    $readStarted = [Diagnostics.Stopwatch]::GetTimestamp()
    $readUtcBefore = [datetimeoffset]::UtcNow.ToString('o')
    $item = Get-Item -LiteralPath $Path
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'unsafe_evidence_path' }
    for ($node = $item.Directory; $null -ne $node; $node = $node.Parent) {
        if ($node.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'unsafe_evidence_path' }
    }
    if ($item.PSIsContainer -or $item.Length -le 0 -or $item.Length -gt 2097152) { throw 'invalid_evidence_file' }
    $stream = New-Object System.IO.FileStream($item.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $handles.Add($stream)
    $bytes = New-Object byte[] ([int]$stream.Length)
    $read = 0
    while ($read -lt $bytes.Length) {
        $count = $stream.Read($bytes, $read, $bytes.Length - $read)
        if ($count -eq 0) { throw 'incomplete_evidence_read' }
        $read += $count
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
    [pscustomobject]@{ role = $Role; file_name = $item.Name; source_path = $item.FullName; sha256 = $hash; bytes = $bytes
        read_started_monotonic = $readStarted; read_finished_monotonic = [Diagnostics.Stopwatch]::GetTimestamp()
        read_utc_before_candidate = $readUtcBefore; read_utc_after_candidate = [datetimeoffset]::UtcNow.ToString('o') }
}

try {
    $inputPath = if ($PSCmdlet.ParameterSetName -eq 'Record') { $RecordPath } else { $ProbePath }
    if ($inputPath -notmatch '^[A-Za-z]:[\\/]' -or $ReportPath -notmatch '^[A-Za-z]:[\\/]') { throw 'absolute_local_paths_required' }
    $inputRoot = (Get-Item -LiteralPath $inputPath).Directory.FullName
    $report = [IO.Path]::GetFullPath($ReportPath)
    if ($report.StartsWith($inputRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'report_must_be_outside_source_directory' }
    if ([IO.Path]::GetFileName($report).Contains(':')) { throw 'unsafe_report_path' }
    if (Test-Path -LiteralPath $report) { throw 'report_already_exists' }
    $parent = Get-Item -LiteralPath ([IO.Path]::GetDirectoryName($report))
    for ($node = $parent; $null -ne $node; $node = $node.Parent) {
        if ($node.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'unsafe_report_path' }
    }
    $started = [Diagnostics.Stopwatch]::GetTimestamp()
    $utcBefore = [datetimeoffset]::UtcNow.ToString('o')
    $recordValidation = $null
    if ($PSCmdlet.ParameterSetName -eq 'Record') {
        $recordInput = ReadEvidence $RecordPath 'collector_record'
        $inputs += $recordInput
        $recordValidation = & (Join-Path $PSScriptRoot 'Test-Mt5CollectorRecord.ps1') -Path $RecordPath
        $record = $utf8.GetString($recordInput.bytes) | ConvertFrom-Json
        if ($null -eq $record.probe_file) { throw 'record_has_no_timing_probe' }
        if ($null -ne $record.previous_record) { $inputs += ReadEvidence (Join-Path $inputRoot $record.previous_record) 'predecessor_record' }
        $ProbePath = Join-Path $inputRoot $record.probe_file
    }
    $probeInput = ReadEvidence $ProbePath 'probe'
    $inputs += $probeInput
    $probeValidation = & (Join-Path $PSScriptRoot 'Test-Mt5ProbeArtifact.ps1') -Path $ProbePath
    $probe = $utf8.GetString($probeInput.bytes) | ConvertFrom-Json

    $sourceQuery = Invoke-Mt5BoundedTimeQuery '/query /source' $QueryTimeoutMilliseconds
    $statusQuery = Invoke-Mt5BoundedTimeQuery '/query /status /verbose' $QueryTimeoutMilliseconds
    $strip = Invoke-Mt5BoundedTimeQuery "/stripchart /computer:$ReferenceHost /samples:$ReferenceSamples /period:1 /rdtsc /dataonly" $QueryTimeoutMilliseconds
    $reference = Get-Mt5StripchartEvidence $strip.raw $strip.exit_code $strip.timed_out $ReferenceHost
    $hostEvidence = [pscustomobject]@{
        utc_before_candidate = $utcBefore; utc_after_candidate = [datetimeoffset]::UtcNow.ToString('o')
        utc_clock_provenance = 'DateTimeOffset.UtcNow_computer_clock_unverified'
        monotonic_started = $started; monotonic_finished = [Diagnostics.Stopwatch]::GetTimestamp(); monotonic_frequency = [Diagnostics.Stopwatch]::Frequency
        source_query = $sourceQuery; status_query = $statusQuery; stripchart = $strip; reference = $reference
    }
    $policySources = @(
        'https://help.oanda.com/bvi/en/faqs/mt5-user-guide-bvi.htm',
        'https://help.oanda.com/bvi/es/faqs/oanda-mobile-user-guide-bvi.htm'
    )
    $policyInputs = @(); $policies = @()
    foreach ($url in $policySources) {
        $retrieved = [datetimeoffset]::UtcNow.ToString('o')
        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10 -MaximumRedirection 3
            if ($response.BaseResponse.ResponseUri.Scheme -ne 'https' -or $response.BaseResponse.ResponseUri.Host -ne 'help.oanda.com') { throw 'unexpected_policy_redirect' }
            $html = [string]$response.Content
            $bytes = $utf8.GetBytes($html)
            if ($bytes.Length -gt 4194304) { throw 'policy_input_bound' }
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $digest = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '').ToLowerInvariant() } finally { $sha.Dispose() }
            $policies += Get-Mt5PolicyEvidence $url $html $retrieved $digest
            $policyInputs += [pscustomobject]@{ name = "policy-$($policyInputs.Count + 1).html"; bytes = $bytes; sha256 = $digest; url = $url }
        } catch {
            $policies += [pscustomobject]@{ url = $url; retrieved_at_host_utc_candidate = $retrieved; classification = 'data_unavailable'; diagnostic = 'policy_retrieval_failed' }
        }
    }
    $assessment = Get-Mt5NativeUtcAssessment $probe $hostEvidence $policies
    $bundle = $report + '.inputs'
    if (Test-Path -LiteralPath $bundle) { throw 'evidence_bundle_already_exists' }
    [IO.Directory]::CreateDirectory($bundle) | Out-Null
    foreach ($inputEvidence in $inputs) { Write-Mt5EvidenceBytes (Join-Path $bundle $inputEvidence.file_name) $inputEvidence.bytes }
    foreach ($policyInput in $policyInputs) { Write-Mt5EvidenceBytes (Join-Path $bundle $policyInput.name) $policyInput.bytes }
    $artifact = [pscustomobject][ordered]@{
        schema = 'moneyway.mt5-native-utc-evidence'; version = 1; trace_id = [guid]::NewGuid().ToString('N')
        method = 'existing_immutable_probe_plus_new_bounded_host_reference'; input_capture_relationship = 'unverified_noncontemporaneous'
        inputs = @($inputs | Select-Object role,file_name,source_path,sha256,read_started_monotonic,read_finished_monotonic,read_utc_before_candidate,read_utc_after_candidate)
        tool_sources = @(@($PSCommandPath, (Join-Path $PSScriptRoot 'Mt5NativeUtcEvidence.ps1')) | ForEach-Object {
            [pscustomobject]@{ file_name = [IO.Path]::GetFileName($_); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
        })
        policy_inputs = @($policyInputs | Select-Object name,sha256,url)
        input_validation = [pscustomobject]@{ record_contract_valid = $(if ($null -ne $recordValidation) { $recordValidation.ContractValid } else { $null })
            probe_validator = 'Test-Mt5ProbeArtifact.ps1'; probe_contract_valid = $probeValidation.ContractValid
            complete_historical_chain_authenticated = $false }
        policy_input_representation = 'UTF8_reencoding_of_InvokeWebRequest_decoded_content'
        host_evidence = $hostEvidence; assessment = $assessment
    }
    $json = $artifact | ConvertTo-Json -Depth 18
    $pending = $report + '.pending-' + [guid]::NewGuid().ToString('N')
    Write-Mt5EvidenceBytes $pending ($utf8.GetBytes($json + "`n"))
    [IO.File]::Move($pending, $report)
    [pscustomobject]@{ ReportPath = $report; Classification = $assessment.classification; IndependentReferenceStatus = $reference.status
        QuoteFreshness = $assessment.broker_native.quote_freshness; CaptureRelationship = $assessment.broker_to_independent_utc_relationship
        CanonicalUtcConversionAuthorized = $false }
} finally {
    foreach ($handle in $handles) { $handle.Dispose() }
}
