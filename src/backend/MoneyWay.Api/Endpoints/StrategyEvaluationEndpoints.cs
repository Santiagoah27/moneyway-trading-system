using Microsoft.AspNetCore.Http.HttpResults;
using MoneyWay.Api.Contracts.StrategyEvaluation;
using MoneyWay.Application.StrategyEvaluation;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Api.Endpoints;

/// <summary>
/// Maps the manual strategy evaluation HTTP boundary.
/// </summary>
public static class StrategyEvaluationEndpoints
{
    public static IEndpointRouteBuilder MapStrategyEvaluationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/strategy-evaluations/manual", EvaluateManual)
            .WithName("EvaluateStrategyManually")
            .WithSummary("Evaluate caller-supplied rule evaluations")
            .WithDescription(
                "Calculates a verdict from manually determined rule evaluations. Ready only means all required "
                + "evaluations supplied passed or were not applicable; it is not a signal or execution authorization.")
            .Produces<ManualStrategyEvaluationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static Results<Ok<ManualStrategyEvaluationResponse>, ValidationProblem> EvaluateManual(
        ManualStrategyEvaluationRequest request,
        EvaluateStrategyUseCase useCase)
    {
        try
        {
            var applicationRequest = ToApplicationRequest(request);
            var result = useCase.Execute(applicationRequest);
            return TypedResults.Ok(ToResponse(result));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["request"] = [exception.Message],
            });
        }
    }

    private static EvaluateStrategyRequest ToApplicationRequest(ManualStrategyEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Evaluations);

        var evaluations = request.Evaluations.Select((evaluation, index) =>
        {
            if (evaluation is null)
            {
                throw new ArgumentException($"Evaluation at index {index} cannot be null.", nameof(request));
            }

            return new RuleEvaluation(
                new RuleId(evaluation.RuleId!),
                ParseCanonical<RuleDefinitionStatus>(evaluation.DefinitionStatus, nameof(evaluation.DefinitionStatus)),
                ParseCanonical<RuleEvaluationResult>(evaluation.Result, nameof(evaluation.Result)),
                evaluation.Sequence,
                evaluation.IsRequired,
                evaluation.Reason!,
                evaluation.EvaluatedAtUtc,
                evaluation.EvidenceReference);
        });

        return new EvaluateStrategyRequest(
            new StrategyId(request.StrategyId!),
            new StrategyVersion(request.StrategyVersion!),
            evaluations);
    }

    private static TEnum ParseCanonical<TEnum>(string? value, string parameterName)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (!Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed)
            || !string.Equals(parsed.ToString(), value, StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{value}' is not a canonical {typeof(TEnum).Name} name.", parameterName);
        }

        return parsed;
    }

    private static ManualStrategyEvaluationResponse ToResponse(EvaluateStrategyResult result)
    {
        return new ManualStrategyEvaluationResponse(
            result.StrategyId.Value,
            result.StrategyVersion.Value,
            result.Outcome.Verdict.ToString(),
            result.Outcome.Reason,
            result.Outcome.BlockingRuleId?.Value,
            result.Outcome.BlockingSequence,
            result.Outcome.BlockingResult?.ToString(),
            result.Outcome.EvaluationCount,
            result.Outcome.RequiredEvaluationCount,
            result.Evaluations.Select(static evaluation => new RuleEvaluationResponse(
                evaluation.RuleId.Value,
                evaluation.DefinitionStatus.ToString(),
                evaluation.Result.ToString(),
                evaluation.Sequence,
                evaluation.IsRequired,
                evaluation.Reason,
                evaluation.EvaluatedAtUtc,
                evaluation.EvidenceReference)).ToArray());
    }
}
