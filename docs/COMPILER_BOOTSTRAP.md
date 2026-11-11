# Executable compiler bootstrap

## Approved boundary

The setup task authorizes a C# compiler, hand-written lexer/recursive-descent
parser, syntax tree, binder with a typed model, readable C# generation and SDK
compilation. It does not authorize the full language or a runtime framework.
The syntax handoff's selected requirements supersede older conflicting baseline
examples; provisional syntax outside this slice is not silently finalized.

The implemented grammar is:

```text
file     = "module" identifier function+ EOF
function = "fn" identifier "(" ")" ":" "int" "{" decimal-integer "}"
```

Identifiers use ASCII letters/underscore followed by ASCII letters/digits/underscore;
language keywords cannot be identifiers. Decimal literals in this slice are
nonnegative digits in 0..2147483647. Negative/suffixed/hex literals, parameters,
imports, expressions, block comments and multiple modules are explicitly unsupported,
not decisions rejecting those features for the eventual language. Whitespace and
`//` comments are accepted. Every source token must be consumed. Multiple uniquely
named functions are supported within the single module. Public generated methods
are accessible through `DSharp.Generated.Module.function()`. A generated base
container avoids C# type/member collisions without restricting D# names; CLR
layout and development CLI names are provisional implementation details.

## Pins and reproducibility

`global.json` pins .NET SDK 10.0.100 with roll-forward disabled and net10.0 targets.
No compiler/runtime NuGet dependencies are needed. Tests use xUnit 2.9.3,
xunit.runner.visualstudio 3.1.5 and Microsoft.NET.Test.Sdk 17.14.1, with exact direct
version ranges and checked-in `packages.lock.json` transitive versions/content hashes.
These provide ordinary VSTest discovery and TRX rather than a custom test runner.
Dependency changes require separate human review. The Unlicense is preserved.

Run `bash scripts/verify-compiler.sh` for locked restore, Release build, actual
VSTest execution, and strict nonzero/all-passed TRX validation. Reports are in
`artifacts/compiler/`; `COMPILER_REPORT_DIR` selects another output directory.
The script fails on missing/zero/skipped/failed or inconsistent results.
Run the existing `.github/ai/bootstrap.sh` and `.github/ai/verify.sh` separately
for documentation and automation regressions. They are unchanged by this PR.

## Acceptance evidence mapping

| Criterion | Executable evidence |
| --- | --- |
| Restore/build | verify-compiler.sh, locked package restore and Release solution build |
| Parse/type resolution | ParserPreservesFunctionAndLiteral, SemanticBindingResolvesIntBeforeEmission |
| Generated execution = 42 | GeneratedCodeBuildsAndReturns42 builds an SDK project, loads its assembly and invokes the public method; includes Demo.answer |
| CLI | documented check/compile commands, smoke-tested with examples/answer.ds and invalid input |
| Diagnostics | InvalidSourceHasDSharpDiagnostic and DiagnosticPointsAtOriginalToken; missing brace DS1002, unknown character DS1001, duplicate DS2001, non-int DS2002, return type DS2003, overflow DS2004 |
| Layered tests | lexer, parser, binder, deterministic emitter and generated execution tests in CompilerTests.cs |
| Regression/discovery | strict TRX parser plus unchanged docs/automation suite |
| Decisions/status | this file, AGENTS.md and baseline precedence notes |

Diagnostics contain a code, source path and UTF-16 offset/length plus 1-based
line/column. The initial compiler reports the first source error. Unsupported
constructs fail explicitly rather than being ignored or handed to C# diagnostics.
CLI usage errors exit 2, source/I/O failures exit 1, SDK build failures propagate.

## Deferred

No runtime, actors, queues, imports, general inference, nullable/result types,
lambdas, records, interfaces, interop or language server are implemented. Preserve
the handoff's established behaviour when adding later slices. Numeric extensions,
formal newline grammar, async/function-type spelling, generic disambiguation,
worker fault wrapper, queue lifecycle, serialization, middleware, authorization
capabilities and safe .NET interop require their own frozen contracts/tests.
Code-writing automation is a separate reviewable change after this baseline.
