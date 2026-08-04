using MoneyWay.Application.StrategyEvaluation;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyEvaluation;

public sealed class EvaluateStrategyRequestTests
{
    [Fact]
    public void ValidValuesAndInputOrderArePreserved()
    {
        var strategyId = new StrategyId("strategy-one");
        var strategyVersion = new StrategyVersion("strategy-one-0.1-draft");
        var later = CreateEvaluation("RULE-002", 20);
        var earlier = CreateEvaluation("RULE-001", 10);

        var request = new EvaluateStrategyRequest(strategyId, strategyVersion, [later, earlier]);

        Assert.Same(strategyId, request.StrategyId);
        Assert.Same(strategyVersion, request.StrategyVersion);
        Assert.Equal([later, earlier], request.Evaluations);
    }

    [Fact]
    public void EmptyCollectionIsAccepted()
    {
        Assert.Empty(new EvaluateStrategyRequest(
            new StrategyId("strategy"), new StrategyVersion("version"), []).Evaluations);
    }

    [Fact]
    public void NullEvaluationsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EvaluateStrategyRequest(
            new StrategyId("strategy"), new StrategyVersion("version"), null!));
    }

    [Fact]
    public void NullEvaluationElementIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new EvaluateStrategyRequest(
            new StrategyId("strategy"), new StrategyVersion("version"), [null!]));
    }

    [Fact]
    public void NullStrategyIdIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EvaluateStrategyRequest(
            null!, new StrategyVersion("version"), []));
    }

    [Fact]
    public void NullStrategyVersionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EvaluateStrategyRequest(
            new StrategyId("strategy"), null!, []));
    }

    [Fact]
    public void OriginalListChangesDoNotAlterSnapshot()
    {
        var first = CreateEvaluation("RULE-001", 1);
        var source = new List<RuleEvaluation> { first };
        var request = new EvaluateStrategyRequest(new StrategyId("strategy"), new StrategyVersion("version"), source);

        source.Clear();
        source.Add(CreateEvaluation("RULE-002", 2));

        Assert.Equal([first], request.Evaluations);
    }

    [Fact]
    public void ExposedCollectionCannotBeModified()
    {
        var request = new EvaluateStrategyRequest(
            new StrategyId("strategy"), new StrategyVersion("version"), [CreateEvaluation("RULE-001", 1)]);
        var collection = Assert.IsAssignableFrom<ICollection<RuleEvaluation>>(request.Evaluations);

        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Add(CreateEvaluation("RULE-002", 2)));
    }

    private static RuleEvaluation CreateEvaluation(string ruleId, int sequence)
    {
        return new RuleEvaluation(
            new RuleId(ruleId),
            RuleDefinitionStatus.Confirmed,
            RuleEvaluationResult.Passed,
            sequence,
            true,
            $"Reason for {ruleId}.",
            new DateTimeOffset(2026, 8, 4, 12, 0, 0, TimeSpan.Zero),
            null);
    }
}
