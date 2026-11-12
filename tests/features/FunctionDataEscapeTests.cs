using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class FunctionDataEscapeTests
{
    public void GenericCallsCannotHideFunctionsInTemporaryData()
    {
        var bodies = new[]
        {
            """
            type Box<T> { value: T }
            fn sink<T>(value: T): Unit { let box = Box { value = value }
                () }
            fn run(): int { sink((x: int) => x)
                42 }
            """,
            """
            type Box<T> { value: T }
            fn sink<T>(value: T): Unit { let box = Box { value = value }
                () }
            fn forward<T>(value: T): Unit { sink(value) }
            fn run(): int { forward((x: int) => x)
                42 }
            """,
            """
            type Choice<T> { | Wrapped(T) }
            fn sink<T>(value: T): Unit { let wrapped = Choice.Wrapped(value)
                () }
            fn run(): int { sink((x: int) => x)
                42 }
            """,
            """
            fn sink<T>(value: T): Unit { let wrapped = Some(value)
                () }
            fn forward<T>(value: T): Unit { sink(value) }
            fn run(): int { forward((x: int) => x)
                42 }
            """
        };

        for (var index = 0; index < bodies.Length; index++)
        {
            var result = DSharpCompiler.Compile("module Demo\n" + bodies[index], "function-data-escape.ds");
            Assert.True(result.CSharp is null, $"Generic function escape scenario {index} emitted C#.");
            var error = Assert.Single(result.Diagnostics);
            Assert.StartsWith("DS", error.Code);
            Assert.Equal("function-data-escape.ds", error.Path);
        }
    }
}
