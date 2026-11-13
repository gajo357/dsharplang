using DSharp.Compiler;

if (args.Length < 2 || !new[] { "check", "compile" }.Contains(args[0]) ||
    (args[0] == "compile" && args.Length < 3))
{
    Console.Error.WriteLine("Usage: dsharp check <source.ds> [source.ds ...] | dsharp compile <source.ds> [source.ds ...] <output-directory>");
    return 2;
}
try
{
    var sourcePaths = args[0] == "check" ? args[1..] : args[1..^1];
    var sources = sourcePaths.Select(path => new SourceFile(path, File.ReadAllText(path))).ToArray();
    // Keep the existing single-file entry point and its behavior intact.
    var result = sources.Length == 1
        ? DSharpCompiler.Compile(sources[0].Text, sources[0].Path)
        : DSharpCompiler.Compile(sources);
    foreach (var diagnostic in result.Diagnostics)
        Console.Error.WriteLine($"{diagnostic.Path}({diagnostic.Span.Line},{diagnostic.Span.Column}): {diagnostic.Code}: {diagnostic.Message}");
    if (result.Diagnostics.Count != 0) return 1;
    if (args[0] == "compile")
    {
        var outputDirectory = args[^1];
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "Generated.cs"), result.CSharp);
        File.WriteAllText(Path.Combine(outputDirectory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>\n");
        // Pin the child SDK even when the output is outside this repository.
        File.WriteAllText(Path.Combine(outputDirectory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}\n");
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetFullPath(outputDirectory) };
        start.ArgumentList.Add("build"); start.ArgumentList.Add("Generated.csproj"); start.ArgumentList.Add("--nologo");
        using var build = System.Diagnostics.Process.Start(start)!;
        build.WaitForExit(); return build.ExitCode;
    }
    return 0;
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"DS0001: {error.Message}"); return 1;
}
