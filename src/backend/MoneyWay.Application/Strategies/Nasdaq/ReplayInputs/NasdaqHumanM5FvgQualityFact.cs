namespace MoneyWay.Application.Strategies.Nasdaq.ReplayInputs;

public enum NasdaqHumanM5FvgQualityDecision { Approved, Rejected }

/// <summary>Source-backed review of the exact selected FVG; neither decision is a runtime result.</summary>
public sealed class NasdaqHumanM5FvgQualityFact
{
    public NasdaqHumanM5FvgQualityFact(NasdaqHumanM5FvgObservation fvg, NasdaqHumanM5FvgQualityDecision decision,
        DateTimeOffset effectiveAtUtc, string rationale)
    {
        Fvg = fvg ?? throw new ArgumentNullException(nameof(fvg));
        if (!Enum.IsDefined(decision)) throw new ArgumentOutOfRangeException(nameof(decision));
        if (effectiveAtUtc.Offset != TimeSpan.Zero || effectiveAtUtc < fvg.EffectiveAtUtc)
            throw new ArgumentException("Quality review time must be UTC and no earlier than FVG confirmation.", nameof(effectiveAtUtc));
        NasdaqStructuralLiquidityReference.ValidateProvenance(rationale);
        Decision = decision; EffectiveAtUtc = effectiveAtUtc; Rationale = rationale;
    }
    public NasdaqHumanM5FvgObservation Fvg { get; }
    public NasdaqHumanM5FvgQualityDecision Decision { get; }
    public DateTimeOffset EffectiveAtUtc { get; }
    public string Rationale { get; }
    // Rationale is retained review support, not an invented quality grade or source priority.
    internal bool SameFact(NasdaqHumanM5FvgQualityFact other) => Fvg.SameFact(other.Fvg)
        && Decision == other.Decision && EffectiveAtUtc == other.EffectiveAtUtc;
}
