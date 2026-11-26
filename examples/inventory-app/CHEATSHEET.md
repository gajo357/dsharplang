# Inventory example quick reference

This reference matches the compiler in this checkout and its pinned
[.NET SDK](../../global.json). It describes only the example below. Consult the
[current implementation status](../../docs/features/DSharp_Implementation_Status.md)
and [syntax reference](../../docs/features/DSharp_Syntax_Reference.md) before
using other features; design examples can describe behavior not yet implemented.

## Immutable stock and capability functions

The [inventory source](inventory/stock.ds) declares an immutable record,
pure functions and an explicit copy update. `sell` returns a new snapshot;
`afterSale` and `originalUnchanged` exercise the resulting values:

<!-- source: inventory/stock.ds -->
```dsharp
module Inventory

type Stock {
    received: int
    sold: int
}

fn remaining(received: int, sold: int): int {
    received - sold
}

fn sell(stock: Stock, quantity: int): Stock {
    stock with { sold = stock.sold + quantity }
}

fn afterSale(received: int, sold: int, quantity: int): int {
    let before = Stock { received = received
                         sold = sold }
    let after = sell(before, quantity)
    remaining(after.received, after.sold)
}

fn originalUnchanged(received: int, sold: int, quantity: int): bool {
    let before = Stock { received = received
                         sold = sold }
    let after = sell(before, quantity)
    before.received == after.received && before.sold == sold && after.sold == sold + quantity
}
```

## Compose it from another file

The [summary source](app/summary.ds) imports that module, constructs its
`Stock` record, calls its copy-update function and computes the new balance:

<!-- source: app/summary.ds -->
```dsharp
module InventorySummary

import Inventory

fn availableAfterSale(received: int, sold: int, quantity: int): int {
    let before = Stock { received = received
                         sold = sold }
    let after = sell(before, quantity)
    remaining(after.received, after.sold)
}

fn available(): int {
    availableAfterSale(50, 5, 3)
}
```

From this directory, run the [example verifier](../../scripts/verify-application-example.sh):

```sh
bash ../../scripts/verify-application-example.sh
```

It checks and compiles both actual files, then executes `available()` and expects
42. It also checks `availableAfterSale`, `remaining` and `afterSale` with inputs
producing positive, zero and negative results and verifies the original stock
snapshot is unchanged.
This verifies those examples on the pinned compiler; it does not
demonstrate an actor runtime, storage, SDK packaging, or other language features.
