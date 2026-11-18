using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

public sealed class MultilineBindingHeaderTests
{
    [Fact]
    public async Task ParenthesizedHeadersContinueAcrossLinesOnlyInsideTheirDelimiters()
    {
        const string source = """
            module Demo
            type Box<T> { value: T }

            fn numbers(mark: (int)->int): ((int, int), int) {
                let called = mark(99)
                ((19, 20), 3)
            }
            fn counted(mark: (int)->int): int {
                let (
                    (left, /* inline */ middle), // nested pattern
                    right
                ) = numbers(mark)
                left + middle + right
            }
            fn answer(): int {
                let (
                    (first,
                        second),
                    third
                ): (
                    (int, int),
                    int
                ) = ((19, 20), 3)
                let pair: (
                    int, // comment after tuple element
                    int
                ) = (first, second)
                let add: (
                    int,
                    int
                ) -> int = (left, right) => left + right
                let apply: (
                    (int) -> int,
                    int
                ) -> int = (f, number) => f(number)
                let (p, q) = pair
                let continued: (
                    int,
                    int
                ) = // continuation after equals
                    (20, 22)
                let (x, y) = continued
                let boxed: Box<(
                    int,
                    int
                )>=Box { value = (20, 22) }
                let single: Box<int>=Box { value = 42 }
                let (boxedLeft, boxedRight) = boxed.value
                if add(p, q) + third == 42 && apply(n => n + 1, 41) == 42 && x + y == 42 && boxedLeft + boxedRight == single.value
                { 42 } else { 0 }
            }
            """;
        foreach (var newline in new[] { "\n", "\r\n", "\r" })
        {
            var input = source.Replace("\n", newline, StringComparison.Ordinal);
            var compiled = DSharpCompiler.Compile(input, "multiline-header.ds");
            Assert.True(compiled.CSharp is not null, string.Join("\n", compiled.Diagnostics));
            Assert.Empty(compiled.Diagnostics);
            await WithGenerated(compiled.CSharp!, type =>
            {
                Assert.Equal(42, Method(type, "answer").Invoke(null, null));
                var events = new List<int>();
                var counted = Method(type, "counted");
                var mark = Adapt(counted.GetParameters()[0].ParameterType, n => { events.Add(n); return n; });
                Assert.Equal(42, counted.Invoke(null, new object[] { mark }));
                Assert.Equal(new[] { 99 }, events);
            });
        }

        string[] boundaryFailures =
        [
            "let\n|value = 42\nvalue",
            "let value\n|: int = 42\nvalue",
            "let value // comment\n|= 42\nvalue",
            "let (a, b)\n|= (20, 22)\na + b",
            "let (a, b)\n|: (int, int) = (20, 22)\na + b",
            "let pair: (\n int,\n int\n)\n|= (20, 22)\n42",
            "let (\n a, b\n) = (20, 22) |42",
            "let (\n a, b\n) = (20, 22)\n|+ 2\na + b"
        ];
        foreach (var newline in new[] { "\n", "\r\n", "\r" })
        foreach (var markedBody in boundaryFailures)
        {
            var marked = ("module Demo\nfn answer(): int {\n" + markedBody + "\n}\n").Replace("\n", newline, StringComparison.Ordinal);
            var start = marked.IndexOf('|');
            var input = marked.Remove(start, 1);
            var result = DSharpCompiler.Compile(input, "multiline-boundary.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + markedBody);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.StartsWith("DS", diagnostic.Code);
            Assert.Equal("multiline-boundary.ds", diagnostic.Path);
            Assert.Equal(start, diagnostic.Span.Start);
            var prefix = input[..start];
            Assert.Equal(prefix.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).Length, diagnostic.Span.Line);
            Assert.Equal(start - prefix.LastIndexOfAny(new[] { '\r', '\n' }), diagnostic.Span.Column);
            Assert.InRange(diagnostic.Span.Length, 1, input.Length - start);
        }

        string[] malformed =
        [
            "let (\n left,\n) = (20, 22)\nleft",
            "let (\n left\n) = (20, 22)\nleft",
            "let (\n left,\n left\n) = (20, 22)\nleft",
            "let (\n (a, b),\n c\n) = ((1, 2, 3), 4)\na + b + c",
            "let pair: (\n int,\n ) = (20, 22)\n42"
        ];
        foreach (var body in malformed)
        {
            var input = "module Demo\nfn answer(): int {\n" + body + "\n}\n";
            var result = DSharpCompiler.Compile(input, "malformed-header.ds");
            Assert.True(result.CSharp is null, "Expected rejection: " + body);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("malformed-header.ds", diagnostic.Path);
            Assert.InRange(diagnostic.Span.Start, 0, input.Length - 1);
            Assert.InRange(diagnostic.Span.Length, 1, input.Length - diagnostic.Span.Start);
        }
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;

    private static Delegate Adapt(Type delegateType, Func<int,int> callback)
    {
        var value = Expression.Parameter(typeof(int), "value");
        return Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), value), value).Compile();
    }

    private static async Task WithGenerated(string csharp, Action<Type> verify)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-multiline-header-" + Guid.NewGuid());
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
            catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Generated multiline binding build timed out."); }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            var context = new AssemblyLoadContext("multiline-header-" + Guid.NewGuid(), isCollectible: true);
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
