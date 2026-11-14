# Issue 18: Scheduling and WebSocket lifecycle contracts

**Base:** `bb2da38280ab694e34120d0cd71290aac97dc2c6` (current main).

## Scope, steps and interfaces

Write DEC-08 from the supplied proposal and review clarifications. Preserve cron
delivery through queues and process-owned sockets. Record independently
reviewable scheduling and socket sections, boundary examples, acceptance checks
and dependent issue gates. No compiler, runtime, dependency, syntax or existing
decision changes. The decision record is the sole product change; this required
plan is the additional workflow artifact required by AGENTS.md.

Issue #18 is open and has no existing open PR at inspection. The merged compiler
bootstrap is available; runtime implementation prerequisites are not part of this
design task. Read DEC-04 and DEC-05 as proposals, without adopting either.

## Acceptance and verification

AC1 maps to the cron, timezone, identity and missed-occurrence tables in DEC-08.
AC2 maps to the transaction algorithm and competing/crashing scheduler examples.
AC3 maps to the socket ownership, buffering, cancellation and reconnect sections.
AC4 maps to separate adoption gates and separate dependent issues #70 and #86.
Their future test matrix is a specification, not executable runtime evidence.

Run `bash .github/ai/bootstrap.sh`, `bash .github/ai/verify.sh`,
`bash scripts/verify-compiler.sh` and `git diff --check`. Review local links and
examples against the current architecture and backlog. Record actual results
below and open one draft PR. Do not merge or start dependent implementation.

## Unresolved decisions

Both proposed contracts require independent explicit adoption. Exact D# syntax,
transport/provider packages, wire envelope, numeric endpoint limits and host
shutdown APIs remain outside this record; their requirements are identified
without selecting dependencies or inventing executable syntax.

## Results

`git diff --check` passed. Documentation verification passed: 59 documents and
177 local links. Installed pinned SDK 11.0.100 under `/tmp` and used a writable
npm cache without changing repository dependencies. Bootstrap then passed.
The required `verify.sh` passed 24 of 25 automation tests; the compiler resource
test failed because .NET attempts to create `/home/agent/.dotnet` on this
environment's read-only filesystem. `verify-compiler.sh` failed for the same
shared-memory initialization error before restore/build/test. No compiler test
or runtime behavior is claimed as verified. The draft PR must retain this
limitation for CI/reviewer verification; no trusted checks were modified.
