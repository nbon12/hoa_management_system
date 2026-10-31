---
name: architecture-police
description: Find places where this repo solves the same problem two or more ways, consolidate the most valuable one into a single shared implementation without changing behavior, and open and merge one PR. Run nightly by the "Architecture Police" scheduled task; can also be run by hand with /architecture-police.
disable-model-invocation: true
---

# Architecture Police (hoa_management_system)

Find near-duplicate architectural patterns: two or more implementations of the same mechanism,
written separately. Pick the most valuable one, merge the copies into one implementation, and
change no behavior. Ship it as one PR, and merge the PR once every check is green.

This runs unattended. **Never ask the user anything.** Decide from the code, the constitution
and the test results, and explain each decision in the PR description.

## What counts as a near-duplicate

Copies that do the same job and would change for the same reason. Typical examples in this repo:

- **Backend.** The same logic repeated across `Features/*` when it belongs in `Features/Common`
  or `Infrastructure/`. Examples:
  - community-scope or authorization checks;
  - `limit`/`offset` pagination handling;
  - mapping errors to responses;
  - outbox enqueueing;
  - options registration and validation;
  - clock or `TimeProvider` handling;
  - storage key building;
  - email composition;
  - FluentValidation rules for the same input shape;
  - EF query filters;
  - FastEndpoints endpoint boilerplate.
- **Frontend.** Services that call the same endpoints, components with near-identical templates
  or logic, repeated signal or state handling, and error or refusal message mapping. Also any
  formatting, date or time-zone helper that duplicates one in `neko-hoa/src/app/shared` or
  `core`.
- **Test support.** Duplicated factories, fixtures, builders and setup helpers. These belong in
  `HOAManagementCompany.Tests/Factories` and `Fixtures`. Never delete or merge test *cases*: the
  "Nightly Unused Test Cleanup" scheduled task owns that.
- **Drift from a canonical piece.** A shared implementation already exists, but code elsewhere
  re-implements it instead of calling it.

These are **not** targets:

- **Coincidental similarity.** Code that looks alike but encodes business rules that will change
  independently, such as payment versus ARC deadlines. If one implementation would need flags
  or mode parameters to serve both callers, the copies are not duplicates.
- **Duplication the constitution or a spec requires**, such as DTOs that are separate API
  contracts.
- **Generated, owned-elsewhere or excluded files** (see below).

## Hard limits

- **No behavior change.** Leave all of these exactly as they are:
  - HTTP routes, verbs, request and response shapes, status codes, error codes and types, and
    the generated OpenAPI;
  - the DB schema and migrations;
  - config keys and environment variables;
  - Serilog message templates and property names;
  - outbox message types and payloads;
  - frontend routes, visible UI text and layout.
- **Never edit:**
  - `HOAManagementCompany/Infrastructure/Persistence/Migrations/**`, including the model snapshot;
  - `*.Designer.cs`;
  - lockfiles;
  - `specs/**`, `.specify/**`, `wireframes/**`, `repowise/**`, `.claude/**`, `.cursor/**`;
  - `docs/**`, except your patch-notes entry.
- **Report only, never change:**
  - `infra/**`. A merge to `main` triggers `infra-apply`, and moving OpenTofu resources changes
    state addresses.
  - `.github/**`, `Dockerfile` and `docker-compose.yaml`, which hold CI and deploys.

  List duplicates you find there in the PR description.
- No new packages.
- Never delete, skip or weaken a test or assertion. Change tests only to follow symbols you
  moved or renamed, or to add coverage for the consolidated code.
- **One theme per PR.** Constitution §13 says broad mixed-scope PRs must be split. A theme may
  touch many call sites. Aim for under about 800 changed lines, not counting tests. For a bigger
  theme, do one coherent slice and list the rest as follow-up.
- Never push to `main`. Never force-push a branch you did not create. Never disable, skip or
  bypass a check.

## Procedure

### 1. Orient

- Read `CLAUDE.md`, plus these sections of `.specify/memory/constitution.md`:
  - §4 Backend Principles;
  - §6 Frontend Principles;
  - §9 Quality & Code Standards;
  - §12 Spec Independence & Parallelism;
  - §13 Governance & Amendments;
  - the testing constitution after them.

  Also read the `docs/patch-notes.md` entries from the last two weeks.
- If the working tree is dirty, stop. Otherwise run `git checkout main && git pull --ff-only
  origin main`.
- Use the GitHub MCP tools (`mcp__github__*`, loaded with ToolSearch) for every GitHub action.
  `gh` is not authenticated in cloud sessions.

### 2. Finish earlier runs first

- List open PRs whose head branch starts with `architecture-police/`.
- If you find one:
  1. Update it from `main`.
  2. Fix anything that fails, staying within its theme.
  3. Merge it if it goes green (step 7).
- If it cannot go green:
  - if it is more than 3 days old, close it with a comment saying why, and continue;
  - otherwise, leave it open, add a comment saying what's blocking it, and stop for this repo
    tonight.

  Do not stack a second refactor on one that is still pending.

### 3. Find candidates

Use several signals. No single one is enough.

- **Copy-paste detection.** Run the clone finder next to this file. It uses only the Python
  standard library, so it needs no install or download. Run it in both modes:

  ```bash
  F=.claude/skills/architecture-police/find_clones.py
  python3 -I $F HOAManagementCompany HOAManagementCompany.Tests neko-hoa/src
  python3 -I $F --fuzzy --min-lines 10 HOAManagementCompany neko-hoa/src
  ```

  The first run finds literal copies. The second (`--fuzzy`) also finds copies with renamed
  identifiers or changed literals. Neither finds the same idea written differently. Treat each
  cluster as a lead, read the code, and drop clusters that are only test cases or
  coincidental.
- **Structural comparison.** Compare sibling features (`HOAManagementCompany/Features/*` and
  `neko-hoa/src/app/features/*`) for the same shape. Look for:
  - re-declared private helpers;
  - repeated constants and regexes;
  - the same sequence of calls;
  - classes whose names differ only by a feature prefix.
- **Canonical drift.** For each type in `Features/Common`, `Infrastructure/` and
  `neko-hoa/src/app/shared` and `core`, search for code that re-implements it.
- **Repowise MCP**, if this session has it. Follow
  `.claude/rules/repowise-strict-embeddings.md`. Cloud sessions usually lack it. If so, say so
  in the report and carry on with the signals above.

Stay clear of in-flight work. List the open PRs and the files each one changes. Drop any
candidate that would touch a file another open PR changes.

### 4. Pick one theme

Rank candidates in this order:

1. Security, tenancy, authorization or money logic implemented more than one way.
2. Re-implementations of an existing shared piece. Point the callers at that piece.
3. Copy-paste clusters with three or more copies.
4. Pairs.

Within a rank, prefer more lines removed and fewer layers touched.

If the copies **differ** in behavior today, merging them would change behavior. A difference may
be a real bug, but do not fix it silently. Record it under "Findings to review" in the PR
description, and pick another theme.

Choose the canonical form this way:

- Use the existing shared or most-used implementation, and the pattern the constitution
  prescribes.
- Put new shared code where its kind already lives:
  - feature-level backend code in `Features/Common`;
  - cross-cutting infrastructure in `Infrastructure/`;
  - UI pieces in `neko-hoa/src/app/shared`;
  - services and models in `core`;
  - test helpers in `HOAManagementCompany.Tests/Factories` or `Fixtures`.

If nothing clears the bar, stop: no branch and no PR. That is a good outcome.

### 5. Check the baseline

Do this on unmodified `main`, before editing.

- **Frontend.** Run `cd neko-hoa && npm ci && npm run test:ci`. If Karma cannot find a browser,
  point `CHROME_BIN` at the preinstalled Chromium, such as
  `/opt/pw-browsers/chromium-*/chrome-linux64/chrome`.
- **Backend.** Run `dotnet build` and `dotnet test` if the .NET 9 SDK is installed. The
  integration tests also need a working Docker daemon for Testcontainers. Cloud sessions
  usually have no SDK and cannot download one. In that case the PR's `test.yml` CI run is the
  backend gate. Confirm that `main`'s latest `test.yml` run is green
  (`mcp__github__actions_list`).
- If `main` is red, change nothing in this repo and say so in the report.

### 6. Consolidate

1. Create the branch `architecture-police/<YYYY-MM-DD>` from the latest `main`.
2. Write the shared implementation, or choose the canonical existing one. Move every copy in the
   theme onto it and delete the copies. Leave no forwarding wrappers unless a public symbol must
   survive.
3. Add unit tests for any new shared code. Follow the testing constitution: factories, and
   Theories for data-varied cases. Added and changed files must reach the 95% coverage gate
   (§9).
4. Build and run every check you can locally: the step 5 commands, plus `npm run build` when the
   frontend changed. Fix until green. If it cannot go green without a behavior change, abandon
   the theme, open no PR, and report why.
5. Commit as `refactor(architecture-police): <theme>`.

### 7. Open the PR and merge it

1. Push with `git push -u origin <branch>`.
2. Open a draft PR against `main` titled `refactor(architecture-police): <theme> (<YYYY-MM-DD>)`.
   Use these sections:
   - **Summary**.
   - **Duplicate pattern**: each copy as `file:line`, and how the copies matched.
   - **Consolidation**: the canonical location, what was deleted, and why behavior is unchanged
     for each caller.
   - **Verification**: commands run with their results. Name anything left to CI, and why.
   - **Next candidates**: other ranked candidates with paths. The next run starts here.
   - **Findings to review**: report-only duplicates under `infra/` and `.github/`, and copies
     that differ in ways that look like bugs.
3. Add an entry at the top of `docs/patch-notes.md` in the file's existing format. Title it
   "Architecture Police: \<theme\>", link the PR, and give it these sections:
   - **Shared pieces to reuse**: the canonical piece and where it lives.
   - **Changes earlier assumptions**: the removed variants, so nobody re-adds them.

   Say "No behavior change." Commit and push.
4. Once local checks pass, mark the PR ready for review. Then wait for every GitHub check to
   finish. Subscribe to the PR's activity and end your turn: check events wake the session. Do
   not poll with `sleep`. If a check fails, fix it within the theme, push, and wait again.
5. **Merge with a squash merge, and delete the branch, only if all of the following hold:**
   - every check has completed and none failed or is pending (skipped or neutral is fine);
   - the branch is up to date with `main`. If `main` moved, update the branch and wait for the
     checks again;
   - the diff touches no path from the "Never edit" or "Report only" lists.
6. If the PR cannot go green, leave it open as a draft with a comment explaining what fails. Do
   not merge it.

### 8. Report

Give one short summary: the theme, the PR link, whether it merged (and why or why not), net
lines removed, and the top remaining candidates.

## Schedule

The "Architecture Police" scheduled task runs this playbook nightly at 4:41 AM
America/New_York, together with the matching playbook in `nbon12/mmo-engine`. It runs after
"Dependabot PR Review & Merge" and "Nightly Unused Test Cleanup" have started, so it works from
a freshly cleaned `main`. To change what the task does, edit this file. The task reads it on
every run.
