# Implementation Plan: Repowise integration

**Branch**: `004-repowise-integration` | **Date**: 2026-06-04 | **Spec**: [spec.md](./spec.md)

## Summary

Self-hosted Repowise 0.16+ with a single **secretless** `repowise-gate` job (index-only + health + risk + marker validation). No CI wiki / no Anthropic key in GitHub Actions. Local `repowise init` for MCP and optional LLM wiki.

## Repowise Documentation

**Status**: Complete

### CI

| Job | Secrets | Role |
|-----|---------|------|
| `repowise-gate` | None | Index-only, health/risk gates, marker validation, artifact upload |

### Local

- Index-only: `repowise init --index-only -y`
- Wiki + MCP: `repowise init -y` with `.repowise/.env` API key

### Human follow-up

1. Require `Repowise health gate (no LLM)` in branch protection.
2. Local MCP setup per [quickstart.md](./quickstart.md).
3. Tune `repowise/health-gates.yaml` from first green gate report if needed.
