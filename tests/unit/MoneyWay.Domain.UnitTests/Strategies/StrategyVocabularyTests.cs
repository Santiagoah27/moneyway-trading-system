using MoneyWay.Domain.Strategies;
using MoneyWay.Domain.TradeEvaluation;

namespace MoneyWay.Domain.UnitTests.Strategies;

public sealed class StrategyVocabularyTests
{
    [Fact]
    public void RuleDefinitionStatusContainsExactVocabulary()
    {
        Assert.Equal(
            ["Confirmed", "Candidate", "ContextSpecific", "VisualOnly", "HumanValidationRequired", "Unresolved", "RejectedAiInference"],
            Enum.GetNames<RuleDefinitionStatus>());
    }

    [Fact]
    public void RuleEvaluationResultContainsExactVocabulary()
    {
        Assert.Equal(
            ["Passed", "Failed", "Waiting", "NotApplicable", "HumanValidationRequired", "DataUnavailable"],
            Enum.GetNames<RuleEvaluationResult>());
    }

    [Fact]
    public void StrategyVerdictContainsExactVocabulary()
    {
        Assert.Equal(
            ["Ready", "Wait", "NoTrade", "HumanValidationRequired", "DataUnavailable"],
            Enum.GetNames<StrategyVerdict>());
    }

    [Fact]
    public void FailureClassificationContainsExactVocabulary()
    {
        Assert.Equal(
            ["ValidStrategyLoss", "RuleViolation", "ExecutionError", "MarketDataError", "InterpretationError", "RiskManagementError", "MarketRegimeMismatch", "Inconclusive"],
            Enum.GetNames<FailureClassification>());
    }
}
