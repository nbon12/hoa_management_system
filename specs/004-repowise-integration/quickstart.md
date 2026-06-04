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
  -x 'frontend/' \
  -x 'message-service-completed/' \
  -x 'neko-hoa/.angular/'
```

Excludes also live in [`.repowiseIgnore`](../../.repowiseIgnore) at repo root.

## Check before opening a PR

```bash
repowise status
repowise health
repowise risk origin/main..HEAD
repowise health --format json > /tmp/health.json
repowise risk origin/main..HEAD --format json > /tmp/risk.json
python .github/scripts/check-repowise-health-gates.py /tmp/gate.txt /tmp/health.json /tmp/risk.json
python .github/scripts/validate-repowise-markers.py
```

## Full wiki + MCP (local only — not in CI)

1. Copy [`repowise/.env.example`](../../repowise/.env.example) to `.repowise/.env` and paste your `ANTHROPIC_API_KEY`.
   ```bash
   mkdir -p .repowise
   cp repowise/.env.example .repowise/.env
   # edit .repowise/.env — paste key after ANTHROPIC_API_KEY=
   ```
2. Run `repowise init -y` (generates wiki pages and `.mcp.json`).
3. Restart Cursor/Claude after MCP registration, or run `repowise mcp`.

## Marker regions

Edit only content between `REPOWISE:START` / `REPOWISE:END` pairs. Follow [`repowise/generation-prompt.md`](../../repowise/generation-prompt.md).

## GitHub

- **Required:** `Repowise health gate (no LLM)` on PRs to `main` — no secrets.
- **Branch protection:** require that check after merge.
- **No CI wiki:** indexing for health/risk runs index-only in Actions; LLM wiki is local only.

## What's left for you

1. **Merge** the Repowise PR and enable branch protection on `repowise-gate`.
2. **Locally:** `repowise init --index-only -y` (or full `init` + MCP if you want token savings in Cursor).
3. **After big features:** update matching `REPOWISE:START` blocks per `generation-prompt.md`.
4. **Tune thresholds:** if gate fails, download `repowise-gate-report` artifact or run the gate script locally; adjust [`health-gates.yaml`](../../repowise/health-gates.yaml) or set `mode: warn` temporarily.
