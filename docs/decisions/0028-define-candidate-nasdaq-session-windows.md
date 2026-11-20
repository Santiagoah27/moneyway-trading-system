# ADR 0028: Explicit candidate Nasdaq Asia and London windows

Accepted by direct user configuration on 2026-11-19, external task attachment
`88027fbe-59e6-479e-ba0b-1dc5067d91a2`. No original mentor recording was independently
verified and no screenshot/media hashes are invented.

For America/Bogota operating calendar date D, Asia is [D-1 17:00, D 02:00)
and London is [D 02:00, D 08:00). These are strategy-specific windows, not universal
exchange schedules. No New York seasonal shift, holiday/weekend exception or
partial-candle interpolation is introduced. A candle must open inside and have
its entire closed interval contained in the window; completeness requires every
expected interval. Equal highs/lows retain all source identities.

The published baseline and `nasdaq-0.2.0-sc-candidate` retain their existing London
end of 07:00. Earlier unresolved contextual-research history remains unchanged.
New opt-in `nasdaq-0.3.0-session-candidate` derives from the SC candidate, preserving
its strict-close behavior and every other rule/dependency. Only NQ-LIQ-001 session
configuration changes. This is not activation or a new rule/evaluator count.
Lifecycle remains candidate -> independent backtest -> human approval -> activation.

The existing audited specification and calculator select exact 1H, not 60m or an
automatically selected smaller timeframe. Keep that authority. Smaller source
timeframes may be compared explicitly as documentary calculations, without
substitution for incomplete H1 or merging provider/instrument sources.

Final Asia/London references require complete observable candles and ended
windows. Session end or native candle close does not establish authenticated
publication, provider finality or temporal admission. Existing canonical replay
and MT5 admission safeguards remain unchanged. Automatic extrema do not approve
liquidity relevance, takes, H4, SC, M1, entry or Ready. Human validation of those
independent decisions remains required.

See the [candidate specification](../strategies/nasdaq/explicit-session-candidate.md).
The operational-window conflict (runtime 11:00 versus user-confirmed 11:30) is
explicitly outside this decision; neither strategy version's cutoff changes.
