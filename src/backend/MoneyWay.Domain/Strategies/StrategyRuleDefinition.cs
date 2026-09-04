namespace MoneyWay.Domain.Strategies;

/// <summary>
/// Represents documented rule metadata, not the result of evaluating the rule.
/// </summary>
public sealed class StrategyRuleDefinition
{
    public StrategyRuleDefinition(
        RuleId ruleId,
        string name,
        string stage,
        int sequence,
        bool isRequired,
        RuleDefinitionStatus definitionStatus,
        string description,
        string sourceReference)
    {
        ArgumentNullException.ThrowIfNull(ruleId);
        ValidateText(name, nameof(name));
        ValidateText(stage, nameof(stage));
        ValidateText(description, nameof(description));
        ValidateText(sourceReference, nameof(sourceReference));

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must be greater than zero.");
        }

        RuleId = ruleId;
        Name = name;
        Stage = stage;
        Sequence = sequence;
        IsRequired = isRequired;
        DefinitionStatus = definitionStatus;
        Description = description;
        SourceReference = sourceReference;
    }

    public RuleId RuleId { get; }

    public string Name { get; }

    public string Stage { get; }

    public int Sequence { get; }

    public bool IsRequired { get; }

    public RuleDefinitionStatus DefinitionStatus { get; }

    public string Description { get; }

    public string SourceReference { get; }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("Value must be non-empty and have no surrounding whitespace.", parameterName);
        }
    }
}
