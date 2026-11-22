using DSharp.Compiler;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

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
        byte[]? runtimeAssembly = null;
        if (result.RequiresRuntime)
        {
            var runtimePath = Environment.GetEnvironmentVariable("DSHARP_RUNTIME_ASSEMBLY");
            if (string.IsNullOrWhiteSpace(runtimePath))
                throw new InvalidDataException("DSHARP_RUNTIME_ASSEMBLY must name a valid DSharp.Runtime assembly for this compilation.");

            // Inspect the same bytes that will be copied, so the reference cannot
            // change between validation and writing the generated project.
            try { runtimeAssembly = File.ReadAllBytes(runtimePath); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                throw new InvalidDataException("Cannot read DSHARP_RUNTIME_ASSEMBLY: " + error.Message, error);
            }
            try
            {
                using var stream = new MemoryStream(runtimeAssembly, writable: false);
                using var pe = new PEReader(stream);
                if (!pe.HasMetadata || pe.PEHeaders.CorHeader is null)
                    throw new BadImageFormatException();
                var metadata = pe.GetMetadataReader();
                if (!metadata.IsAssembly ||
                    !string.Equals(metadata.GetString(metadata.GetAssemblyDefinition().Name), "DSharp.Runtime", StringComparison.Ordinal))
                    throw new BadImageFormatException();
            }
            catch (Exception error) when (error is BadImageFormatException or InvalidOperationException or ArgumentException)
            {
                throw new InvalidDataException("DSHARP_RUNTIME_ASSEMBLY must name a valid managed assembly with identity DSharp.Runtime.", error);
            }
        }
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "Generated.cs"), result.CSharp);
        if (runtimeAssembly is not null)
            File.WriteAllBytes(Path.Combine(outputDirectory, "DSharp.Runtime.dll"), runtimeAssembly);
        var project = runtimeAssembly is null
            ? "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>\n"
            : "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include=\"DSharp.Runtime\"><HintPath>DSharp.Runtime.dll</HintPath></Reference></ItemGroup></Project>\n";
        File.WriteAllText(Path.Combine(outputDirectory, "Generated.csproj"), project);
        // Pin the child SDK even when the output is outside this repository.
        File.WriteAllText(Path.Combine(outputDirectory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}\n");
        var start = new System.Diagnostics.ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetFullPath(outputDirectory) };
        start.ArgumentList.Add("build"); start.ArgumentList.Add("Generated.csproj"); start.ArgumentList.Add("--nologo");
        using var build = System.Diagnostics.Process.Start(start)!;
        build.WaitForExit(); return build.ExitCode;
    }
    return 0;
}
catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"DS0001: {error.Message}"); return 1;
}
