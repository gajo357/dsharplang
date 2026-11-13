using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class MultiFileModuleTests
{
    [Fact(DisplayName = "Multi-file D# modules")]
    public async Task FilesResolveImportsAndPreserveOrigins()
    {
        var leftTokens = Lexer.Lex("module App\nfn run(): int { 42 }", "one.ds");
        var rightTokens = Lexer.Lex("module App\nfn run(): int { 42 }", "two.ds");
        Assert.Equal(leftTokens.Count, rightTokens.Count);
        for (var i = 0; i < leftTokens.Count; i++)
        {
            Assert.Equal(leftTokens[i], rightTokens[i]);
            Assert.Equal(leftTokens[i].GetHashCode(), rightTokens[i].GetHashCode());
        }

        // These are real D# source files: Math is imported only in the Billing
        // file that uses it; the other Billing file supplies a forward reference.
        var files = new[]
        {
            new SourceFile("math.ds", "module Lib.Math\nfn increment(value: int): int { value + 1 }"),
            new SourceFile("billing-a.ds", "module Billing\nimport Lib.Math\nfn answer(): int { increment(start()) }"),
            new SourceFile("billing-b.ds", "module Billing\nfn start(): int { 41 }"),
            new SourceFile("demo.ds", "module Demo\nimport Billing\nfn run(): int { Billing.answer() }")
        };
        foreach (var order in new[] { files, files.Reverse().ToArray() })
        {
            var compilation = DSharpCompiler.Compile(order);
            Assert.Empty(compilation.Diagnostics);
            await ExecuteGenerated(compilation.CSharp!, "Demo", "run", 42);
        }

        // A repeated import must not introduce an ambiguity.
        var repeated = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn value(): int { 42 }"),
            new SourceFile("app.ds", "module App\nimport Lib\nimport Lib\nfn run(): int { value() }")
        });
        Assert.Empty(repeated.Diagnostics);
        await ExecuteGenerated(repeated.CSharp!, "App", "run", 42);

        // Imports are file scoped, not transitive, and qualification does not
        // grant access to an unimported module or a sibling dotted module.
        AssertRejected(new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn value(): int { 42 }"),
            new SourceFile("app.ds", "module App\nfn run(): int { Lib.value() }")
        }, "app.ds", "value");
        AssertRejected(new[]
        {
            new SourceFile("base.ds", "module Base\nfn value(): int { 42 }"),
            new SourceFile("mid.ds", "module Mid\nimport Base\nfn number(): int { value() }"),
            new SourceFile("app.ds", "module App\nimport Mid\nfn run(): int { Base.value() }")
        }, "app.ds", "value");
        AssertRejected(new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn value(): int { 42 }"),
            new SourceFile("a.ds", "module App\nimport Lib\nfn a(): int { value() }"),
            new SourceFile("b.ds", "module App\nfn b(): int { value() }")
        }, "b.ds", "value");
        AssertRejected(new[]
        {
            new SourceFile("b.ds", "module A.B\nfn value(): int { 41 }"),
            new SourceFile("c.ds", "module A.C\nfn value(): int { 42 }"),
            new SourceFile("app.ds", "module App\nimport A.B\nfn run(): int { A.C.value() }")
        }, "app.ds", "value");

        var collision = DSharpCompiler.Compile(new[]
        {
            new SourceFile("first.ds", "module Billing\nfn total(): int { 1 }"),
            new SourceFile("second.ds", "module Billing\nfn total(): int { 2 }")
        });
        Assert.Null(collision.CSharp);
        Assert.NotEmpty(collision.Diagnostics);
        var collisionText = string.Join("\n", collision.Diagnostics.Select(d => d.Path + " " + d.Message));
        Assert.Contains("first.ds", collisionText);
        Assert.Contains("second.ds", collisionText);

        var ambiguous = DSharpCompiler.Compile(new[]
        {
            new SourceFile("a.ds", "module A\nfn format(): int { 1 }"),
            new SourceFile("b.ds", "module B\nfn format(): int { 2 }"),
            new SourceFile("app.ds", "module App\nimport A\nimport B\nfn run(): int { format() }")
        });
        Assert.Null(ambiguous.CSharp);
        var ambiguityText = string.Join("\n", ambiguous.Diagnostics.Select(d => d.Path + " " + d.Message));
        Assert.Contains("app.ds", ambiguityText);
        Assert.Contains("a.ds", ambiguityText);
        Assert.Contains("b.ds", ambiguityText);

        var qualified = DSharpCompiler.Compile(new[]
        {
            new SourceFile("a.ds", "module A\nfn format(): int { 41 }"),
            new SourceFile("b.ds", "module B\nfn format(): int { 1 }"),
            new SourceFile("app.ds", "module App\nimport A\nimport B\nfn run(): int { A.format() + B.format() }")
        });
        Assert.Empty(qualified.Diagnostics);
        await ExecuteGenerated(qualified.CSharp!, "App", "run", 42);

        const string selfQualified = "module App\nfn value(): int { 42 }\nfn run(): int { App.value() }";
        foreach (var compilation in new[]
        {
            DSharpCompiler.Compile(selfQualified, "self.ds"),
            DSharpCompiler.Compile(new[] { new SourceFile("self.ds", selfQualified) })
        })
        {
            Assert.Empty(compilation.Diagnostics);
            await ExecuteGenerated(compilation.CSharp!, "App", "run", 42);
        }
        AssertRejected(new[]
        {
            new SourceFile("self.ds", "module App\nfn value(): int { 42 }\nfn run(): int { let App = 1\n App.value() }")
        }, "self.ds", "value");

        // The current module wins over imports; a lexical value shadows a
        // module prefix and is never silently resolved as module qualification.
        var precedence = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn number(): int { 1 }"),
            new SourceFile("app.ds", "module App\nimport Lib\nfn number(): int { 42 }\nfn run(): int { number() }")
        });
        Assert.Empty(precedence.Diagnostics);
        await ExecuteGenerated(precedence.CSharp!, "App", "run", 42);

        // Types, generic functions and case patterns must share the same
        // module-resolution rules as ordinary function calls.
        var data = DSharpCompiler.Compile(new[]
        {
            new SourceFile("data.ds", "module Data\ntype Box<T> { value: T }\ntype Choice { | Found(int)\n | Missing }\nfn identity<T>(value: T): T { value }"),
            new SourceFile("app.ds", "module App\nimport Data\nfn run(): int { let b: Box<int> = Box { value = identity<int>(42) }\n let c = Found(b.value)\n match c { Found(n) => n\n Missing => 0 } }")
        });
        Assert.Empty(data.Diagnostics);
        await ExecuteGenerated(data.CSharp!, "App", "run", 42);

        var qualifiedData = DSharpCompiler.Compile(new[]
        {
            new SourceFile("data.ds", "module Data.Model\ntype Box<T> { value: T }\ntype Choice { | Found(int)\n | Missing }\nfn outcome(): Result<int, string> { Success(42) }"),
            new SourceFile("app.ds", "module App\nimport Data.Model\nfn run(): int { let b: Data.Model.Box<int> = Data.Model.Box { value = 42 }\n let c = Data.Model.Choice.Found(b.value)\n let answer = Data.Model.outcome()\n match c { Data.Model.Choice.Found(n) => match answer { Success(m) => n + m - 42\n Error(e) => 0 }\n Data.Model.Choice.Missing => 0 } }")
        });
        Assert.Empty(qualifiedData.Diagnostics);
        await ExecuteGenerated(qualifiedData.CSharp!, "App", "run", 42);

        AssertRejected(new[]
        {
            new SourceFile("cases.ds", "module App\ntype First { | Missing }\ntype Second { | Missing }"),
            new SourceFile("app.ds", "module App\nfn run(): int { let c = Missing\n 42 }")
        }, "app.ds", "Missing");
        AssertRejected(new[]
        {
            new SourceFile("cases.ds", "module App\ntype Choice { | Missing }\nfn Missing(): int { 42 }"),
            new SourceFile("app.ds", "module App\nfn run(): int { Missing() }")
        }, "app.ds", "Missing");
        AssertRejected(new[]
        {
            new SourceFile("cases.ds", "module Other\ntype Choice { | Missing }"),
            new SourceFile("app.ds", "module App\nfn run(): int { let c = Missing\n 42 }")
        }, "app.ds", "Missing");

        var importedCases = new[]
        {
            new SourceFile("left.ds", "module A\ntype Left { | Missing }"),
            new SourceFile("right.ds", "module B\ntype Right { | Missing }"),
            new SourceFile("app.ds", "module App\nimport A\nimport B\nfn run(): int { let c = Missing\n 42 }")
        };
        AssertRejected(importedCases, "app.ds", "Missing");
        var distinctCases = DSharpCompiler.Compile(new[]
        {
            importedCases[0], importedCases[1],
            new SourceFile("app.ds", "module App\nimport A\nimport B\nfn run(): int { let a = A.Left.Missing\n let b = B.Right.Missing\n match a { A.Left.Missing => match b { B.Right.Missing => 42 } } }")
        });
        Assert.Empty(distinctCases.Diagnostics);
        await ExecuteGenerated(distinctCases.CSharp!, "App", "run", 42);

        var functionAndCase = DSharpCompiler.Compile(new[]
        {
            new SourceFile("app.ds", "module App\ntype Choice { | Hit(int) }\nfn Hit(n: int): int { n }\nfn run(): int { let c = Choice.Hit(21)\n match c { Choice.Hit(n) => App.Hit(n + 21) } }")
        });
        Assert.Empty(functionAndCase.Diagnostics);
        await ExecuteGenerated(functionAndCase.CSharp!, "App", "run", 42);

        var generatedNameCollision = DSharpCompiler.Compile(new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn value(): int { 41 }"),
            new SourceFile("app.ds", "module App\nimport Lib\nfn run(): int { let __dsharp_module_symbol_0 = 1\n value() + __dsharp_module_symbol_0 }")
        });
        Assert.Empty(generatedNameCollision.Diagnostics);
        await ExecuteGenerated(generatedNameCollision.CSharp!, "App", "run", 42);
        AssertRejected(new[]
        {
            new SourceFile("cases.ds", "module A\ntype Choice { | Hit(int) }"),
            new SourceFile("app.ds", "module App\nimport A\nfn run(): int { let Choice = 42\n Choice.Hit(1) }")
        }, "app.ds", "Hit");
        AssertRejected(new[]
        {
            new SourceFile("cases.ds", "module A\ntype Choice { | Hit(int) }"),
            new SourceFile("app.ds", "module App\nimport A\nfn run(): int { let Choice = 1\n let x = Hit(42)\n match x { Choice.Hit(n) => n } }")
        }, "app.ds", "Choice");

        var duplicateType = DSharpCompiler.Compile(new[]
        {
            new SourceFile("one.ds", "module App\ntype Box { value: int }"),
            new SourceFile("two.ds", "module App\ntype Box { value: int }")
        });
        Assert.Null(duplicateType.CSharp);
        var duplicateTypeText = string.Join("\n", duplicateType.Diagnostics.Select(d => d.Path + " " + d.Message));
        Assert.Contains("one.ds", duplicateTypeText);
        Assert.Contains("two.ds", duplicateTypeText);

        AssertRejected(new[]
        {
            new SourceFile("lib.ds", "module Lib\nfn number(): int { 42 }"),
            new SourceFile("app.ds", "module App\nimport Lib\nfn run(): int { let Lib = 1\n Lib.number() }")
        }, "app.ds", "number");

        await VerifyCli(files);
    }

    private static void AssertRejected(SourceFile[] sources, string expectedPath, string token)
    {
        var compilation = DSharpCompiler.Compile(sources);
        Assert.Null(compilation.CSharp);
        var diagnostic = Assert.Single(compilation.Diagnostics, d => d.Path == expectedPath);
        Assert.StartsWith("DS", diagnostic.Code);
        var source = Assert.Single(sources, s => s.Path == expectedPath);
        Assert.InRange(diagnostic.Span.Start, 0, source.Text.Length - 1);
        Assert.InRange(diagnostic.Span.Length, 1, source.Text.Length - diagnostic.Span.Start);
        Assert.Contains(token, source.Text.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
    }

    private static async Task ExecuteGenerated(string csharp, string module, string name, int expected)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-multifile-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Generated.cs"), csharp);
            File.WriteAllText(Path.Combine(directory, "Generated.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
            File.WriteAllText(Path.Combine(directory, "global.json"), "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"disable\"}}");
            File.WriteAllText(Path.Combine(directory, "NuGet.Config"), "<configuration><packageSources><clear/></packageSources></configuration>");
            var result = await RunProcess("dotnet", directory, "build", "Generated.csproj", "--nologo", "--verbosity", "quiet");
            Assert.True(result.Exit == 0, result.Output);
            var context = new AssemblyLoadContext("multifile-" + Guid.NewGuid(), isCollectible: true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.Combine(directory, "bin/Debug/net10.0/Generated.dll"));
                var type = Assert.Single(assembly.GetTypes(), t => t.Name == module);
                Assert.Equal(expected, type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.Invoke(null, null));
            }
            finally { context.Unload(); }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task VerifyCli(SourceFile[] sources)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var cli = Path.Combine(root, "src/DSharp.Cli/DSharp.Cli.csproj");
        var directory = Path.Combine(Path.GetTempPath(), "dsharp-multifile-cli-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var source in sources) File.WriteAllText(Path.Combine(directory, source.Path), source.Text);
            var paths = sources.Select(source => Path.Combine(directory, source.Path)).ToArray();
            var check = await RunProcess("dotnet", root, new[] { "run", "--project", cli, "--no-restore", "-c", "Release", "--", "check" }.Concat(paths).ToArray());
            Assert.True(check.Exit == 0, check.Output);
            var output = Path.Combine(directory, "out");
            var compile = await RunProcess("dotnet", root, new[] { "run", "--project", cli, "--no-restore", "-c", "Release", "--", "compile" }.Concat(paths).Append(output).ToArray());
            Assert.True(compile.Exit == 0, compile.Output);
            Assert.True(File.Exists(Path.Combine(output, "Generated.cs")));
            var single = await RunProcess("dotnet", root, "run", "--project", cli, "--no-restore", "-c", "Release", "--", "check", paths[0]);
            Assert.True(single.Exit == 0, single.Output);
            var missing = await RunProcess("dotnet", root, "run", "--project", cli, "--no-restore", "-c", "Release", "--", "check", Path.Combine(directory, "missing.ds"));
            Assert.NotEqual(0, missing.Exit);
            Assert.Contains("DS0001", missing.Output);
            var invalidPath = Path.Combine(directory, "invalid.ds");
            File.WriteAllText(invalidPath, "module Demo\nfn bad(): int { missing }");
            var invalid = await RunProcess("dotnet", root, "run", "--project", cli, "--no-restore", "-c", "Release", "--", "check", paths[0], invalidPath);
            Assert.NotEqual(0, invalid.Exit);
            Assert.Contains(invalidPath, invalid.Output);
            Assert.Contains("DS", invalid.Output);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task<(int Exit, string Output)> RunProcess(string file, string directory, params string[] args)
    {
        var start = new ProcessStartInfo(file) { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(true); throw new TimeoutException("Multi-file test command timed out."); }
        return (process.ExitCode, await stdout + await stderr);
    }
}
