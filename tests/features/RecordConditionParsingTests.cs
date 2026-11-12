using DSharp.Compiler;
using Xunit;

namespace DSharp.Tests;

public sealed class RecordConditionParsingTests
{
    [Fact]
    public void ConditionRecordLiteralsAreDistinguishedFromIfBodies()
    {
        const string source = """
            module Demo
            type Flag { enabled: bool }
            type Empty { }
            fn named(): int {
                if Flag { enabled = true }.enabled { 1 } else { 0 }
            }
            fn empty(): int {
                if Empty { } == Empty { } { 1 } else { 0 }
            }
            fn ordinary(ready: bool): int {
                if ready { 1 } else { 0 }
            }
            """;
        var module = new Parser(Lexer.Lex(source, "conditions.ds"), "conditions.ds").Parse();
        var named = Assert.IsType<IfSyntax>(module.Functions[0].Body!.Result);
        var access = Assert.IsType<FieldAccessSyntax>(named.Condition);
        Assert.IsType<RecordConstructionSyntax>(access.Receiver);
        var empty = Assert.IsType<IfSyntax>(module.Functions[1].Body!.Result);
        var comparison = Assert.IsType<BinarySyntax>(empty.Condition);
        Assert.IsType<RecordConstructionSyntax>(comparison.Left);
        Assert.IsType<RecordConstructionSyntax>(comparison.Right);
        var ordinary = Assert.IsType<IfSyntax>(module.Functions[2].Body!.Result);
        Assert.IsType<ValueSyntax>(ordinary.Condition);
    }

    [Fact]
    public void ARecordOnlyModuleHasNoRequiredFunction()
    {
        var module = new Parser(Lexer.Lex("module Demo\ntype Flag { enabled: bool }")).Parse();
        Assert.Single(module.Records);
        Assert.Empty(module.Functions);
    }
}
