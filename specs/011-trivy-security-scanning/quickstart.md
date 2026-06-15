# Quickstart: Trivy Security Scanning Pipeline

Setup and operating instructions for the two-stage Trivy scanning added by feature `011`.

## What gets added

- `.github/workflows/security-scan.yml` — `iac-config` (`trivy config` on `infra/`) and `image-scan`
  (`trivy image` on the locally built backend image / nightly published image).
- A surgical edit to `.github/workflows/test.yml` `docker-push` — build → **scan** → push.
- `.trivyignore` at the repo root — reviewed allowlist of accepted findings.

## One-time setup

### 1. Resolve and pin action SHAs (FR-008)

Never commit a floating tag for a third-party action. Resolve each `@<SHA>` placeholder before
merging. Easiest with [`pinact`](https://github.com/suzuki-shunsuke/pinact):

```bash
go install github.com/suzuki-shunsuke/pinact/cmd/pinact@latest
pinact run .github/workflows/security-scan.yml
pinact run .github/workflows/test.yml
```

Or resolve a single tag→SHA by hand:

```bash
gh api repos/aquasecurity/trivy-action/commits/v0.28.0 --jq '.sha'
```

Keep the trailing `# vX.Y.Z` comment so humans can read the version. Enable Dependabot for the
`github-actions` ecosystem so pin bumps arrive as PRs (which themselves pass the scan).

### 2. Enable GitHub Code Scanning (for the Security-tab experience)

The SARIF upload populates **Security → Code scanning**. It is free on public repos. On a **private**
repo it requires GitHub Advanced Security. If Code Scanning is not enabled, the `upload-sarif` step is
`continue-on-error` and the SARIF is still saved as a build artifact — **gating is unaffected**
(the table-format step is what fails the build), you just won't get Security-tab alerts.

To enable: Repo → Settings → Code security and analysis → enable **Code scanning** (default setup can
be left off; this workflow uploads its own SARIF).

### 3. Make the scans required status checks (FR-011 on the PR path)

Repo → Settings → Branches → branch protection for `main` → **Require status checks to pass** → add
`IaC config scan (trivy config)` and `Image vulnerability scan (trivy image)`. This blocks merges
(and therefore the post-merge push/deploy) when a scan fails.

### 4. Define the severity policy variable (single source — FR-007)

Set the failing-severity set **once** as a repository variable so both `security-scan.yml` and
`test.yml` read the same value:

```bash
gh variable set TRIVY_SEVERITY --body 'CRITICAL,HIGH'
```

Or Repo → Settings → Secrets and variables → Actions → Variables → `TRIVY_SEVERITY = CRITICAL,HIGH`.
Both workflows reference `${{ vars.TRIVY_SEVERITY || 'CRITICAL,HIGH' }}`, so the `|| 'CRITICAL,HIGH'`
fallback keeps them working before the variable is set. To tighten later (e.g. add `MEDIUM`), change
this one variable — no workflow edits.

### 5. Create `.trivyignore`

Start empty. Add documented exceptions only via review:

```text
# CVE-2025-12345 — no upstream fix in base image; tracked in INFRA-42; re-check 2026-09-01 — @nbon12
CVE-2025-12345
```

## Day-to-day

### Severity policy — one knob

Change the `TRIVY_SEVERITY` **repository variable** (see setup step 4) to tighten later, e.g.
`gh variable set TRIVY_SEVERITY --body 'CRITICAL,HIGH,MEDIUM'`. Both workflows pick it up; no YAML
edits. The image scan keeps `--ignore-unfixed`, so only findings with an available fix block the
build.

### Reading results

- **On a PR**: findings introduced by the change appear as annotations on the PR and in the run logs
  (table output). Click the failing check → step logs for the package/severity/fixed-version.
- **On `main` / nightly**: open **Security → Code scanning**, filter by tool category `trivy-iac` or
  `trivy-image`.

### Running Trivy locally (reproduce a CI failure)

```bash
# Install: https://aquasecurity.github.io/trivy
# IaC config scan
trivy config --severity CRITICAL,HIGH ./infra

# Image scan (build locally first, then scan the local tag — same as CI)
docker build -f HOAManagementCompany/Dockerfile -t nekohoa-api:scan .
trivy image --severity CRITICAL,HIGH --ignore-unfixed \
  --ignorefile .trivyignore nekohoa-api:scan
```

## Verifying the feature (maps to acceptance scenarios)

| Check | How | Expected |
|-------|-----|----------|
| Vulnerable image blocked | Temporarily set an outdated base image, open a PR | `image-scan` fails; merge blocked; no push |
| Clean image passes | Revert | Scans green; pipeline proceeds |
| IaC misconfig caught | Add `infra/insecure.tf` with a known bad setting | `iac-config` fails |
| Empty `infra/` passes | With no `infra/*.tf` | `iac-config` logs "no IaC to scan", exits 0 |
| SHA pinning | `grep -nE '@(main\|master\|v[0-9]+)\b' .github/workflows/security-scan.yml` | no third-party matches |
| Nightly re-scan | Trigger the schedule (or `workflow_dispatch` for testing) | scans `sakurapatch/nekohoa-api:latest` |

## Recommended follow-up (out of scope for this feature)

`test.yml` still uses floating tags for several non-sensitive actions (`actions/checkout@v4`,
`docker/build-push-action@v5`, `codecov/codecov-action@v4`, …). Pinning the whole file to SHAs is a
worthwhile separate hardening PR (run `pinact run .github/workflows/test.yml`).
