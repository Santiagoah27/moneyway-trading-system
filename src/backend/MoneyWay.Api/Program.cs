using MoneyWay.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

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

app.Run();

public partial class Program;
