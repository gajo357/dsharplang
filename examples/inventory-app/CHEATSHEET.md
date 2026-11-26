# Inventory example quick reference

This reference matches the compiler in this checkout and its pinned
[.NET SDK](../../global.json). It describes only the example below. Consult the
[current implementation status](../../docs/features/DSharp_Implementation_Status.md)
and [syntax reference](../../docs/features/DSharp_Syntax_Reference.md) before
using other features; design examples can describe behavior not yet implemented.

## A capability function

The [inventory source](inventory/stock.ds) declares a module and a pure function
with two `int` parameters and an `int` result:

<!-- source: inventory/stock.ds -->
```dsharp
module Inventory

fn remaining(received: int, sold: int): int {
    received - sold
}
```

## Compose it from another file

The [summary source](app/summary.ds) imports that module and calls its function:

<!-- source: app/summary.ds -->
```dsharp
module InventorySummary

import Inventory

fn available(): int {
    remaining(50, 8)
}
```

From this directory, run the [example verifier](../../scripts/verify-application-example.sh):

```sh
bash ../../scripts/verify-application-example.sh
```

It checks and compiles both actual files, then executes `available()` and expects
42. It also checks `remaining` with inputs producing positive, zero and negative
results. This verifies those examples on the pinned compiler; it does not
demonstrate an actor runtime, storage, SDK packaging, or other language features.
