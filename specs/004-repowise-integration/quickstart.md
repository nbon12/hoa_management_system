# Repowise quickstart (HOA Management Company)

## Install

```bash
pip install "repowise>=0.16.0"
# or: pipx install "repowise>=0.16.0"
```

## Index without LLM (matches CI gate)

```bash
cd /path/to/HOAManagementCompany
repowise init --index-only -y \
  -x frontend/ \
  -x message-service-completed/ \
  -x neko-hoa/.angular/**
```

## Check before opening a PR

```bash
repowise status
repowise health
repowise risk origin/main..HEAD
python .github/scripts/check-repowise-health-gates.py /tmp/gate.txt /tmp/health.json /tmp/risk.json
```

Generate JSON for the gate script:

```bash
repowise health --format json > /tmp/health.json
repowise risk origin/main..HEAD --format json > /tmp/risk.json
```

## Full wiki + MCP (optional)

1. Add provider key to `.repowise/.env` (gitignored), e.g. `ANTHROPIC_API_KEY`.
2. Run `repowise init -y` (generates wiki pages and `.mcp.json`).
3. Restart Cursor/Claude after MCP registration, or run `repowise mcp`.

## Marker regions

Edit only content between `REPOWISE:START` / `REPOWISE:END` pairs. Follow [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md).

Validate:

```bash
python .github/scripts/validate-repowise-markers.py
```

## GitHub

- **Required**: `repowise-gate` job — no secrets.
- **Optional**: add `ANTHROPIC_API_KEY` (or `OPENAI_API_KEY`) repo secret for `repowise-docs` LLM wiki updates.
- **Branch protection**: require `Repowise health gate (no LLM)` check on `main`.
