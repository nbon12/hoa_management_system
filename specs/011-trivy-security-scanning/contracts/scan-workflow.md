# Contract: Trivy Scan Workflow Interface

This is the behavioral contract for the scanning pipeline. The YAML below is the reference
implementation produced by `/speckit.plan`; `/speckit.tasks` + `/speckit.implement` will land it and
**resolve every `@<SHA>` placeholder to a real pinned commit SHA** (see Quickstart).

## Trigger contract

| Event | Runs | Image-scan target |
|-------|------|-------------------|
| `pull_request` → `main` | `iac-config`, `image-scan` | image built locally from `HOAManagementCompany/Dockerfile` (no push) |
| `push` → `main` | `iac-config`, `image-scan` (in new workflow) **and** the refactored `docker-push` in `test.yml` builds→scans→pushes | locally built image |
| `schedule` (nightly) | `iac-config`, `image-scan` | pulls & scans `sakurapatch/nekohoa-api:latest` (no build) |

## Job graph & gating contract

- `image-scan` declares `needs: [iac-config]` ⇒ the IaC config scan runs **first**, and the image
  build/scan does not start until it passes (FR-001/FR-002; US2-AS1 "stops before the build stage").
- Both jobs are registered as **required status checks** on `main` ⇒ a failing scan blocks merge.
- On the post-merge `test.yml` path the build/scan/push lives in `docker-push`; cross-workflow
  ordering against `iac-config` is enforced at **merge time** by the required `iac-config` check
  (GitHub Actions cannot express `needs:` across workflows), so a vulnerable/misconfigured change
  cannot merge to trigger that push.
- On the main-push deploy path, `test.yml` `docker-push` runs the image scan as a **step before** the
  push step ⇒ a failing scan returns non-zero and the push step never executes (FR-011). `deploy-dev`
  already `needs: [docker-push]`, so a failed scan also stops the deploy.

## Permissions contract

```yaml
permissions:
  contents: read
  security-events: write   # upload-sarif (Code Scanning)
  pull-requests: read      # PR annotations
```

## Severity contract

- **Single source of truth**: the repository variable `vars.TRIVY_SEVERITY` (set once in repo
  Settings → Variables, default `CRITICAL,HIGH`). Both `security-scan.yml` and `test.yml` reference
  `${{ vars.TRIVY_SEVERITY || 'CRITICAL,HIGH' }}`, so the failing-severity set is defined in exactly
  one place across both workflows (FR-007).
- Image scan: `--ignore-unfixed` + root `.trivyignore` (FR-015).
- Gating pass: `exit-code: 1`. SARIF pass: `exit-code: 0`, reports all severities.

## Reference workflow — `.github/workflows/security-scan.yml`

```yaml
name: Security Scan (Trivy)

on:
  pull_request:
    branches: [main]
  push:
    branches: [main]
  schedule:
    - cron: '0 7 * * *'   # nightly re-scan of the published image + infra/

permissions:
  contents: read
  security-events: write
  pull-requests: read

env:
  # Single source of truth: repo variable vars.TRIVY_SEVERITY (default CRITICAL,HIGH) — FR-007.
  # test.yml references the same vars.TRIVY_SEVERITY, so the policy lives in exactly one place.
  TRIVY_SEVERITY: ${{ vars.TRIVY_SEVERITY || 'CRITICAL,HIGH' }}
  IMAGE_LOCAL_TAG: 'nekohoa-api:scan'
  PUBLISHED_IMAGE: 'sakurapatch/nekohoa-api:latest'

jobs:
  # ── Stage 1: IaC configuration scan (runs first; non-blocking until infra/ exists) ──
  iac-config:
    name: IaC config scan (trivy config)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<SHA> # v4 (actions/checkout)

      - name: Detect IaC sources
        id: detect
        shell: bash
        run: |
          if [ -d infra ] && find infra -type f \( -name '*.tf' -o -name '*.tofu' \) | grep -q .; then
            echo "present=true" >> "$GITHUB_OUTPUT"
          else
            echo "present=false" >> "$GITHUB_OUTPUT"
            echo "No OpenTofu sources under infra/ yet — IaC scan is a non-blocking pass (FR-009)."
          fi

      # Report-all → SARIF (does not fail the build)
      - name: Trivy config (SARIF)
        if: steps.detect.outputs.present == 'true'
        uses: aquasecurity/trivy-action@<SHA> # v0.x (aquasecurity/trivy-action)
        with:
          scan-type: config
          scan-ref: infra
          format: sarif
          output: trivy-iac.sarif
          exit-code: '0'

      - name: Upload IaC SARIF to Code Scanning
        if: steps.detect.outputs.present == 'true'
        continue-on-error: true   # Code Scanning may be unavailable on private repos w/o GHAS
        uses: github/codeql-action/upload-sarif@<SHA> # v3 (github/codeql-action)
        with:
          sarif_file: trivy-iac.sarif
          category: trivy-iac

      - name: Upload IaC SARIF artifact
        if: steps.detect.outputs.present == 'true'
        uses: actions/upload-artifact@<SHA> # v4 (actions/upload-artifact)
        with:
          name: trivy-iac-sarif
          path: trivy-iac.sarif
          if-no-files-found: ignore

      # Gating pass → fails on fixable CRITICAL/HIGH (FR-001/FR-005)
      - name: Trivy config (gate)
        if: steps.detect.outputs.present == 'true'
        uses: aquasecurity/trivy-action@<SHA> # v0.x (aquasecurity/trivy-action)
        with:
          scan-type: config
          scan-ref: infra
          format: table
          severity: ${{ env.TRIVY_SEVERITY }}
          exit-code: '1'
          trivyignores: .trivyignore

  # ── Stage 2+3: build the image locally, then scan it before any push ──
  image-scan:
    name: Image vulnerability scan (trivy image)
    runs-on: ubuntu-latest
    needs: [iac-config]          # IaC scan runs first; build/scan gated on it (FR-001/FR-002)
    steps:
      - uses: actions/checkout@<SHA> # v4 (actions/checkout)

      - name: Set up Buildx
        uses: docker/setup-buildx-action@<SHA> # v3 (docker/setup-buildx-action)

      # PR/push: build locally (no push). Schedule: skip build, scan published image.
      - name: Build image (local, no push)
        if: github.event_name != 'schedule'
        uses: docker/build-push-action@<SHA> # v6 (docker/build-push-action)
        with:
          context: .
          file: HOAManagementCompany/Dockerfile
          load: true
          push: false
          tags: ${{ env.IMAGE_LOCAL_TAG }}-${{ github.sha }}

      - name: Pull published image (scheduled re-scan)
        if: github.event_name == 'schedule'
        run: docker pull "$PUBLISHED_IMAGE"

      - name: Resolve scan target
        id: target
        shell: bash
        run: |
          if [ "${{ github.event_name }}" = "schedule" ]; then
            echo "ref=$PUBLISHED_IMAGE" >> "$GITHUB_OUTPUT"
          else
            echo "ref=${IMAGE_LOCAL_TAG}-${{ github.sha }}" >> "$GITHUB_OUTPUT"
          fi

      # Report-all → SARIF (does not fail)
      - name: Trivy image (SARIF)
        uses: aquasecurity/trivy-action@<SHA> # v0.x (aquasecurity/trivy-action)
        with:
          scan-type: image
          image-ref: ${{ steps.target.outputs.ref }}
          format: sarif
          output: trivy-image.sarif
          ignore-unfixed: true
          exit-code: '0'

      - name: Upload image SARIF to Code Scanning
        continue-on-error: true
        uses: github/codeql-action/upload-sarif@<SHA> # v3 (github/codeql-action)
        with:
          sarif_file: trivy-image.sarif
          category: trivy-image

      - name: Upload image SARIF artifact
        uses: actions/upload-artifact@<SHA> # v4 (actions/upload-artifact)
        with:
          name: trivy-image-sarif
          path: trivy-image.sarif
          if-no-files-found: ignore

      # Gating pass → fails on fixable CRITICAL/HIGH not in .trivyignore (FR-003/FR-005/FR-015)
      - name: Trivy image (gate)
        uses: aquasecurity/trivy-action@<SHA> # v0.x (aquasecurity/trivy-action)
        with:
          scan-type: image
          image-ref: ${{ steps.target.outputs.ref }}
          format: table
          severity: ${{ env.TRIVY_SEVERITY }}
          ignore-unfixed: true
          exit-code: '1'
          trivyignores: .trivyignore
```

## Reference change — `test.yml` `docker-push` (build → scan → push)

The single `Build and push` step (currently `push: true`) is split so the scan sits between build
and push. Only the relevant steps are shown; SHAs are pinned the same way.

```yaml
      - name: Set up Buildx
        uses: docker/setup-buildx-action@<SHA> # v3

      - name: Build image (local, no push)
        uses: docker/build-push-action@<SHA> # v6
        with:
          context: .
          file: HOAManagementCompany/Dockerfile
          load: true
          push: false
          tags: nekohoa-api:scan-${{ github.sha }}

      - name: Trivy image scan (gate before push)
        uses: aquasecurity/trivy-action@<SHA> # v0.x
        with:
          scan-type: image
          image-ref: nekohoa-api:scan-${{ github.sha }}
          severity: ${{ vars.TRIVY_SEVERITY || 'CRITICAL,HIGH' }}   # same single source as security-scan.yml (FR-007)
          ignore-unfixed: true
          exit-code: '1'
          trivyignores: .trivyignore

      - name: Log in to Docker Hub
        uses: docker/login-action@<SHA> # v3
        with:
          username: ${{ secrets.DOCKER_HUB_USERNAME }}
          password: ${{ secrets.DOCKER_HUB_TOKEN }}

      - name: Push (reuses buildx cache — same artifact that was scanned)
        uses: docker/build-push-action@<SHA> # v6
        with:
          context: .
          file: HOAManagementCompany/Dockerfile
          push: true
          tags: |
            sakurapatch/nekohoa-api:latest
            sakurapatch/nekohoa-api:${{ github.sha }}
```

## Acceptance trace

| Acceptance scenario / FR | Contract element |
|--------------------------|------------------|
| IaC scan runs before build; build gated on it (FR-001/FR-002, US2-AS1) | `image-scan` `needs: [iac-config]` + required-check-at-merge for `test.yml` path |
| US1 image fails on CRIT/HIGH before push (FR-003/FR-011) | `image-scan` gate + `docker-push` scan-before-push step |
| US1 MEDIUM/LOW pass (FR-006) | gate severity = `CRITICAL,HIGH` only |
| US2 IaC misconfig fails (FR-001/FR-005) | `iac-config` gate |
| US2 empty `infra/` passes (FR-009) | `Detect IaC sources` guard |
| US3 SHA pinning (FR-008) | every `uses:` pinned to `@<SHA>` |
| Reporting hybrid (FR-014) | SARIF upload with `category` per scan + artifact fallback |
| Unfixable/allowlist (FR-015) | `ignore-unfixed: true` + `trivyignores: .trivyignore` |
| Nightly re-scan published image (FR-016) | `schedule` trigger + `Resolve scan target` |
| Scanner DB unavailable fails (FR-012) | Trivy exits non-zero on DB fetch failure (default behavior) |
