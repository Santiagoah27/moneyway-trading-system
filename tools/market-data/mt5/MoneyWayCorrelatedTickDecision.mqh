// Pure decisions shared with the synthetic host tests. Terminal APIs stay in the EA.
bool TickNonceEligible(bool ready_exists, bool request_exists, bool response_exists, bool cancelled)
{
    return !ready_exists && !request_exists && !response_exists && !cancelled;
}

int TickStopReason(bool cancelled, bool source_safe, bool connected, long elapsed_ms,
    long session_limit_ms, bool capture_armed, long armed_elapsed_ms, long request_limit_ms)
{
    if(cancelled) return 1;
    if(elapsed_ms >= session_limit_ms) return 2;
    if(!source_safe) return 3;
    if(!connected) return 4;
    if(capture_armed && armed_elapsed_ms >= request_limit_ms) return 5;
    return 0;
}

bool TickProgressed(bool capture_armed, long callbacks, long callbacks_when_armed,
    long latest_msc, long baseline_msc)
{
    return capture_armed && callbacks > callbacks_when_armed && baseline_msc > 0 && latest_msc > baseline_msc;
}
