using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.StrategyEvaluation;

public sealed record StrategyEvaluationValidationIssue
{
    public StrategyEvaluationValidationIssue(string code, string message, RuleId? ruleId = null)
    {
        ValidateText(code, nameof(code));
        ValidateText(message, nameof(message));

        Code = code;
        Message = message;
        RuleId = ruleId;
    }

    public string Code { get; }

    public string Message { get; }

    public RuleId? RuleId { get; }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            throw new ArgumentException("Value must be non-empty and have no surrounding whitespace.", parameterName);
        }
    }
}
