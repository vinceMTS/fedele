// The analyze and anonymize commands are implemented in the next step
// (see docs/stato.md). This tool must never print values from input files.
Console.WriteLine("Fedele.Anonymizer");
Console.WriteLine();
Console.WriteLine("Usage:");
Console.WriteLine("  analyze <file>                                    (not implemented yet)");
Console.WriteLine("  anonymize <file> <output> --columns <list> [--seed <n>]   (not implemented yet)");
return args.Length == 0 ? 0 : 1;
