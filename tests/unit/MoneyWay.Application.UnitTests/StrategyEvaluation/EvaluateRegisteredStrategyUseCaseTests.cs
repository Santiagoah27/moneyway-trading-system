using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions.Forex;
using MoneyWay.Application.StrategyDefinitions.Nasdaq;
using MoneyWay.Application.StrategyEvaluation;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Application.UnitTests.StrategyEvaluation;

public sealed class EvaluateRegisteredStrategyUseCaseTests
{
    private static readonly DateTimeOffset EvaluatedAtUtc = new(2026, 9, 3, 20, 0, 0, TimeSpan.Zero);
    private readonly EvaluateRegisteredStrategyUseCase useCase = new(new StrategyDefinitionCatalog(), new EvaluateStrategyUseCase());

    [Fact]
    public void ValidRegisteredRequestSucceedsAndPreservesGenericOutcome()
    {
        var request = CreateValidRequest(MoneyWayForexStrategyDefinition.Instance);

        var result = useCase.Execute(request);

        Assert.Equal(StrategyEvaluationExecutionStatus.Success, result.Status);
        Assert.NotNull(result.Evaluation);
        Assert.Equal(StrategyVerdict.Ready, result.Evaluation.Outcome.Verdict);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData("moneyway-unknown", "unknown-0.1.0-draft")]
    [InlineData("moneyway-forex", "forex-99.0.0-draft")]
    public void UnknownDefinitionReturnsNotFound(string strategyId, string version)
    {
        var result = useCase.Execute(new EvaluateStrategyRequest(
            new StrategyId(strategyId), new StrategyVersion(version), []));

        Assert.Equal(StrategyEvaluationExecutionStatus.StrategyNotFound, result.Status);
        Assert.Null(result.Evaluation);
        Assert.Equal("strategy_not_found", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void UnknownForeignAndDuplicateRulesAreReportedTogetherWithMissingRules()
    {
        var foreign = MoneyWayNasdaqStrategyDefinition.Instance.Rules[0];
        var evaluations = new[] { CreateEvaluation(foreign), CreateEvaluation(foreign, sequence: foreign.Sequence + 1) };
        var request = new EvaluateStrategyRequest(
            MoneyWayForexStrategyDefinition.Instance.StrategyId,
            MoneyWayForexStrategyDefinition.Instance.Version,
            evaluations);

        var result = useCase.Execute(request);

        Assert.Equal(StrategyEvaluationExecutionStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == "duplicate_rule" && issue.RuleId == foreign.RuleId);
        Assert.Contains(result.Issues, issue => issue.Code == "unknown_rule" && issue.RuleId == foreign.RuleId);
        Assert.Contains(result.Issues, issue => issue.Code == "missing_required_rule");
    }

    [Fact]
    public void InventedRuleIsRejected()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var evaluations = RequiredEvaluations(definition).Append(new RuleEvaluation(
            new RuleId("FX-INVENTED-999"), RuleDefinitionStatus.Confirmed, RuleEvaluationResult.Passed,
            999, false, "Invented rule.", EvaluatedAtUtc, null));

        Assert.Contains(useCase.Execute(CreateRequest(definition, evaluations)).Issues,
            issue => issue.Code == "unknown_rule" && issue.RuleId == new RuleId("FX-INVENTED-999"));
    }

    [Theory]
    [InlineData("sequence_mismatch")]
    [InlineData("required_flag_mismatch")]
    [InlineData("definition_status_mismatch")]
    public void AlteredRegisteredMetadataIsRejected(string expectedCode)
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var target = definition.Rules.First(static rule => rule.IsRequired);
        var evaluations = RequiredEvaluations(definition).Select(evaluation => evaluation.RuleId != target.RuleId
            ? evaluation
            : new RuleEvaluation(
                evaluation.RuleId,
                expectedCode == "definition_status_mismatch" ? RuleDefinitionStatus.Confirmed : evaluation.DefinitionStatus,
                evaluation.Result,
                expectedCode == "sequence_mismatch" ? evaluation.Sequence + 1 : evaluation.Sequence,
                expectedCode == "required_flag_mismatch" ? !evaluation.IsRequired : evaluation.IsRequired,
                evaluation.Reason,
                evaluation.EvaluatedAtUtc,
                evaluation.EvidenceReference));

        var result = useCase.Execute(CreateRequest(definition, evaluations));

        Assert.Equal(StrategyEvaluationExecutionStatus.ValidationFailed, result.Status);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode && issue.RuleId == target.RuleId);
    }

    [Fact]
    public void MissingRequiredRuleIsRejected()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var missing = definition.Rules.First(static rule => rule.IsRequired);

        var result = useCase.Execute(CreateRequest(
            definition, RequiredEvaluations(definition).Where(evaluation => evaluation.RuleId != missing.RuleId)));

        Assert.Equal("missing_required_rule", Assert.Single(result.Issues).Code);
        Assert.Equal(missing.RuleId, result.Issues[0].RuleId);
    }

    [Fact]
    public void OptionalRuleMayBeOmittedOrIncludedWithExactMetadata()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var optional = definition.Rules.First(static rule => !rule.IsRequired);

        var omitted = useCase.Execute(CreateValidRequest(definition));
        var included = useCase.Execute(CreateRequest(definition, RequiredEvaluations(definition).Append(CreateEvaluation(optional))));

        Assert.Equal(StrategyEvaluationExecutionStatus.Success, omitted.Status);
        Assert.Equal(StrategyEvaluationExecutionStatus.Success, included.Status);
    }

    [Fact]
    public void AlteredOptionalMetadataIsRejected()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var optional = definition.Rules.First(static rule => !rule.IsRequired);
        var altered = CreateEvaluation(optional, isRequired: true);

        var result = useCase.Execute(CreateRequest(definition, RequiredEvaluations(definition).Append(altered)));

        Assert.Contains(result.Issues, issue => issue.Code == "required_flag_mismatch" && issue.RuleId == optional.RuleId);
    }

    [Fact]
    public void InputOrderDoesNotChangeValidationOrMutateRequestOrDefinition()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var beforeRules = definition.Rules.ToArray();
        var source = RequiredEvaluations(definition).Reverse().ToList();
        var request = CreateRequest(definition, source);

        var first = useCase.Execute(request);
        var second = useCase.Execute(CreateRequest(definition, source.AsEnumerable().Reverse()));

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Issues, second.Issues);
        Assert.Equal(first.Evaluation!.Outcome, second.Evaluation!.Outcome);
        Assert.Equal(source, request.Evaluations);
        Assert.Equal(beforeRules, definition.Rules);
    }

    [Fact]
    public void IssuesUseDeterministicEvaluationThenDefinitionOrder()
    {
        var definition = MoneyWayForexStrategyDefinition.Instance;
        var later = definition.Rules.First(rule => rule.IsRequired && rule.Sequence == 20);
        var earlier = definition.Rules.First(rule => rule.IsRequired && rule.Sequence == 10);
        var request = CreateRequest(definition,
        [
            CreateEvaluation(later, sequence: 999),
            CreateEvaluation(earlier, sequence: 998, isRequired: false),
        ]);

        var result = useCase.Execute(request);

        Assert.Equal(["sequence_mismatch", "required_flag_mismatch"], result.Issues.Take(2).Select(issue => issue.Code));
        Assert.Equal(earlier.RuleId, result.Issues[0].RuleId);
        Assert.Equal(earlier.RuleId, result.Issues[1].RuleId);
        Assert.Equal(later.RuleId, result.Issues[2].RuleId);
        Assert.Equal(definition.Rules.Where(rule => rule.IsRequired && rule.Sequence >= 30).Select(rule => rule.RuleId),
            result.Issues.Skip(3).Select(issue => issue.RuleId));
    }

    [Fact]
    public void RequiredBlockingResultUsesSequentialEvaluatorSemantics()
    {
        var definition = MoneyWayNasdaqStrategyDefinition.Instance;
        var blocker = definition.Rules.First(static rule => rule.IsRequired);
        var evaluations = RequiredEvaluations(definition).Select(evaluation => evaluation.RuleId == blocker.RuleId
            ? CreateEvaluation(blocker, RuleEvaluationResult.Waiting)
            : evaluation);

        var result = useCase.Execute(CreateRequest(definition, evaluations));

        Assert.Equal(StrategyVerdict.Wait, result.Evaluation!.Outcome.Verdict);
        Assert.Equal(blocker.RuleId, result.Evaluation.Outcome.BlockingRuleId);
    }

    private static EvaluateStrategyRequest CreateValidRequest(StrategyDefinition definition) =>
        CreateRequest(definition, RequiredEvaluations(definition));

    private static EvaluateStrategyRequest CreateRequest(
        StrategyDefinition definition,
        IEnumerable<RuleEvaluation> evaluations) =>
        new(definition.StrategyId, definition.Version, evaluations);

    private static IEnumerable<RuleEvaluation> RequiredEvaluations(StrategyDefinition definition) =>
        definition.Rules.Where(static rule => rule.IsRequired).Select(static rule => CreateEvaluation(rule));

    private static RuleEvaluation CreateEvaluation(
        StrategyRuleDefinition rule,
        RuleEvaluationResult result = RuleEvaluationResult.Passed,
        int? sequence = null,
        bool? isRequired = null) =>
        new(rule.RuleId, rule.DefinitionStatus, result, sequence ?? rule.Sequence, isRequired ?? rule.IsRequired,
            $"Manual result for {rule.RuleId.Value}.", EvaluatedAtUtc, "audited-test-evidence");
}
