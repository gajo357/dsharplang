# Issue 211: Bare immutable extension method pipes

**Issue:** [#211](https://github.com/gajo357/dsharplang/issues/211)
**Base:** `0fa6ff7`, including merged PR #210.
**Dependencies:** The adopted pipe specification in #129 and the earlier method-value and pipe work, including #197, #199, #203, and #209.

## Goal and approach

Allow a pipe to target a visible immutable extension method directly, as in `fee |> invoice.add`. Select a unique applicable method using the incoming value and available output context, then reuse the existing method-value and pipe lowering so the flowing value is evaluated once before the receiver and method execution waits for saturation. Preserve existing field and built-in precedence, overload ambiguity, nullable behavior, visibility, asynchronous behavior, and capability restrictions. Keep omitted-return inference conservative where dependencies are not established.

The adopted pipe specification explicitly permits this form. Four older negative fixture inputs contradict that specification: change only `2 |> 40.add` to the genuinely wrong-typed `true |> 40.add` in each of ExtensionMethodValueTests, CollectionMethodPipeTests, BareCollectionPipeInferenceTests, and GroupedMethodValueTests. Keep their surrounding assertions and all other existing tests intact.

## Verification

Add executable D# examples and focused tests for successful and rejected pipes, staged application, evaluation order, overloads, nullability, precedence and safety. Keep each match arm on a separate line. Run the repository bootstrap, `scripts/verify-compiler.sh`, and `.github/ai/verify.sh`; record any environment limitations. Open one draft PR for this issue and fix any CI failures on the same branch.

## Files and acceptance mapping

- `src/DSharp.Compiler/Compiler.cs`: resolve pipe-context extension groups and reuse the bound method-value and pipe paths; preserve diagnostics, imports and ownership checks.
- `tests/features/BareExtensionPipeTests.cs` and `examples/bare_extension_pipes.ds`: exercise ordinary D# pipes, staged invocation, evaluation order, nullable and overload selection, ambiguity, asynchronous results, collisions and rejected capability captures. Existing negative tests retain wrong-type coverage through the four targeted fixture replacements above.
- `docs/features/DSharp_Implementation_Status.md`: describe the implemented boundary and any remaining inference limitations.

Commands: `PATH=/workspace/work/dotnet11:$PATH npm_config_cache=/tmp/dsharp-npm-cache bash .github/ai/bootstrap.sh`; `bash scripts/verify-compiler.sh`; `bash .github/ai/verify.sh`, with writable temporary runtime, NuGet and npm directories when required. Verify the example through the D# CLI and inspect generated code if the test harness does not already do so. Unresolved decisions: none; the scope follows the adopted #129 contract without introducing untyped standalone method values.

## Results

- Bootstrap passed using the pinned SDK and a writable npm cache.
- `scripts/verify-compiler.sh` passed: 18 compiler tests and 379 feature tests, with a clean Release build.
- The focused feature test compiled generated C# with warnings as errors and executed ordering, deferred-failure, asynchronous, nullable and overload cases. The example also passed the D# CLI `check` command.
- `.github/ai/verify.sh` passed documentation validation and all 25 automation tests. Its final isolated compiler stage could not run locally: pulling the pinned .NET SDK Docker image failed with `Forbidden`. GitHub CI must provide the isolated verification result.
- The four old rejection inputs were updated as described above; all surrounding assertions remain in place.
- GitHub's first compiler run passed all 379 tests but rejected the TRX as oversized. The new feature test is registered through the existing feature aggregate, preserving every assertion and the trusted report-size verifier while reducing report entries.
