// Pure decision functions. No terminal, clock, filesystem or execution APIs.
const int COLLECTOR_WAIT = 0;
const int COLLECTOR_INITIAL = 1;
const int COLLECTOR_NEW_M1 = 2;
const int COLLECTOR_RECOVERY = 3;
const int COLLECTOR_UNAVAILABLE = 4;
const int COLLECTOR_REGRESSED = 5;
const int COLLECTOR_RETRY = 6;
const int COLLECTOR_STOP = 7;

int CollectorCondition(bool source_safe, bool connected, bool head_ready, long head, long last_head)
{
    if(!source_safe) return 4;
    if(!connected) return 1;
    if(!head_ready || head <= 0) return 2;
    if(last_head > 0 && head < last_head) return 3;
    return 0;
}

int CollectorAction(bool has_snapshot, int condition, int previous_condition,
    long head, long last_head, bool retry_due, bool incomplete)
{
    if(condition == 4) return COLLECTOR_STOP;
    if(previous_condition < 0) return COLLECTOR_INITIAL;
    if(condition == 3)
        return condition != previous_condition || retry_due ? COLLECTOR_REGRESSED : COLLECTOR_WAIT;
    if(condition != 0)
        return condition != previous_condition || retry_due ? COLLECTOR_UNAVAILABLE : COLLECTOR_WAIT;
    if(!has_snapshot) return COLLECTOR_INITIAL;
    if(head > last_head) return COLLECTOR_NEW_M1;
    if(previous_condition != 0) return COLLECTOR_RECOVERY;
    if(incomplete && retry_due) return COLLECTOR_RETRY;
    return COLLECTOR_WAIT;
}

bool CollectorAttemptAllowed(int action, bool retry_due, bool condition_changed, long head, long last_attempted_head)
{
    if(action == COLLECTOR_WAIT || action == COLLECTOR_STOP) return false;
    return retry_due || condition_changed || (action == COLLECTOR_NEW_M1 && head != last_attempted_head);
}

bool CollectorSequenceFree(long candidate, bool record_exists, bool probe_exists)
{
    return candidate > 0 && candidate <= 999999999999 && !record_exists && !probe_exists;
}

string CollectorEventName(int action)
{
    if(action == COLLECTOR_INITIAL) return "initial_snapshot";
    if(action == COLLECTOR_NEW_M1) return "new_native_m1";
    if(action == COLLECTOR_RECOVERY) return "source_recovery";
    if(action == COLLECTOR_UNAVAILABLE) return "source_unavailable";
    if(action == COLLECTOR_REGRESSED) return "native_head_regressed";
    return "incomplete_snapshot_retry";
}
