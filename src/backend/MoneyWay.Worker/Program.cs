using MoneyWay.Worker;

if (args.Length > 0 && string.Equals(args[0], "replay-csv", StringComparison.Ordinal))
{
    return new HistoricalReplayCommand().Execute(args, Console.Out, Console.Error);
}

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
return 0;
