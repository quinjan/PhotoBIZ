# PhotoBIZ Agent Instructions

Before making implementation changes in this repository, read these documents in order:

1. `docs/ARCHITECTURE.md`
2. `docs/PRD.md`
3. `docs/CODING_GUIDELINES.md`

`docs/ARCHITECTURE.md` is the source of truth for platform boundaries, technology decisions, state machines, tenant isolation, and implementation phases. If another document conflicts with it, follow the architecture document and call out the conflict.

## Default Agent Workflow

- Keep changes scoped to the requested feature, bug, or documentation task.
- Prefer small vertical slices that include API behavior, persistence, UI, realtime updates, and tests only when those parts are needed for the requested workflow.
- Preserve tenant isolation in every client-scoped path.
- Treat backend state as authoritative for transactions, payments, booth state, subscriptions, and agent commands.
- Add or update focused tests for meaningful behavior changes.
- Do not introduce new frameworks, package managers, hosting patterns, or architecture layers without updating `docs/ARCHITECTURE.md` and explaining the decision.
- Do not accept arbitrary tenant CSS, JavaScript, HTML, SQL, command strings, or file paths.

## Pull Request Expectations

- Explain what changed, why it changed, and how it was validated.
- Mention any guideline exception clearly.
- Keep generated files, screenshots, and formatting-only churn out of PRs unless they are part of the request.
- If tests or checks cannot be run, say exactly why.
