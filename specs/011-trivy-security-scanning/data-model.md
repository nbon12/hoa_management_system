# Phase 1 Data Model: Trivy Security Scanning Pipeline

This feature introduces no database schema, migrations, or runtime entities. The "entities" are
**configuration objects** that govern scan behavior. They are documented here because the spec's Key
Entities map to concrete, validatable configuration artifacts.

## Configuration objects

### 1. Severity Policy

The single source of truth for which findings fail the build.

| Field | Value (current phase) | Where defined | Validation |
|-------|----------------------|---------------|------------|
| `failing_severities` | `CRITICAL,HIGH` | `env.TRIVY_SEVERITY` in `security-scan.yml` (and reused by the `docker-push` scan step) | Referenced by every Trivy step; changing it in one place changes the whole pipeline (FR-007) |
| `reported_severities` | all (`CRITICAL,HIGH,MEDIUM,LOW`) | SARIF pass uses unfiltered/severity-broad reporting | MEDIUM/LOW appear in SARIF/logs but never set a non-zero exit (FR-006) |
| `gate_exit_code` | `1` | Trivy gating pass `exit-code` | Non-zero ⇒ pipeline stops |
| `ignore_unfixed` | `true` (image scan only) | `--ignore-unfixed` on `trivy image` | Findings with no upstream fix are reported, not failed (FR-015) |

**State**: static config. A future tightening (e.g. add `MEDIUM`) is a one-line change to
`TRIVY_SEVERITY`.

### 2. Vulnerability Allowlist (`.trivyignore`)

Reviewed, documented exceptions kept at the repo root.

| Field | Description | Rules |
|-------|-------------|-------|
| entry | One CVE/misconfig ID per line (e.g. `CVE-2025-12345`, `AVD-AWS-0089`) | MUST be accompanied by a `# reason — owner — review/expiry` comment |
| scope | Applies to whichever scan references the file (image and/or config) | An entry suppresses *only* that exact ID; severity policy still applies to everything else |
| lifecycle | Added via PR review; removed when the upstream fix ships or the exception expires | Removing an entry restores enforcement for that ID (edge case in spec) |

**Validation**: an allowlisted finding remains visible in SARIF/logs but does not fail the build.
Empty/missing `.trivyignore` ⇒ no exceptions (default-deny on nothing).

### 3. Action Pin Registry

The set of third-party actions referenced by the scanning workflow, each pinned to an immutable SHA.

| Field | Description | Rules |
|-------|-------------|-------|
| `uses` | `owner/repo@<40-char-sha>` | MUST be a full commit SHA, never a branch or floating tag (FR-008) |
| version comment | trailing `# vX.Y.Z` | Human-readable version the SHA corresponds to |
| update path | `pinact` / `gh api` resolves tag→SHA; Dependabot (`github-actions`) proposes bumps | Bumps arrive as PRs that must themselves pass the scan |

**Validation**: a reviewer can grep the workflow and confirm no `@<branch>` or `@v<n>`-only refs
remain among third-party actions (SC-003).

### 4. SARIF Result Stream

The machine-readable output uploaded to GitHub Code Scanning.

| Field | Description |
|-------|-------------|
| `category` | `trivy-iac` for the config scan, `trivy-image` for the image scan — distinct so alerts don't overwrite each other |
| destination | Code Scanning alerts: PR annotations on `pull_request`, Security tab on `push`/`schedule` |
| fallback | Always also uploaded as a build artifact, so findings survive even when Code Scanning is unavailable |

## Scan Stage state transitions

```text
[IaC config scan]
  infra/ absent or empty ──────────────► PASS (no-op)            (FR-009)
  fixable CRIT/HIGH misconfig ─────────► FAIL (stop before build)(FR-001, FR-005)
  only MED/LOW or allowlisted ─────────► PASS (reported)         (FR-006, FR-015)
        │ pass
        ▼
[Build image locally (load, no push)]
  build fails ─────────────────────────► FAIL (no image to scan) (edge case)
        │ success
        ▼
[Image vulnerability scan]
  fixable CRIT/HIGH (not allowlisted) ─► FAIL (image NOT pushed)  (FR-003, FR-011)
  unfixable / allowlisted / MED / LOW ─► PASS (reported)          (FR-006, FR-015)
  scanner DB unavailable ──────────────► FAIL (no false clean)    (FR-012)
        │ pass
        ▼
[Push to Docker Hub] ─► [009 Cloud Run deploy]
```
