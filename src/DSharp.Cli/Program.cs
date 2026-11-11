using DSharp.Compiler;

if (args.Length < 2 || !new[] { "check", "compile" }.Contains(args[0]) ||
    (args[0] == "check" && args.Length != 2) || (args[0] == "compile" && args.Length != 3))
{
    Console.Error.WriteLine("Usage: dsharp check <source.ds> | dsharp compile <source.ds> <output-directory>");
    return 2;
}
try
{
    var result = DSharpCompiler.Compile(File.ReadAllText(args[1]), args[1]);
    foreach (var diagnostic in result.Diagnostics)
        Console.Error.WriteLine($"{diagnostic.Path}({diagnostic.Span.Line},{diagnostic.Span.Column}): {diagnostic.Code}: {diagnostic.Message}");
    if (result.Diagnostics.Count != 0) return 1;
    if (args[0] == "compile")
    {
        Directory.CreateDirectory(args[2]);
        File.WriteAllText(Path.Combine(args[2], "Generated.cs"), result.CSharp);
        File.WriteAllText(Path.Combine(args[2], "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        // Pin the child SDK even when the output is outside this repository.
        File.WriteAllText(Path.Combine(args[2], "global.json"), "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"disable\"}}\n");
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetFullPath(args[2]) };
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
