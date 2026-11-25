# Inventory example

This example uses the repository compiler and the SDK pinned by [global.json](../../global.json).
Start with the current [implementation status](../../docs/features/DSharp_Implementation_Status.md),
[syntax reference](../../docs/features/DSharp_Syntax_Reference.md), and verified
[examples](../). Check existing conventions and feature availability before using
syntax or APIs from another language. The language design documents include
illustrative features that may not execute yet.

`inventory/` contains the capability's pure calculation; `app/` composes it.
Keep related definitions together, prefer immutable typed values and pure
functions, and add folders only when needed. The behavior test and build/check
commands are in [the example verifier](../../scripts/verify-application-example.sh).
From this directory, run:

```sh
bash ../../scripts/verify-application-example.sh
```

The verifier checks and compiles the actual D# files, then executes the generated
method and asserts its result. It does not demonstrate actor registration, scoped
resources, dispatcher invocation, durable submission or publication. Document
those distinctions when the application actually uses supported runtime features.
Keep this file project-owned when upgrading the SDK.
