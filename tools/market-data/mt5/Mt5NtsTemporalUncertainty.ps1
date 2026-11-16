Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Mt5NtsCorrelation.ps1')

function Read-Mt5NtsUncertaintyEvidence {
    param([string]$CapturePath, [string]$ExpectedCaptureSha256, [string]$ReassessmentPath = '', [string]$ExpectedReassessmentSha256 = '')
    $state = [pscustomobject]@{ status = 'data_unavailable'; capture = $null; source_sha256 = $null; raw_fields_verified = 0
        reassessment = $null; reassessment_sha256 = $null; reassessment_status = 'data_unavailable'; diagnostics = @() }
    try {
        if ($ExpectedCaptureSha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'explicit_source_hash_required' }
        $source = Read-Mt5TickFile $CapturePath -Json
        $state.source_sha256 = $source.sha256
        if ($source.sha256 -cne $ExpectedCaptureSha256.ToLowerInvariant()) { throw 'source_hash_mismatch' }
        $data = $source.data
        if ($data.schema -cne 'moneyway.windows-wsl-nts-correlation' -or $data.version -ne 1 -or
            $data.correlation_id -cnotmatch '^[0-9a-f]{64}$' -or $data.capture.session -cne $data.correlation_id) { throw 'source_schema_or_session_mismatch' }
        $fields = @('configuration','authdata','ntpdata','capabilities','client_stderr','client_stdout')
        if ($data.retained_evidence_hashes.Count -ne $fields.Count) { throw 'incomplete_raw_field_provenance' }
        foreach ($field in $fields) {
            $pin = @($data.retained_evidence_hashes | Where-Object { $_.field -ceq $field })
            if ($pin.Count -ne 1 -or $data.capture.result.$field -isnot [string] -or
                $pin[0].sha256 -cne (Get-Mt5TickHash ([Text.Encoding]::UTF8.GetBytes($data.capture.result.$field)))) { throw 'raw_field_hash_mismatch' }
            $state.raw_fields_verified++
        }
        $state.capture = $data; $state.status = 'passed'
    } catch { $state.diagnostics += $_.Exception.Message; return $state }
    try {
        if ([string]::IsNullOrWhiteSpace($ReassessmentPath) -or -not (Test-Path -LiteralPath $ReassessmentPath)) { throw 'reassessment_unavailable' }
        if ($ExpectedReassessmentSha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'explicit_reassessment_hash_required' }
        $review = Read-Mt5TickFile $ReassessmentPath -Json
        $state.reassessment_sha256 = $review.sha256
        if ($review.sha256 -cne $ExpectedReassessmentSha256.ToLowerInvariant()) { throw 'reassessment_hash_mismatch' }
        if ($review.data.schema -cne 'moneyway.windows-wsl-nts-correlation-reassessment' -or $review.data.version -ne 1 -or
            $review.data.source_report_sha256 -cne $state.source_sha256 -or $review.data.correlation_id -cne $state.capture.correlation_id) { throw 'reassessment_source_binding_mismatch' }
        $state.reassessment = $review.data; $state.reassessment_status = 'passed'
    } catch { $state.diagnostics += $_.Exception.Message }
    $state
}

function Get-Mt5NtsIntervalIntersection([decimal]$FirstLower, [decimal]$FirstUpper, [decimal]$SecondLower, [decimal]$SecondUpper) {
    if ($FirstLower -gt $FirstUpper -or $SecondLower -gt $SecondUpper) { throw 'reversed_interval_endpoints' }
    $lower = [math]::Max($FirstLower, $SecondLower); $upper = [math]::Min($FirstUpper, $SecondUpper)
    $empty = $lower -gt $upper
    [pscustomobject]@{ empty = $empty; lower_seconds = $(if (-not $empty) { $lower }); upper_seconds = $(if (-not $empty) { $upper }) }
}

function Get-Mt5ChronyConditionalErrorBound($Tracking) {
    # This is a conditional arithmetic contract. The real capture has no tracking input.
    $missing = @()
    foreach ($name in @('source','system_time_offset_seconds','root_delay_seconds','root_dispersion_seconds',
        'leap_status','reference_time','observation_id','assumptions')) {
        if ($null -eq $Tracking -or $null -eq $Tracking.PSObject.Properties[$name] -or $null -eq $Tracking.$name) { $missing += $name }
    }
    if ($missing.Count -eq 0) {
        if ($Tracking.source -cne 'chronyc_tracking_4.5') { $missing += 'tracking_field_provenance_required' }
        if ($Tracking.leap_status -notin @('Normal','Insert second','Delete second')) { $missing += 'synchronized_tracking_state' }
        if ([string]::IsNullOrWhiteSpace($Tracking.reference_time) -or [string]::IsNullOrWhiteSpace($Tracking.observation_id)) { $missing += 'tracking_observation_binding' }
        foreach ($name in @('ultimate_reference_correct','same_observation_and_clock','mode_semantics_reviewed','applies_only_at_observation')) {
            if ($null -eq $Tracking.assumptions.PSObject.Properties[$name] -or $Tracking.assumptions.$name -ne $true) { $missing += $name }
        }
    }
    $bound = $null
    if ($missing.Count -eq 0) {
        try {
            $systemOffset = Get-Mt5NtsDecimal $Tracking.system_time_offset_seconds
            $delay = Get-Mt5NtsDecimal $Tracking.root_delay_seconds
            $dispersion = Get-Mt5NtsDecimal $Tracking.root_dispersion_seconds
            if ($delay -lt 0 -or $dispersion -lt 0) { throw 'negative_uncertainty_component' }
            $bound = [math]::Abs($systemOffset) + $dispersion + $delay / [decimal]2
        } catch { $missing += $_.Exception.Message }
    }
    [pscustomobject]@{ status = $(if ($null -eq $bound) { 'data_unavailable' } else { 'candidate' })
        formula = 'abs(system_time_offset) + root_dispersion + root_delay / 2'
        conditional_bound_seconds = $bound; unverified_or_missing_prerequisites = $missing
        scope = 'Linux_system_clock_at_the_supplied_tracking_observation_under_explicit_assumptions'
        unconditional_utc_bound_established = $false; assumptions_independently_verified = $false }
}

function Get-Mt5NtsPacketField([string]$Text, [string]$Label) {
    $match = [regex]::Match($Text, ('(?m)^' + [regex]::Escape($Label) + '\s+: ([^\r\n]+)'))
    if ($match.Success) { return $match.Groups[1].Value.Trim() }
    return $null
}

function Get-Mt5NtsClockInventory {
    @(
        [pscustomobject]@{ domain = 'windows_wall_clock'; api = 'DateTimeOffset.UtcNow'; coordinate = 'UTC_candidate'
            representation = '100_ns_ticks_not_accuracy'; order = 'observed_endpoints_only'; overhead = 'API_read_and_host_scheduling'
            can_change = 'yes_Windows_or_underlying_clock_controls'; authentication = 'unresolved'; role = 'candidate_coordinates' },
        [pscustomobject]@{ domain = 'windows_stopwatch'; api = 'Stopwatch.GetTimestamp/GetFrequency'; coordinate = 'local_monotonic'
            representation = '1/frequency_seconds_per_tick'; order = 'same_domain_recorded_order'; overhead = 'counter_read_and_process_scheduling'
            can_change = 'elapsed_rate_accuracy_unmeasured'; authentication = 'unresolved'; role = 'local_elapsed_coordinates' },
        [pscustomobject]@{ domain = 'linux_wall_clock'; api = 'GNU_date_%s.%N'; coordinate = 'Unix_UTC_candidate'
            representation = '9_decimal_places_not_accuracy'; order = 'between_private_request_and_response'
            overhead = 'date_process_startup_read_and_transport'; can_change = 'yes_HyperV_implicit_sync'; authentication = 'unresolved'; role = 'instantaneous_candidate_reading' },
        [pscustomobject]@{ domain = 'linux_boottime'; api = '/proc/uptime_CLOCK_BOOTTIME'; coordinate = 'local_monotonic_including_suspend'
            representation = '0.01_second_truncation'; order = 'brackets_date_read_in_same_Linux_process_protocol'
            overhead = 'proc_reads_and_shell_scheduling'; can_change = 'includes_suspend_rate_not_calibrated_to_UTC'; authentication = 'unresolved'; role = 'quantized_elapsed_constraint' },
        [pscustomobject]@{ domain = 'chrony_reference'; api = 'chronyd_-Q_authdata_ntpdata'; coordinate = 'estimated_source_to_Linux_offset'
            representation = 'Q_print_6_decimals_packet_offset_9_root_fields_6_not_error_bounds'; order = 'same_private_query_instance'
            overhead = 'network_client_estimator_and_monitoring'; can_change = 'reference_and_local_clock_can_evolve'; authentication = 'client_verified_NTS'; role = 'authenticated_packet_and_offset_estimate' },
        [pscustomobject]@{ domain = 'host_guest_exchange'; api = 'nonce_bound_BEFORE_AFTER_stream_protocol'; coordinate = 'two_local_clock_domains'
            representation = 'source_representation_resolution'; order = 'request_Linux_read_response_under_reviewed_protocol'
            overhead = 'full_request_response_transport_and_scheduling'; can_change = 'wall_clocks_can_step_between_reads'; authentication = 'local_protocol_binding_not_clock_authentication'; role = 'conditional_cross_clock_interval' },
        [pscustomobject]@{ domain = 'WSL_lifecycle'; api = 'Windows_staging_launch_ready_completion'; coordinate = 'Windows_local_observations'
            representation = 'Stopwatch_ticks_and_UtcNow_candidates'; order = 'staging_before_capture_ready_before_query'
            overhead = 'initialization_transfer_process_setup_and_cleanup_not_separately_identified'; can_change = 'yes_during_execution'; authentication = 'unresolved'; role = 'elapsed_intervals_shutdown_not_observed' },
        [pscustomobject]@{ domain = 'HyperV_implicit_sync'; api = 'preexisting_platform_behavior'; coordinate = 'no_sync_event_telemetry_captured'
            representation = 'unresolved'; order = 'no_sync_event_order_recorded'; overhead = 'unresolved'
            can_change = 'can_adjust_guest_clock'; authentication = 'unresolved'; role = 'clocks_not_independent_no_equality_or_drift_guarantee' }
    )
}

function Get-Mt5NtsLocalCoordinateOrder($Capture) {
    $order = [pscustomobject]@{ host_monotonic_status = 'data_unavailable'; linux_monotonic_status = 'data_unavailable'
        windows_execution_seconds = $null; linux_boottime_elapsed_seconds = $null; diagnostics = @() }
    try {
        $frequency = [decimal]$Capture.launch.stopwatch_frequency
        $previous = [long]$Capture.initialization_before.stopwatch_ticks
        foreach ($stamp in @($Capture.initialization_before,$Capture.initialization_after,$Capture.launch,$Capture.ready,
            $Capture.before.sent,$Capture.before.received,$Capture.after.sent,$Capture.after.received,$Capture.completion)) {
            if ($stamp.monotonic_domain -cne 'windows_stopwatch' -or $frequency -le 0 -or
                [decimal]$stamp.stopwatch_frequency -ne $frequency -or [long]$stamp.stopwatch_ticks -lt $previous) { throw 'host_monotonic_domain_or_order_mismatch' }
            $previous = [long]$stamp.stopwatch_ticks
        }
        $order.windows_execution_seconds = ([decimal]$Capture.completion.stopwatch_ticks - [decimal]$Capture.launch.stopwatch_ticks) / $frequency
        $order.host_monotonic_status = 'passed'
    } catch { $order.diagnostics += $_.Exception.Message }
    try {
        $previous = [decimal]0
        foreach ($exchange in @($Capture.before,$Capture.after)) {
            if ($exchange.linux.monotonic_domain -cne 'linux_proc_uptime_boottime') { throw 'linux_clock_domain_mismatch' }
            foreach ($value in @($exchange.linux.monotonic_before,$exchange.linux.monotonic_after)) {
                if ($value -cnotmatch '^[0-9]+\.[0-9]{2}$') { throw 'unsupported_linux_monotonic_resolution' }
                $coordinate = Get-Mt5NtsDecimal $value
                if ($coordinate -lt $previous) { throw 'linux_monotonic_order_mismatch' }
                $previous = $coordinate
            }
        }
        $order.linux_boottime_elapsed_seconds = @(((Get-Mt5NtsDecimal $Capture.after.linux.monotonic_before) - (Get-Mt5NtsDecimal $Capture.before.linux.monotonic_after) - [decimal]'0.01'),
            ((Get-Mt5NtsDecimal $Capture.after.linux.monotonic_after) - (Get-Mt5NtsDecimal $Capture.before.linux.monotonic_before) + [decimal]'0.01'))
        $order.linux_boottime_elapsed_seconds[0] = [math]::Max([decimal]0, $order.linux_boottime_elapsed_seconds[0])
        $order.linux_monotonic_status = 'passed'
    } catch { $order.diagnostics += $_.Exception.Message }
    $order
}

function Get-Mt5NtsTemporalUncertainty($Evidence) {
    $result = [pscustomobject][ordered]@{
        schema = 'moneyway.nts-temporal-uncertainty'; version = 1; method = 'offline_interval_and_field_provenance_audit_v1'
        source_sha256 = $Evidence.source_sha256; correlation_id = $null; status = 'data_unavailable'
        input_integrity_status = $Evidence.status; reassessment_status = $Evidence.reassessment_status; diagnostics = @($Evidence.diagnostics)
        clock_domains = @(Get-Mt5NtsClockInventory); recorded_inputs = $null; linux_nts_authentication = $null
        ordering = $null; conditional_cross_clock_intervals = @(); constant_offset_hypothesis = $null
        local_elapsed_constraints = $null; chrony_field_inventory = @(); chrony_tracking_formula = Get-Mt5ChronyConditionalErrorBound $null
        contradictions = @(); temporal_findings = @(); missing_evidence = @(); human_review_requirements = @()
        defensible_constraint_scope = 'validated_recorded_local_coordinates_and_quantization_only'
        overall_utc_uncertainty = 'unresolved'
        windows_host_utc_error_bound_seconds = $null; mt5_event_utc_interval = $null
        windows_clock_authenticated = $false; mt5_native_utc_authenticated = $false
        canonical_utc_conversion_authorized = $false; utc_conversion_authenticated = $false; canonical_ready = $false
    }
    if ($Evidence.status -cne 'passed') { return $result }
    $source = $Evidence.capture; $capture = $source.capture; $sid = $source.correlation_id
    $result.correlation_id = $sid
    # Re-evaluate retained raw inputs; do not trust an earlier summary's numerical conclusions.
    $assessment = Get-Mt5NtsCorrelationAssessment $capture $sid
    $result.status = 'unresolved'
    $result.linux_nts_authentication = [pscustomobject]@{ nts_authenticated_measurement_observed = $assessment.nts_authenticated_measurement_observed
        nts_ke_observed = $assessment.nts_ke_observed; accepted_authenticated_packets = $assessment.accepted_authenticated_packets
        scope = 'recorded_Linux_client_measurement_only_not_a_new_NTS_exchange' }
    $result.diagnostics += $assessment.diagnostics
    $localOrder = Get-Mt5NtsLocalCoordinateOrder $capture
    $hostObservations = @($capture.initialization_before,$capture.initialization_after,$capture.launch,$capture.ready,
        $capture.before.sent,$capture.before.received,$capture.after.sent,$capture.after.received,$capture.completion)
    $result.recorded_inputs = [pscustomobject]@{ windows_observations = $hostObservations
        linux_observations = @($capture.before.linux,$capture.after.linux); final_Q_offset_seconds = $assessment.chrony_final_offset_seconds
        last_monitored_packet_metrics_seconds = $assessment.packet_metrics_seconds
        packet_leap_status = Get-Mt5NtsPacketField $capture.result.ntpdata 'Leap status'
        packet_reference_time = Get-Mt5NtsPacketField $capture.result.ntpdata 'Reference time'
        packet_precision = Get-Mt5NtsPacketField $capture.result.ntpdata 'Precision'
        raw_evidence_references = @($source.retained_evidence_hashes)
        reference_server = $source.reference_server; client_profile = $source.client_profile }
    $timing = $assessment.timing
    $result.ordering = [pscustomobject]@{ status = $(if ($localOrder.host_monotonic_status -ceq 'passed' -and $localOrder.linux_monotonic_status -ceq 'passed') { 'passed' } else { 'data_unavailable' })
        host_monotonic_status = $localOrder.host_monotonic_status; linux_monotonic_status = $localOrder.linux_monotonic_status
        constraint_scope = 'recorded_local_monotonic_and_endpoint_realtime_order'
        protocol_causality = 'supported_by_reviewed_capture_procedure_and_session_bindings'
        between_sample_no_step_guarantee = 'unresolved'; cross_domain_monotonic_subtraction_permitted = $false }
    $result.local_elapsed_constraints = [pscustomobject]@{ windows_capture_execution_seconds = $localOrder.windows_execution_seconds
        linux_boottime_elapsed_seconds = $localOrder.linux_boottime_elapsed_seconds
        scope = 'local_counter_coordinates_available_independently_of_wall_clock_fields'; UTC_rate_calibration_or_drift_bound_established = $false }
    if ($null -ne $timing) {
        $conditions = @('capture_procedure_and_session_binding_are_trusted',
            'Windows_wall_clock_is_nondecreasing_through_each_exchange',
            'captured_read_values_represent_the_documented_local_API_observations')
        foreach ($index in 0..1) {
            $exchange = $timing.exchanges[$index]
            $result.conditional_cross_clock_intervals += [pscustomobject]@{ phase = @('BEFORE','AFTER')[$index]; status = 'candidate'
                lower_seconds = $exchange.linux_minus_windows_candidate_interval_seconds[0]
                upper_seconds = $exchange.linux_minus_windows_candidate_interval_seconds[1]
                scope = 'Linux_minus_Windows_at_that_Linux_wall_read_only'; assumptions = $conditions
                assumptions_verified = $false; host_exchange_elapsed_seconds = $exchange.host_elapsed_seconds
                realtime_minus_counter_elapsed_seconds = $exchange.realtime_minus_monotonic_elapsed_seconds
                rigorous_global_utc_bound = $false }
        }
        $first = $result.conditional_cross_clock_intervals[0]; $second = $result.conditional_cross_clock_intervals[1]
        $intersection = Get-Mt5NtsIntervalIntersection $first.lower_seconds $first.upper_seconds $second.lower_seconds $second.upper_seconds
        $result.constant_offset_hypothesis = [pscustomobject]@{ status = $(if ($intersection.empty) { 'failed' } else { 'candidate' })
            feasible_under_assumptions = -not $intersection.empty; intersection = $intersection
            additional_assumption = 'Linux_minus_Windows_offset_constant_between_both_read_instants'
            additional_assumption_verified = $false; future_or_intermediate_event_propagation_authorized = $false }
        $result.local_elapsed_constraints = [pscustomobject]@{ windows_initialization_and_staging_seconds = $timing.initialization_and_staging_seconds
            windows_launch_to_ready_seconds = $timing.launch_to_ready_seconds; windows_capture_execution_seconds = $timing.host_execution_seconds
            linux_boottime_elapsed_seconds = $localOrder.linux_boottime_elapsed_seconds
            linux_boottime_quantization_seconds = '0.01'; linux_realtime_elapsed_seconds = $timing.linux_realtime_elapsed_seconds
            linux_unit_rate_no_step_hypothesis_feasible = $timing.linux_realtime_elapsed_seconds -ge $timing.linux_boottime_elapsed_bracket_seconds[0] -and
                $timing.linux_realtime_elapsed_seconds -le $timing.linux_boottime_elapsed_bracket_seconds[1]
            linux_unit_rate_no_step_hypothesis_verified = $false
            constraint_scope = 'local_recorded_coordinates_and_documented_boottime_truncation'
            UTC_rate_calibration_or_drift_bound_established = $false; WSL_shutdown_interval = $null }
    } else {
        $result.contradictions = @($localOrder.diagnostics)
        $result.temporal_findings = @($assessment.diagnostics | Where-Object { $_ -match 'discontinuity' })
    }
    foreach ($label in @('Offset','Peer delay','Peer dispersion','Root delay','Root dispersion')) {
        $raw = Get-Mt5NtsPacketField $capture.result.ntpdata $label
        $value = if ($null -ne $raw -and $raw -match '^([+-]?[0-9]+\.[0-9]+) seconds$') { $Matches[1] } else { $null }
        $result.chrony_field_inventory += [pscustomobject]@{ field = $label; value_seconds = $value
            present = $null -ne $value; provenance = 'capture.result.ntpdata'
            clock_scope = $(if ($label -in @('Root delay','Root dispersion')) { 'remote_response_NTP_fields' } else { 'last_monitored_source_measurement' }) }
    }
    foreach ($label in @('System time','Last offset','Root delay','Root dispersion','Ref time','Leap status','source selection')) {
        $result.chrony_field_inventory += [pscustomobject]@{ field = $label; value_seconds = $null; present = $false
            provenance = 'chronyc_tracking_or_source_selection_not_captured'; clock_scope = 'local_tracking_state_missing' }
    }
    $result.missing_evidence = @('local_tracking_System_time_and_same_epoch_root_parameters',
        'reviewed_uncertainty_contract_for_nonadjusting_Q_mode', 'local_tracking_or_selection_state_and_update_epoch',
        'ultimate_reference_correctness_not_demonstrated_by_NTS', 'no_step_and_rate_bounds_through_host_guest_exchanges',
        'bounded_evolution_between_NTS_estimate_and_wall_clock_reads', 'MT5_event_bound_to_this_session',
        'provider_historical_timezone_DST_mapping', 'provider_closure_and_historical_availability')
    $result.human_review_requirements = @('review_Q_mode_estimator_to_system_clock_uncertainty_semantics',
        'justify_reference_accuracy_assumptions_and_same_epoch_parameter_provenance',
        'establish_host_guest_clock_path_and_rate_constraints_before_interval_propagation')
    $result
}
