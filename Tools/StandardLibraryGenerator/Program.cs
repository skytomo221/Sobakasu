using System;
using System.Collections.Generic;
using System.IO;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;

internal static class Program
{
    private static readonly HashSet<string> AllowedArguments = new(StringComparer.Ordinal)
    {
        "project",
        "catalog",
        "config",
        "output",
        "additions",
        "diagnostics"
    };

    private static int Main(string[] args)
    {
        try
        {
            var values = ParseArguments(args);
            var project = Path.GetFullPath(
                Get(values, "project") ?? Directory.GetCurrentDirectory());
            var catalog = Resolve(project, Get(values, "catalog")) ?? StandardLibraryGenerator.DefaultCatalogPath;
            var config = Resolve(project, Get(values, "config")) ?? StandardLibraryGenerator.DefaultConfigurationPath;
            var output = Resolve(project, Get(values, "output")) ?? StandardLibraryGenerator.DefaultOutputDirectory;
            var additions = Resolve(project, Get(values, "additions")) ?? StandardLibraryGenerator.DefaultAdditionsDirectory;
            var diagnostics = Resolve(project, Get(values, "diagnostics")) ?? StandardLibraryGenerator.DefaultDiagnosticsDirectory;
            var result = StandardLibraryGenerator.CreateDefault(catalog, config).GenerateToDirectory(output, additions, diagnostics);
            Console.WriteLine("Standard library generation completed.");
            Console.WriteLine($"Files: {result.Files.Count}");
            Console.WriteLine($"Types: {result.Report.types_generated}/{result.Report.types_discovered}");
            Console.WriteLine($"Udon API coverage: {result.Report.udon_signatures_covered}/{result.Report.udon_signatures_exposed} ({result.Report.udon_api_coverage_percent:F2}%).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Invalid argument '{token}'. Expected an option beginning with '--'.");

            var key = token.Substring(2);
            if (!AllowedArguments.Contains(key))
                throw new ArgumentException($"Unknown option '--{key}'.");
            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option '--{key}' requires a value.");
            if (result.ContainsKey(key))
                throw new ArgumentException($"Option '--{key}' was specified more than once.");

            result.Add(key, args[++i]);
        }
        return result;
    }

    private static string Get(IDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string Resolve(string project, string path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(project, path));
}
