using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class MultilineDeclarationBoundaryTests
{
    public async Task MultilineTypesPreserveDeclarationBoundaries()
    {
        const string source = """
            module Demo

            type Entry {
                pair: (
                    int,
                    int
                )
                choices: List<(
                    int,
                    int
                )> // complete multiline generic type
                option: Option<
                    int
                >
                nested: Option<List<
                    int
                >>?
            }
            type Commented {
                pair: (
                    int,
                    int
                ) /* block comment
                     preserves the boundary */ number: int
            }
            interface Source {
                pair(): (
                    int,
                    int
                )
                callback(): (
                    int
                ) -> int // complete function return type
                apply(value: int): int
            }
            interface CommentedSource {
                pair(): (
                    int,
                    int
                ) /* another
                     boundary */ number(): int
                nested(): Option<List<
                    int
                >>?
                text(): string
            }
            service Values: Source {
                pair() { (20, 22) }
                callback() { (x: int) => x + 1 }
                apply(value) { value + 1 }
            }
            fn answer(): int {
                let value: Source = Values()
                let (left, right) = value.pair()
                let entry = Entry {
                    pair = (left, right)
                    choices = [(left, right)]
                    option = Some(41)
                    nested = null
                }
                let (first, second) = entry.pair
                if first + second == 42 && value.callback()(41) == 42 && value.apply(41) == 42
                { 42 } else { 0 }
            }
            """;
        foreach (var newline in new[] { "\n", "\r\n", "\r" })
        {
            var input = source.Replace("\n", newline, StringComparison.Ordinal);
            var result = DSharpCompiler.Compile(input, "multiline-declarations.ds");
            Assert.True(result.CSharp is not null, string.Join("\n", result.Diagnostics));
            Assert.Empty(result.Diagnostics);
            await WithGenerated(result.CSharp!, type =>
                Assert.Equal(42, type.GetMethod("answer", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null)));
        }

        string[] boundaryFailures =
        [
            "type R { first: int |second: int }",
            "type R { first: (\n int,\n int) |second: int }",
            "type R { first: (\n int,\n int) /* inline */ |second: int }",
            "type R { first: List<(\n int,\n int)> |second: int }",
            "type R { first: Option<\n int\n> |second: int }",
            "type R { first: Option<List<\n int\n>>? |second: int }",
            "interface I { first(): int |second(): int }",
            "interface I { first(): (\n int,\n int) |second(): int }",
            "interface I { first(): (\n int\n) -> int /* inline */ |second(): int }",
            "interface I { first(): List<(\n int,\n int)> |second(): int }",
            "interface I { first(): Option<\n int\n> |second(): int }",
            "interface I { first(): Option<List<\n int\n>>? |second(): int }"
        ];
        foreach (var newline in new[] { "\n", "\r\n", "\r" })
        foreach (var markedBody in boundaryFailures)
        {
            var marked = ("module Invalid\n" + markedBody + "\n").Replace("\n", newline, StringComparison.Ordinal);
            var start = marked.IndexOf('|');
            var input = marked.Remove(start, 1);
            var result = DSharpCompiler.Compile(input, "invalid-multiline-declaration.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + markedBody);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("DS1002", diagnostic.Code);
            Assert.Equal("invalid-multiline-declaration.ds", diagnostic.Path);
            Assert.Equal(start, diagnostic.Span.Start);
            var prefix = input[..start];
            Assert.Equal(prefix.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).Length, diagnostic.Span.Line);
            Assert.Equal(start - prefix.LastIndexOfAny(new[] { '\r', '\n' }), diagnostic.Span.Column);
            Assert.InRange(diagnostic.Span.Length, 1, input.Length - start);
        }
    }

    private static async Task WithGenerated(string csharp, Action<Type> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-multiline-declarations-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.cs"), csharp);
            await File.WriteAllTextAsync(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"11.0.100\",\"rollForward\":\"disable\"}}");
            await File.WriteAllTextAsync(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "build", "Generated.csproj", "--nologo", "--verbosity", "quiet" }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated multiline declaration build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("multiline-declarations-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net11.0/Generated.dll"));
                verify(assembly.GetType("DSharp.Generated.Demo")!);
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }
}
