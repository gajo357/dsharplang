# Pipes and logging specification reconciliation

Base: `c87287e` on main, including merged tuple PR #128. The user requested
importing the supplied specification, creating issues and updating repository
documents. This task changes specifications and tracking, not compiler/runtime
behavior, tests, dependencies or verification.

## Scope and evidence

- Import the supplied revision 1.0 as `docs/features/DSharp_Pipes_and_Logging.md`,
  retaining semantics and defaults while fixing repository-relative links and
  adding issue mappings.
- Reconcile syntax, implementation instructions, backlog, sequence, task queue
  and cross-document precedence/status links. Historical completed feature plans
  remain evidence of their original scope.
- Create #129–#134 for LANG-41, LIB-04 and LOG-01–04; #135/#136 are early core
  partial-application and synchronous-Result children. Parent completion retains
  the later async/method/lifetime matrix and runtime/setup gates.
- Update existing #15, #17, #35, #54, #56, #58, #61, #78, #83, #91, #92, #93
  and queue tracker #101. Keep #35 closed; assign adapters to #56/#61, callable
  HTTP preprocessing to #83 and instrumentation using Logger to #91.
- Avoid the observer/search dependency cycle: #134 depends on #92, not vice
  versa. Record the scoped stale-arity assertion migration before #135 dispatch.
- Validate Markdown links, changed-file scope and diff formatting; run the
  repository's required checks and record actual results. Passing checks does
  not imply implementation of this target.

## Remaining implementation design detail

Future slice plans must define concrete LogEvent/field/query/error types,
codec numeric limits and declarations for overridable operational settings.
These details must preserve the supplied semantics, null/Option distinction,
bounded delivery defaults and explicit setup approvals. Other unadopted design
decisions (including DEC-01) remain open.

## Validation results

- Documentation validation passed for 43 files and 129 local links; diff checks
  passed. Independent review found no blocking contract or dependency conflicts.
- Bootstrap and compiler verification passed: 18 baseline and 358 feature tests.
  Source code, project configuration, tests and verification remain unchanged.
- Full verification passed documentation and all 20 automation tests. Its
  isolated compiler stage was blocked by a `Forbidden` response while pulling
  the pinned Docker image; GitHub Actions runs that unchanged check.
