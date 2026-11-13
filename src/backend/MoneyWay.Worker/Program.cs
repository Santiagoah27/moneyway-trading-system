using MoneyWay.Worker;

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
