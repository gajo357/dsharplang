# Application guidance

This is a scaffold source, not an installed or upgrade-managed file. Keep the
application's copy project-owned and preserve its edits during SDK upgrades.

Read the documentation and verified examples matching this project's D# compiler
or SDK version. Check the project's conventions and feature availability before
using syntax or APIs familiar from another language. Organize related definitions
by business capability; prefer immutable typed data and pure functions where
supported. Avoid creating unused folders.

Locate the project's source, tests and documentation before editing. Use the
check, build and test commands documented by that project, and verify behavior
at the level claimed. Do not infer a runtime capability from a compiler check.
When runtime capabilities are available, document explicit registration and
scoped resources, and distinguish invocation, durable submission and publication.
