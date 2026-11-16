Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Mt5NtsSelectionEvidence.ps1')

function Get-Mt5NtsPrintedEndpoint([string]$Text) {
    # Scientific %10.3e has four significant digits. Preserve signed zero as text.
    if ($Text -cnotmatch '^(?<sign>-?)(?<leading>[0-9])\.[0-9]{3}e(?<exponent>[+-](?:[0-9]{2}|[1-9][0-9]{2}))$') {
        throw 'unsupported_selection_print_format'
    }
    $negativeZero = $Matches['sign'] -ceq '-' -and $Text.StartsWith('-0.000e')
    $leading = $Matches['leading']
    $exponent = [int]$Matches['exponent']
    $value = ConvertFrom-Mt5NtsSelectionNumber $Text
    if ($value -eq 0) {
        if ($Text -cnotmatch '^-?0\.000e\+00$') { throw 'noncanonical_scientific_zero' }
        return [pscustomobject]@{ original = $Text; printed_decimal = [decimal]0; negative_zero = $negativeZero
            quantum_seconds = $null; formatting_lower_seconds = [decimal]0; formatting_upper_seconds = [decimal]0
            nearest_lower_seconds = [decimal]0; nearest_upper_seconds = [decimal]0 }
    }
    if ($leading -ceq '0') { throw 'nonnormalized_scientific_endpoint' }
    # Decimal.Parse and checked Decimal operations reject values whose enclosure cannot be represented exactly.
    $quantum = ConvertFrom-Mt5NtsSelectionNumber ('1e' + ($exponent - 3).ToString([Globalization.CultureInfo]::InvariantCulture))
    $halfQuantum = $quantum / [decimal]2
    if ($halfQuantum -eq 0 -or $halfQuantum * [decimal]2 -ne $quantum) { throw 'formatting_quantum_not_exactly_representable' }
    [pscustomobject]@{ original = $Text; printed_decimal = $value; negative_zero = $false; quantum_seconds = $quantum
        formatting_lower_seconds = $value - $quantum; formatting_upper_seconds = $value + $quantum
        nearest_lower_seconds = $value - $halfQuantum; nearest_upper_seconds = $value + $halfQuantum }
}

function Get-Mt5NtsSelectionSemantics($VerifiedEvidence, $CaptureReport) {
    $result = [pscustomobject][ordered]@{
        schema = 'moneyway.nts-selection-interval-semantics'; version = 1
        contract_id = 'chrony_4.5_Ubuntu_4.5-1ubuntu4.2_selection_v1'; status = 'data_unavailable'
        correlation_id = $null; source_address = $null; chrony_version = '4.5-1ubuntu4.2'
        sign_interpretation = $null; clock_domain = $null; epoch = $null; selected_row = $null
        printed_interval = $null; formatting_aware_interval = $null; nearest_rounding_candidate = $null
        underlying_model_estimate = 'internal_binary_values_and_best_sample_parameters_not_retained'
        selection_state_sequence = @(); later_states = @(); nts_authenticated_measurement_observed = $false
        selected_source_authenticated = $false; diagnostics = @(); assumptions = @(); unresolved = @()
        temporal_propagation_authorized = $false; timestamp_correction_authorized = $false
        overall_utc_uncertainty = 'unresolved'; windows_host_utc_error_bound_seconds = $null; mt5_event_utc_interval = $null
        windows_clock_authenticated = $false; mt5_native_utc_authenticated = $false
        canonical_utc_conversion_authorized = $false; utc_conversion_authenticated = $false; canonical_ready = $false
    }
    try {
        if ($VerifiedEvidence.status -cne 'passed') { throw 'selection_evidence_integrity_unverified' }
        $selection = $VerifiedEvidence.assessment
        $result.nts_authenticated_measurement_observed = $selection.nts_authenticated_measurement_observed
        $result.selected_source_authenticated = $selection.selected_source_authenticated
        if ($CaptureReport.schema -cne 'moneyway.windows-wsl-nts-correlation' -or $CaptureReport.version -ne 1 -or
            $CaptureReport.correlation_id -cne $selection.correlation_id -or
            $CaptureReport.capture.session -cne $selection.correlation_id) { throw 'selection_semantics_session_mismatch' }
        if ($CaptureReport.client_profile -cne 'Ubuntu_chrony_4.5-1ubuntu4.2_executable_hash_pinned' -or
            $CaptureReport.reference_server -cne 'time.cloudflare.com') { throw 'selection_semantics_profile_unreviewed' }
        if ($selection.gate -cne 'G_authenticated_selected_interval' -or -not $selection.selected_source_authenticated) {
            throw 'authenticated_selected_interval_unavailable'
        }
        $result.correlation_id = $selection.correlation_id
        $result.source_address = $selection.selected_row.source_address
        $result.selected_row = $selection.selected_row
        # Read every state from the validated full log embedded in the capture, including rows after the selected event.
        $text = (New-Object Text.UTF8Encoding($false,$true)).GetString([Convert]::FromBase64String($CaptureReport.capture.result.selection.raw_base64))
        $selectedIndex = -1
        foreach ($line in ($text -split '\r?\n')) {
            if ($line -match '^\d{4}-\d{2}-\d{2} ') {
                $parts = $line.Trim() -split '\s+'
                $result.selection_state_sequence += $parts[3]
                if ($parts[3] -ceq '*') { $selectedIndex = $result.selection_state_sequence.Count - 1 }
            }
        }
        if ($selectedIndex -lt 0) { throw 'selected_row_missing_from_validated_log' }
        if ($selectedIndex + 1 -lt $result.selection_state_sequence.Count) {
            $result.later_states = @($result.selection_state_sequence[($selectedIndex + 1)..($result.selection_state_sequence.Count - 1)])
        }
        $lower = Get-Mt5NtsPrintedEndpoint $selection.selected_row.lower_original
        $upper = Get-Mt5NtsPrintedEndpoint $selection.selected_row.upper_original
        if ($lower.printed_decimal -gt $upper.printed_decimal) { throw 'reversed_printed_interval' }
        $result.sign_interpretation = [pscustomobject]@{
            status = 'confirmed'; quantity = 'Chrony_cooked_local_clock_minus_reference'
            positive = 'local_clock_ahead_of_reference'; negative = 'local_clock_behind_reference'
            NTP_sample_offset = 'reference_minus_local_before_SST_negation'
            corrective_adjustment_sign = 'opposite_of_local_minus_reference'
            final_Q_output = 'negative_of_reference_update_offset_estimate_with_six_decimal_formatting'
            Q_and_selection_use_identical_estimator = $false
        }
        $result.clock_domain = [pscustomobject]@{
            status = 'confirmed'; local = 'Chrony_cooked_Linux_CLOCK_REALTIME'; reference = 'selected_NTS_source'
            Q_driver = 'SYS_Null'; cooked_time = 'raw_realtime_plus_driver_correction'
            captured_configuration_correction = 'zero_null_driver_initial_correction_no_driftfile_no_normal_reference_update'
            Windows_Stopwatch_domain = 'separate'; Linux_boottime_domain = 'separate'; cross_domain_counter_subtraction_permitted = $false
        }
        $result.epoch = [pscustomobject]@{
            status = 'context_specific'; evaluation = 'cached_cooked_scheduler_event_time'
            sample_epoch = 'local_midpoint_for_NTP_sample_best_single_sample_selected_by_SST'
            extrapolation = 'stored_offset_plus_abs_evaluation_minus_best_sample_time_times_estimated_frequency'
            root_distance = 'half_sample_root_delay_plus_sample_root_dispersion_plus_elapsed_times_skew'
            printed_timestamp = $selection.selected_row.source_timestamp_label
            printed_timestamp_precision_seconds = 1; printing = 'gmtime_scheduler_event_tv_sec_fraction_omitted'
            exact_subsecond_epoch = $null; best_sample_timestamp = $null
            displayed_sample_age = 'newest_sample_age_not_necessarily_best_sample_age'
        }
        $result.printed_interval = [pscustomobject]@{ lower_original = $lower.original; upper_original = $upper.original
            lower_seconds = $lower.printed_decimal; upper_seconds = $upper.printed_decimal }
        $result.formatting_aware_interval = [pscustomobject]@{
            status = 'context_specific'; lower_seconds = $lower.formatting_lower_seconds; upper_seconds = $upper.formatting_upper_seconds
            scope = 'conservative_enclosure_of_internal_model_endpoints_under_reviewed_standard_C_e_formatting'
            endpoint_details = @($lower,$upper); significant_digits = 4; fraction_digits = 3
            rounding = 'union_of_nearest_upward_downward_toward_zero_closed_one_quantum_enclosure'
            locale = 'C_startup_locale_no_setlocale_calls_in_reviewed_Chrony_sources'
            historical_rounding_mode = 'unresolved'; historical_libc_binary_hash = $null
            model_or_UTC_error_bound_established = $false
        }
        $result.nearest_rounding_candidate = [pscustomobject]@{ status = 'candidate'
            lower_seconds = $lower.nearest_lower_seconds; upper_seconds = $upper.nearest_upper_seconds
            required_assumption = 'historical_formatter_used_FE_TONEAREST'; assumption_verified = $false }
        $result.assumptions = @($selection.assumptions) + @('reviewed_standard_C_formatter_contract_applies_to_historical_runtime')
        $result.unresolved = @('historical_libc_bytes_and_fenv_not_recorded','internal_double_endpoints_not_recoverable',
            'exact_best_sample_and_selection_subsecond_epochs_not_retained','ultimate_reference_accuracy',
            'clock_rate_and_HyperV_step_bounds_for_propagation','Windows_and_MT5_event_binding',
            'provider_historical_timezone_DST_closure_and_availability')
        $result.status = 'context_specific'
    } catch { $result.diagnostics += $_.Exception.Message }
    $result
}
