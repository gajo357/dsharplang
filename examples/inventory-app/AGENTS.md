# Inventory example

This example uses the repository compiler and the SDK pinned by [global.json](../../global.json).
Start with the [example quick reference](CHEATSHEET.md), current [implementation status](../../docs/features/DSharp_Implementation_Status.md),
[syntax reference](../../docs/features/DSharp_Syntax_Reference.md), and the
verified [inventory](inventory/stock.ds) and [summary](app/summary.ds) sources.
Check existing conventions and feature availability before using
syntax or APIs from another language. The language design documents include
illustrative features that may not execute yet.
When a syntax or API change affects this guidance or its examples, update the
affected documentation and sources in the same change and rerun verification.

`inventory/` contains the capability's pure calculation; `app/` composes it.
Keep related definitions together, prefer immutable typed values and pure
functions, and add folders only when needed. The behavior test and build/check
commands are in [the example verifier](../../scripts/verify-application-example.sh).
From this directory, run:

```sh
bash ../../scripts/verify-application-example.sh
```

The verifier checks and compiles the actual D# files, then executes the generated
summary, cross-module stock calculation, inventory arithmetic and immutable
stock update with positive, zero and negative results. It checks that the
original snapshot remains unchanged. It
also independently observes both the original and returned generated records
after calling `sell` with a separate input. It
does not demonstrate actor registration, scoped
resources, dispatcher invocation, durable submission or publication. Document
those distinctions when the application actually uses supported runtime features.
Keep this file project-owned when upgrading the SDK.
