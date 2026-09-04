using MoneyWay.Api;
using MoneyWay.Api.Endpoints;
using MoneyWay.Application.StrategyDefinitions;
using MoneyWay.Application.StrategyEvaluation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<EvaluateStrategyUseCase>();
builder.Services.AddSingleton<StrategyDefinitionCatalog>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddPolicy("DevelopmentFrontend", policy =>
        policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("DevelopmentFrontend");
}

app.MapHealthChecks("/health");
app.MapGet("/api/system/status", () => Results.Ok(new SystemStatusResponse(
    "MoneyWay Trading System", "bootstrap", "disabled", false)));
app.MapStrategyEvaluationEndpoints();
app.MapStrategyDefinitionEndpoints();

app.Run();

public partial class Program;
