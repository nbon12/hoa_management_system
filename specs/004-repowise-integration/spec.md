# Feature Specification: Repowise integration

**Status**: In progress  
**Date**: 2026-06-04

## Summary

Complete self-hosted Repowise for the HOA Management Company monorepo: deterministic PR health gatekeeping (no LLM), optional docs/marker workflow, committed marker regions, and developer MCP setup.

## Requirements

1. PRs to `main` run `repowise-gate` without API secrets; job fails when `repowise/health-gates.yaml` thresholds are breached.
2. PRs optionally run `repowise-docs` for wiki update and marker validation (`continue-on-error` until marker LLM sync is automated).
3. Marker regions exist per `repowise/generation-prompt.md` catalog.
4. `.repowise/` remains gitignored; excludes via `.repowiseIgnore` and CI `-x` flags.
5. Developers can run `repowise init --index-only` locally before push; full `repowise init` + MCP when API keys are configured.

## Success criteria

- `repowise doctor` passes after local `repowise init --index-only`.
- GitHub Actions `repowise-gate` job is green on representative PRs.
- `validate-repowise-markers.py` passes for all marker files.
