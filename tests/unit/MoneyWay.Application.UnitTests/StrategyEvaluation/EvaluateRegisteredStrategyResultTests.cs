using MoneyWay.Application.StrategyEvaluation;

namespace MoneyWay.Application.UnitTests.StrategyEvaluation;

public sealed class EvaluateRegisteredStrategyResultTests
{
    [Fact]
    public void ValidationFailureRequiresAtLeastOneIssue()
    {
        Assert.Throws<ArgumentException>(() => EvaluateRegisteredStrategyResult.ValidationFailed([]));
    }

    [Fact]
    public void FactoriesRejectNullValues()
    {
        Assert.Throws<ArgumentNullException>(() => EvaluateRegisteredStrategyResult.Success(null!));
        Assert.Throws<ArgumentNullException>(() => EvaluateRegisteredStrategyResult.StrategyNotFound(null!));
        Assert.Throws<ArgumentNullException>(() => EvaluateRegisteredStrategyResult.ValidationFailed(null!));
    }

    [Fact]
    public void IssuesAreImmutableSnapshots()
    {
        var issue = new StrategyEvaluationValidationIssue("unknown_rule", "Unknown rule.");
        var source = new List<StrategyEvaluationValidationIssue> { issue };
        var result = EvaluateRegisteredStrategyResult.ValidationFailed(source);
        source.Clear();

        Assert.Equal([issue], result.Issues);
        var collection = Assert.IsAssignableFrom<ICollection<StrategyEvaluationValidationIssue>>(result.Issues);
        Assert.True(collection.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => collection.Clear());
    }

    [Theory]
    [InlineData(null, "Message")]
    [InlineData("", "Message")]
    [InlineData(" code", "Message")]
    [InlineData("code", null)]
    [InlineData("code", " ")]
    public void IssueRejectsInvalidText(string? code, string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new StrategyEvaluationValidationIssue(code!, message!));
    }
}
