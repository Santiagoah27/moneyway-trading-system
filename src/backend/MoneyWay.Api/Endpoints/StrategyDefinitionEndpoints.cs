using Microsoft.AspNetCore.Http.HttpResults;
using MoneyWay.Api.Contracts.StrategyDefinitions;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Domain.Strategies;

namespace MoneyWay.Api.Endpoints;

/// <summary>
/// Maps read-only access to versioned built-in strategy definitions.
/// </summary>
public static class StrategyDefinitionEndpoints
{
    public static IEndpointRouteBuilder MapStrategyDefinitionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/strategy-definitions", GetAll)
            .WithName("GetStrategyDefinitions")
            .WithSummary("Get built-in strategy definitions")
            .WithDescription("Returns versioned definitions only; it does not analyze markets, evaluate strategies, or produce signals.")
            .Produces<IReadOnlyList<StrategyDefinitionResponse>>(StatusCodes.Status200OK);

        endpoints.MapGet("/api/strategy-definitions/{strategyId}/{version}", GetExact)
            .WithName("GetStrategyDefinition")
            .WithSummary("Get an exact versioned strategy definition")
            .WithDescription("Uses exact identifiers and returns definition metadata only; it does not analyze markets, evaluate strategies, or produce signals.")
            .Produces<StrategyDefinitionResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static Ok<IReadOnlyList<StrategyDefinitionResponse>> GetAll(StrategyDefinitionCatalog catalog)
    {
        return TypedResults.Ok<IReadOnlyList<StrategyDefinitionResponse>>(
            catalog.GetAll().Select(ToResponse).ToArray());
    }

    private static Results<Ok<StrategyDefinitionResponse>, ValidationProblem, NotFound> GetExact(
        string strategyId,
        string version,
        StrategyDefinitionCatalog catalog)
    {
        try
        {
            var definition = catalog.Find(new StrategyId(strategyId), new StrategyVersion(version));
            return definition is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(definition));
        }
        catch (ArgumentException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["route"] = ["Strategy identifier and version must be non-empty and have no surrounding whitespace."],
            });
        }
    }

    private static StrategyDefinitionResponse ToResponse(StrategyDefinition definition)
    {
        var rules = definition.Rules.Select(static rule => new StrategyRuleDefinitionResponse(
            rule.RuleId.Value,
            rule.Name,
            rule.Stage,
            rule.Sequence,
            rule.IsRequired,
            rule.DefinitionStatus.ToString(),
            rule.Description,
            rule.SourceReference)).ToArray();

        return new StrategyDefinitionResponse(
            definition.StrategyId.Value,
            definition.Version.Value,
            definition.DisplayName,
            definition.SpecificationReference,
            rules.Length,
            rules.Count(static rule => rule.IsRequired),
            rules);
    }
}
