using MoneyWay.Worker;

if (args.Length > 0 && args[0] == "h4-structure-research")
{
    return new H4StructureResearchCommand().Execute(args, Console.Out, Console.Error);
}

if (args.Length > 0 && args[0] == "nasdaq-session-liquidity")
{
    return new NasdaqSessionLiquidityResearchCommand().Execute(args, Console.Out, Console.Error);
}

if (args.Length > 0 && args[0] == "m5-swing-lab")
{
    return new M5SwingLabCommand().Execute(args, Console.Out, Console.Error);
}

if (args.Length > 0 && args[0] == "mt5-timestamp-witness")
{
    return await new Mt5TimestampWitnessCommand().ExecuteAsync(args, Console.Out, Console.Error);
}

if (args.Length > 0 && args[0] == "mt5-causal-availability")
{
    return new Mt5CausalAvailabilityCommand().Execute(args, Console.Out, Console.Error);
}

if (args.Length > 0 && args[0] == "mt5-lifecycle")
{
    return new Mt5LifecycleCommand().Execute(args, Console.Out, Console.Error);
}

if (args.Length > 0 && string.Equals(args[0], "mt5-receipt", StringComparison.Ordinal))
{
    if (args.Length > 1 && args[1] == "observe")
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;
        try
        {
            return await new Mt5ReceiptObservationCommand().ExecuteAsync(args, Console.Out, Console.Error, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }
    return new Mt5ReceiptCommand().Execute(args, Console.Out, Console.Error);
}

if (args.Length > 0 && string.Equals(args[0], "replay-csv", StringComparison.Ordinal))
{
    return new HistoricalReplayCommand().Execute(args, Console.Out, Console.Error);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
return 0;
