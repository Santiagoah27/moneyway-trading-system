# Nasdaq experimental M5 contextual confirmation V1

`EXP-NQ-M5-CONTEXTUAL-SEQUENCE-V1` / `1.0.0` composes the existing E1/E2/E3
penetration observations, contextual strict-close SC and both M5 FVG/IFVG
profiles. The source is the original H1/M5 documentary context at a shared
explicit cutoff. Nominal close timestamps do not authenticate historical
publication. The experiment's required directional FVG describes research
completion. In canonical Standard, an approved FVG is optional support, and
the audited runtime window ends at 11:00. The experiment uses the separately
requested 11:30 end. [ADR 0027](../decisions/0027-permit-isolated-experimental-strategy-hypotheses.md) · [Audited Nasdaq specification](../strategies/nasdaq/strategy-specification.md) · [Contextual SC](contextual-swing-sc-v1.md) · [M5 FVG and IFVG](nasdaq-m5-fvg-ifvg-candidate-v1.md)

## Explicit policies

- Each episode keeps its operating date, reference and existing E-policy
  identity. High-side penetration maps to bearish evidence and low-side
  penetration maps to bullish evidence under this reversal hypothesis.
  Incompatible evidence remains in diagnostics.
- `StrictAfterSweep` requires confirmation strictly after sweep availability.
  `SameCloseDiagnostic` additionally marks an ambiguous relationship at an
  equal close. Equality never authorizes that event as the first confirmation.
  A later eligible event can still be reported alongside the ambiguity.
- `PostSweepObservation` compares the chronology without the research window.
  `WindowEventOnly` requires both closes within `[08:30, 11:30)`
  America/Bogota. `WindowConfirmationWithPriorSweep` can use a prior
  penetration in the same episode and requires the confirmation close within
  that window. The clock is converted using the existing Nasdaq Bogotá zone.
  FVG completion is bounded by the same operating date and cutoff. These
  policies apply the window filter to sweep and confirmation, with no separate
  FVG window or cross-session carryover.
- SC uses its strict close availability. IFVG uses the inversion close and
  retains its original zone, even when formed before the sweep. The first
  eligible close retains every tied event, including ties between SC and IFVG
  and separate SC hypotheses. The report groups common SC candle evidence
  and exact IFVG identities associated with multiple episodes alongside their
  distinct event IDs.
- `FormationAfterConfirmation` requires a directional FVG close strictly
  after the trigger. `FormationSequenceAfterConfirmation` also requires C1
  to open at or after the trigger. Equal-close formations remain diagnostic.
  The originating inverted zone cannot serve as the subsequent FVG. All
  qualifying zones and every earlier-timestamp tie at the earliest eligible
  formation survive, including distinct trigger associations for one zone.
- An IFVG invalidated by the subsequent FVG close cannot support that branch.
  At equal invalidation and formation closes the trigger is already invalid
  at the decision boundary. A later invalidation preserves the historical
  completion. When the confirming FVG later inverts, record
  `FVGInvalidated` for its original directional role. SC supplies no
  invalidation rule. There is no restart or fallback to a later trigger after
  the first eligible trigger invalidates.
- Unknown source gaps between stages prevent eligibility. The existing
  SC coverage diagnostics and FVG/IFVG evidence limitations remain visible.
  The completion timestamp, trigger and zone states, frozen source identities
  and transition chronology expose what a future M1 research consumer needs.

The Worker runs all 24 configurations per episode. They are the product of two
sweep policies, three window policies, two FVG sequence policies, and two FVG
geometry profiles. Application callers supply explicit configurations, including
the research window limits. The candidate remains isolated from canonical replay
and reports `CanonicalAdmissionPermitted=false` and `OperationalSignal=false`.

## Run the exposed June 23 context

```powershell
$source = (Resolve-Path 'src/frontend/moneyway-web/src/research/context/data/june23-context.json').Path
$destination = Join-Path $env:TEMP ('moneyway-context-sequence-' + [guid]::NewGuid().ToString('N'))
dotnet run --no-launch-profile --project src/backend/MoneyWay.Worker -- nasdaq-session-liquidity --input $source --output $destination --compare-context-sequence --cutoff-utc 2026-06-23T16:30:00Z
```

`contextual-sequence.json` binds the input SHA256 and documentary mapping.
Its evidence catalogs store episodes, SC evidence, confirmations and zones by
identity. Each configuration retains references to those catalogs, eligibility
reasons, latency, historical completion and subsequent invalidations. The
explicit cutoff can precede the document maximum. The mode accepts the exposed
Development input and rejects the frozen June 22 review input. Older modes keep
their report schemas and publication paths.

## Development results at 16:30 UTC

LL/E1 recorded a low-side penetration at 13:10 UTC (08:10 Bogotá). The bearish
FVG that formed at 12:40 inverted bullish at 13:25 (08:25) and invalidated at
13:30. The separate H1 and H2 contextual SC hypotheses both observed bullish
SC at 13:35 (08:35). Both geometry profiles, both sweep policies and both FVG
sequence policies agreed on the following LL/E1 result at each window:

| Research window policy | Sweep | Eligible first | Directional FVG and consequence |
| --- | --- | --- | --- |
| PostSweepObservation | Eligible | Bullish IFVG 13:25 | Invalidation at 13:30 precedes a possible follow-up. No completion. |
| WindowEventOnly | Outside window | None | Pre-window sweep excludes the entire sequence. |
| WindowConfirmationWithPriorSweep | Eligible same-session prior sweep | Bullish SC 13:35, both hypothesis identities | Bullish FVG 13:55, C1 open 13:40, zone 29570.3–29582.1. Historical completion survives its inversion at 15:10. |

Twelve episode/reference-policy combinations entered the comparison. Six
contained an observable penetration, all on the low side. No SC/IFVG tie
occurred at the first eligible close in this sample. Per single geometry
profile, sweep policy and FVG sequencing policy:

| Window policy | Eligible sweeps | IFVG first | SC first | Complete | Incomplete among eligible sweeps |
| --- | ---: | ---: | ---: | ---: | ---: |
| PostSweepObservation | 6 | 5 | 1 | 5 | 1 |
| WindowEventOnly | 4 | 4 | 0 | 4 | 0 |
| WindowConfirmationWithPriorSweep | 6 | 5 | 1 | 6 | 0 |

The full matrix contains 288 episode/configuration results and 120 historical
completions. These repeat observations from one session. Per configuration
there were 42 directional mismatches and nine direction-compatible post-sweep
confirmations before the window across the six observed episodes. The
event-only window excluded two sweeps. Each same-close diagnostic configuration
marked four episodes as ambiguous at the sweep close. Those episodes still had
later strictly eligible evidence. For both geometry profiles the stricter
formation policy kept the completion counts but shifted AL/E1 post-sweep
completion from 07:15 to 08:40 UTC. The E2/E3 AL/LL cases shifted from 13:55
to 14:05. Under prior-sweep eligibility, AL/E1 also shifted from 13:55 to
14:05. LL/E1 stayed at 13:55 in that window policy.

The sample has documentary availability and no expert ground-truth labels.
June 22, 24, 26, Validation and HeldOut inputs were preserved. Broader independent
validation and human approval remain necessary. No manual structural labeling is
required to run the comparison.
