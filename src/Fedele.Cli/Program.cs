using Fedele.Cli;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

return await CommandLine.RunAsync(args, Console.Out, Console.Error, cancellation.Token);
