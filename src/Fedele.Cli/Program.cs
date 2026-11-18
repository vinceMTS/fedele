using System.Reflection;

var version = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion ?? "unknown";

Console.WriteLine($"fedele {version}");
Console.WriteLine("Commands (coming soon): inspect, infer, validate, convert");
return 0;
