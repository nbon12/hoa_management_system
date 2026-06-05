# Feature Specification: Repowise integration

**Status**: In progress  
**Date**: 2026-06-04

## Summary

Self-hosted Repowise for the HOA Management Company monorepo: deterministic PR health gatekeeping (no LLM, no CI wiki), committed marker regions, and local developer MCP/wiki setup.

## Requirements

1. PRs to `main` run `repowise-gate` without API secrets; job fails when `repowise/health-gates.yaml` thresholds are breached.
2. Marker regions validated on each PR (`validate-repowise-markers.py`).
3. Marker regions exist per `repowise/generation-prompt.md` catalog.
4. `.repowise/` remains gitignored; excludes via `.repowiseIgnore` and CI init flags.
5. Full wiki + MCP: **local only** (`repowise init` with key in `.repowise/.env`).

## Success criteria

- `repowise doctor` passes after local `repowise init --index-only`.
- GitHub Actions `repowise-gate` job is green on representative PRs.
- `validate-repowise-markers.py` passes for all marker files.
