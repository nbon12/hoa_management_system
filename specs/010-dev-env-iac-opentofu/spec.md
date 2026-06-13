# Feature Specification: Infrastructure as Code — Declarative Dev Environment Provisioning

**Feature Branch**: `010-dev-env-iac-opentofu`  
**Created**: 2026-06-13  
**Status**: Draft  
**Input**: User description: "Create a declarative Infrastructure-as-Code setup, committed in the repo under infra/, using OpenTofu, that provisions the entire Dev environment for the 009-dev-auto-deploy pipeline so no cloud resources are created by hand."

## Why This Feature Exists

Feature `009-dev-auto-deploy` delivered the **continuous-deployment pipeline** (the `deploy-dev`
GitHub Actions job) but deliberately deferred the creation of the **cloud resources that pipeline
deploys into** — the isolated database, the backend runtime service, the edge configuration, the
secret store, and the identity federation that lets the pipeline authenticate. Those resources were
left to be created by hand (009's Phase 1, tasks T001–T006). This feature replaces that manual
provisioning with declarative Infrastructure as Code so the entire Dev environment is reproducible,
reviewable, and version-controlled from the repository.

The output is a contract: once the infrastructure is applied and the operator sets a handful of
GitHub Actions secrets/variables and flips `DEV_DEPLOY_ENABLED=true`, the `009` pipeline does the
rest with no further manual cloud setup.

## Clarifications

### Session 2026-06-13

- Q: How should the OpenTofu code under `infra/` be structured for multi-environment reuse with isolated state? → A: A reusable shared module (`infra/modules/*`) called by per-environment directories (`infra/environments/dev`, `.../staging`, `.../prod`), each with its own tfvars and backend config.
- Q: What gate should guard the apply-on-merge workflow? → A: **Prod** apply requires manual approval (protected GitHub Environment with a required reviewer); **Dev** and **Staging** auto-apply on merge to `main` with no approval step.
- Q: How should remote state (GCS) be isolated across environments? → A: A single versioned GCS state bucket with a distinct prefix/path per environment (e.g. `state/dev`, `state/staging`, `state/prod`).
- Q: Beyond restricting deployer-SA impersonation to this repo, should the WIF trust also be ref-restricted? → A: No — repo-scoped only (any branch/ref in this repo may impersonate the deployer); this keeps PR plan-only runs able to authenticate without a second identity.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are **platform/infrastructure operators** (the engineering team members
responsible for the cloud accounts) and, indirectly, every developer who relies on a reproducible
Dev environment. The feature replaces tribal, click-ops knowledge with reviewed, declarative
definitions living in the repo.

### User Story 1 - Stand up the entire Dev environment from the repo with one apply (Priority: P1)

An operator with the necessary cloud credentials checks out the repo, supplies the required secrets
in a local (gitignored) variables file, and runs a single plan-then-apply cycle. Every cloud
resource the `009` pipeline depends on — the database, the backend runtime service, the secret
store, the identity federation, and the edge/DNS configuration — is created to match the exact
names and values the pipeline hardcodes. No resource is created by hand in a cloud console.

**Why this priority**: This is the core of the feature and the minimum viable product. Without a
single reproducible apply that produces a pipeline-compatible environment, the `009` pipeline has
nothing to deploy into and the manual-provisioning gap the feature exists to close remains open. It
delivers standalone value the moment it works: the Dev environment becomes a repo artifact.

**Independent Test**: From a clean cloud account (or after destroying a prior Dev environment), run
the plan and apply against the Dev configuration and confirm that every resource named in the `009`
environment matrix exists with the contracted name and configuration, and that the database
connection output flows into the database secret automatically.

**Acceptance Scenarios**:

1. **Given** valid cloud credentials and a populated (gitignored) secrets file, **When** the
   operator runs the plan, **Then** the plan completes with no errors and lists every resource that
   will be created, with no unexpected changes to resources outside the Dev environment.
2. **Given** an approved plan, **When** the operator applies it, **Then** the managed database
   (project, Dev branch, database, and role), the backend runtime service `nekohoa-api-dev`, the
   runtime and deployer identities, the secret store entries, the identity-federation pool/provider,
   and the edge project, object-storage bucket, and DNS records are all created.
3. **Given** a completed apply, **When** the operator inspects the backend runtime service, **Then**
   it is configured with environment `Dev`, scale-to-zero (minimum 0 instances), public
   (unauthenticated) access, container port `8080`, and a health probe on `/health`.
4. **Given** a completed apply, **When** the operator inspects the database-connection secret,
   **Then** its value is the pooled connection string produced by the database resource, in the
   .NET keyword format the application expects (not a URI), and it was wired in automatically with
   no copy/paste.
5. **Given** a completed apply, **When** the operator re-runs the plan with no changes, **Then** the
   plan reports no drift (the configuration is idempotent and convergent).

---

### User Story 2 - Discover exactly what to wire into GitHub Actions (Priority: P2)

After applying the infrastructure, the operator needs to connect the `009` pipeline to the
freshly-created resources. Rather than hunting through cloud consoles, the operator reads the
configuration's outputs, which print every GitHub Actions secret and variable value the pipeline
requires, plus an explicit reminder about the value that must be set last to actually turn the
pipeline on.

**Why this priority**: The infrastructure is only useful once the pipeline can authenticate to it
and find it. This story removes the most error-prone manual step (transcribing identifiers) and
makes the handoff from infrastructure to pipeline self-documenting. It depends on US1 but delivers
distinct value: a correct, copy-pasteable wiring list.

**Independent Test**: After an apply, read the outputs and confirm every GitHub Actions
secret/variable the `009` pipeline reads is present with a correct value, that sensitive values are
masked in console output, and that the instruction to enable the pipeline last is shown.

**Acceptance Scenarios**:

1. **Given** a completed apply, **When** the operator views the outputs, **Then** the values for the
   identity-federation provider, the deployer identity, the edge API token, the edge account
   identifier, the runtime region, and the deploy-alert webhook target are all printed.
2. **Given** the outputs include sensitive material, **When** they are displayed, **Then** sensitive
   values are marked sensitive and not shown in plaintext in ordinary plan/apply summaries.
3. **Given** the operator has set the secrets and variables, **When** they read the outputs'
   guidance, **Then** it explicitly instructs them to set the pipeline-enable variable
   (`DEV_DEPLOY_ENABLED=true`) **last**, after everything else is in place.

---

### User Story 3 - Review and apply infrastructure changes safely through pull requests (Priority: P2)

Infrastructure changes go through the same review discipline as application code. When a contributor
opens a pull request touching the infrastructure definitions, an automated workflow runs a
**plan-only** preview and surfaces the result so reviewers see exactly what would change before
anything is applied. When the change merges, a **gated apply** runs to converge the real
environment.

**Why this priority**: Declarative IaC without review automation invites drift and unreviewed
production-adjacent changes. This story makes infrastructure changes auditable and safe, but the
environment can be stood up manually (US1) before this automation exists, so it is not the MVP.

**Independent Test**: Open a pull request with a trivial infrastructure change and confirm the
plan-only workflow runs and surfaces the planned change without applying it; merge it and confirm
the gated apply runs and converges the environment.

**Acceptance Scenarios**:

1. **Given** a pull request that modifies infrastructure definitions, **When** the workflow runs,
   **Then** it produces a plan only and makes no changes to live resources.
2. **Given** an infrastructure change to **Dev** (or Staging) is merged, **When** the apply
   workflow runs, **Then** it applies the change automatically to that environment; **Given** the
   change targets **Prod**, the apply MUST pause for a required reviewer's approval (protected
   environment) before touching live resources.
3. **Given** either workflow runs, **When** it authenticates to the cloud accounts, **Then** it does
   so without any long-lived static credentials committed to the repo.

---

### User Story 4 - Extend the same setup to Staging and Prod later (Priority: P3)

When the team is ready to provision Staging and Prod, they reuse the same definitions with
per-environment inputs and isolated state, without copying and diverging the Dev configuration.

**Why this priority**: The constitution (§10) requires separate, isolated environments. Designing
for reuse now avoids a costly rewrite later, but Staging/Prod are explicitly out of scope for this
feature's deliverable, so it is the lowest priority and is satisfied by structure/design rather than
by additional provisioned resources.

**Independent Test**: Demonstrate that adding a new environment requires only new input values and a
new isolated state target — not duplicating or editing the resource definitions themselves.

**Acceptance Scenarios**:

1. **Given** the Dev environment is defined, **When** a reviewer inspects the structure, **Then**
   environment-specific values (names, region, domains, branch) are parameterized rather than
   hardcoded into reusable definitions.
2. **Given** a future Staging/Prod environment, **When** it is provisioned, **Then** it uses its own
   isolated state and its own database and runtime service, with no shared mutable state with Dev
   (constitution §10).

---

### Edge Cases

- **State bucket does not yet exist (chicken-and-egg)**: The remote state store itself cannot store
  its own creation state. The first-ever setup must create the state store through a documented
  bootstrap step before the main apply can use the remote backend.
- **Custom-domain certificate ordering**: The API custom domain must obtain a certificate from the
  runtime platform before edge proxying is enabled; enabling the proxy too early breaks certificate
  issuance. The DNS records must be created in (or documented to require) the correct order
  (unproxied "grey-cloud" first, then proxied once the certificate is issued).
- **Identity-federation over-broad trust**: If the federation trust condition is too loose, a
  repository other than this one could impersonate the deployer identity. The trust condition MUST
  restrict impersonation to this specific repository.
- **Drift introduced by manual console edits**: If someone changes a resource by hand, the next plan
  must reveal the drift so it can be reconciled rather than silently ignored.
- **Missing or malformed secrets file**: If a required secret input is absent, the plan must fail
  clearly rather than create a half-configured environment or fall back to an empty value.
- **Name collision with the pipeline contract**: If any resource name diverges from the values the
  `009` pipeline hardcodes, the pipeline breaks; the configuration must use the exact contracted
  names.
- **Re-applying after the pipeline has deployed a revision**: A subsequent infrastructure apply must
  not clobber the application image/revision that the `009` pipeline manages on the runtime service.

## Requirements *(mandatory)*

### Functional Requirements

#### Database (managed PostgreSQL — Neon)

- **FR-001**: The configuration MUST provision a managed PostgreSQL project, a dedicated **Dev**
  branch, a database, and a role for the Dev environment.
- **FR-002**: The configuration MUST expose the **pooled** connection endpoint as an output for
  downstream consumption.
- **FR-003**: The connection value consumed by the application MUST be in the .NET/Npgsql **keyword
  format** (e.g., `Host=…;Database=…;Username=…;Password=…;SSL Mode=Require;Channel Binding=Require`),
  **not** the `postgresql://` URI form.
- **FR-004**: The Dev database MUST be isolated from any Staging/Prod database (constitution §10,
  §3).

#### Backend runtime (Cloud Run)

- **FR-005**: The configuration MUST provision the backend runtime service named exactly
  **`nekohoa-api-dev`**.
- **FR-006**: The runtime service MUST be configured with: region taken from an input variable;
  minimum instances **0** (scale-to-zero); **unauthenticated** public access; container port
  **8080**; environment variable `ASPNETCORE_ENVIRONMENT=Dev`; and a health probe targeting
  **`/health`**.
- **FR-007**: The runtime service's container image is **pulled, not built** by this configuration;
  the application image and revision are owned by the `009` pipeline, and an infrastructure apply
  MUST NOT clobber the pipeline-managed revision.
- **FR-008**: The runtime service MUST consume its secret values **by reference** from the secret
  store (not as literal values baked into the configuration or image).

#### Identity & access

- **FR-009**: The configuration MUST provision a **runtime** service identity granted the ability to
  access secrets (secret accessor role) and nothing broader than required.
- **FR-010**: The configuration MUST provision a **deployer** service identity granted the
  privileges the pipeline needs to deploy (runtime admin) and to act as the runtime identity
  (service-account user), and nothing broader than required (least privilege, constitution §7).
- **FR-011**: The configuration MUST provision a **Workload Identity Federation** pool and provider
  that allows the GitHub repository to impersonate the deployer identity **without** long-lived
  static keys.
- **FR-012**: The federation trust condition MUST restrict impersonation to **this specific
  repository** so no other repository can assume the deployer identity. The condition is
  **repo-scoped only** (not further restricted by git ref/branch), so any branch or PR in this
  repository — including plan-only PR runs — can authenticate without provisioning a second
  identity.

#### Secret store (Secret Manager)

- **FR-013**: The configuration MUST provision secret-store entries with these **exact** identifiers:
  `dev-db-connection`, `dev-jwt-secret`, `dev-sentry-dsn`, `dev-stripe-secret-key`,
  `dev-stripe-webhook-secret`, `dev-storage-service-url`, `dev-storage-access-key`,
  `dev-storage-secret-key`, `dev-scheduler-secret`.
- **FR-014**: The `dev-db-connection` secret value MUST be wired **automatically** from the database
  connection-string output (FR-003) with no manual copy/paste.
- **FR-015**: Secret values supplied by the operator (e.g., signing keys, third-party API
  credentials) MUST come from a gitignored variables file and MUST NOT be committed to the
  repository (constitution §8).

#### Edge (Cloudflare)

- **FR-016**: The configuration MUST provision the frontend hosting (Pages) project named exactly
  **`nekohoa-dev`** with production branch **`main`**.
- **FR-017**: The configuration MUST provision the Dev documents object-storage (R2) bucket.
- **FR-018**: The configuration MUST provision the DNS records for **`dev.nekohoa.com`** (frontend
  hosting) and **`api-dev.nekohoa.com`** (backend custom-domain mapping; CNAME →
  `ghs.googlehosted.com`).
- **FR-019**: The API custom-domain record MUST account for the certificate-issuance ordering
  constraint (unproxied/"grey-cloud" until the certificate is issued, then proxied at
  Full(strict)); where a single declarative step cannot satisfy this, the required two-step or
  documented manual flip MUST be provided.

#### State, secrets handling, and outputs

- **FR-020**: Remote state MUST be stored in a **single versioned** cloud object-storage state
  backend (one GCS bucket), with a **distinct prefix/path per environment** (e.g. `state/dev`,
  `state/staging`, `state/prod`) so each environment's state object is isolated.
- **FR-021**: The required provider versions MUST be **pinned**, and the community-maintained
  database provider (`kislerdm/neon`) MUST be explicitly called out as community-maintained (not
  vendor-verified) with a pinned version.
- **FR-022**: All secret inputs (database password, edge API token, database API key, cloud
  credentials) MUST be supplied via a gitignored variables file; the repo MUST include a
  non-secret example/template for that file and a gitignore entry that prevents committing the real
  one.
- **FR-023**: Outputs MUST print the GitHub Actions secret/variable values an operator must set:
  the identity-federation provider (`GCP_WIF_PROVIDER`), the deployer identity
  (`GCP_DEPLOY_SERVICE_ACCOUNT`), the edge API token (`CLOUDFLARE_API_TOKEN`), the edge account
  identifier (`CLOUDFLARE_ACCOUNT_ID`), the runtime region (`GCP_REGION`), and the deploy-alert
  webhook target (`DEPLOY_ALERT_WEBHOOK_URL`); plus an explicit instruction to set the
  pipeline-enable variable (`DEV_DEPLOY_ENABLED=true`) **last**.
- **FR-024**: Outputs that carry sensitive values MUST be marked **sensitive** so they are not
  rendered in plaintext in ordinary plan/apply summaries (constitution §8).

#### Automation & lifecycle

- **FR-025**: A GitHub Actions workflow MUST run a **plan-only** preview on pull requests that touch
  the infrastructure definitions, making **no** changes to live resources.
- **FR-026**: A GitHub Actions workflow MUST run an **apply** on merge to `main`. For **Dev** (and
  Staging) the apply runs **automatically** with no approval step; for **Prod** the apply MUST be
  **gated** behind a protected GitHub Environment requiring a reviewer's manual approval before any
  live change. (This feature delivers the Dev path; the gate configuration MUST be designed so Prod
  is approval-gated when added.)
- **FR-027**: The automation workflows MUST authenticate to the cloud accounts without committing
  long-lived static credentials to the repo (using the federated identity from FR-011).
- **FR-028**: The configuration MUST be idempotent: re-running the plan against an unchanged
  environment MUST report no drift.

#### Pipeline-contract fidelity

- **FR-029**: Every resource name and value that the `009` `deploy-dev` job hardcodes MUST match the
  values in `specs/009-dev-auto-deploy/contracts/environment-matrix.md` exactly (service name,
  region variable, container port, environment value, the nine secret IDs and their env-var
  mappings, project name, domains, and the GitHub secret/variable names).

#### Reusability

- **FR-030**: Environment-specific values (names, region, domains, branch) MUST be parameterized so
  the same definitions extend cleanly to Staging and Prod with isolated state and isolated
  resources (constitution §10), without duplicating the resource definitions. The structure MUST
  be a **reusable shared module** (`infra/modules/*`) consumed by **per-environment directories**
  (`infra/environments/dev`, and later `.../staging`, `.../prod`), each supplying its own tfvars and
  backend configuration.

#### Bootstrap

- **FR-031**: The first-time setup of the remote state backend (the state bucket itself) MUST be
  handled by a documented bootstrap step (manual creation or a small local-state bootstrap) to
  resolve the chicken-and-egg problem, and this MUST be the only documented manual cloud step
  besides the day-(-1) account/credential prerequisites.

### Key Entities

- **Dev environment definition**: The complete declarative description of the Dev environment,
  parameterized by environment-specific inputs (name suffix `dev`, region, domains, production
  branch).
- **Managed database resources**: Project, Dev branch, database, role, and the derived pooled
  connection string (in .NET keyword format).
- **Backend runtime service**: `nekohoa-api-dev`, with its scale-to-zero, public-access, port,
  environment, health-probe, and secret-reference configuration.
- **Service identities**: A runtime identity (secret accessor) and a deployer identity (deploy
  privileges + impersonation), plus the federation pool/provider that binds the GitHub repo to the
  deployer identity.
- **Secret-store entries**: The nine named Dev secrets, one of which (`dev-db-connection`) is
  auto-populated from the database output.
- **Edge resources**: The frontend hosting project (`nekohoa-dev`), the Dev documents bucket, and
  the two DNS records.
- **State backend**: The remote object-storage location holding the environment's state, isolated
  per environment.
- **Operator inputs**: The gitignored secret/credential values and the non-secret example template.
- **Outputs**: The GitHub Actions wiring values and enablement guidance.

### Constitution Requirements *(mandatory when applicable)*

- **Tenant boundary**: Not applicable at the data-row level — this feature provisions
  infrastructure, not HOA-scoped application data. Environment isolation (Dev vs Staging/Prod) is
  the relevant boundary and is enforced by separate databases, runtime services, and state
  (§10, §3).
- **Authorization**: No application endpoints are added. Infrastructure access control is enforced
  by least-privilege service identities and a repository-scoped federation trust condition (§7);
  the deployer identity gets only deploy + impersonation rights and the runtime identity only
  secret access.
- **Database/runtime**: Provisions the isolated **Dev** managed-PostgreSQL database with pooled
  connections (the connection-string output uses the pooled endpoint per §8 low-connection/pooling
  guidance) and a scale-to-zero Cloud Run runtime that applies migrations idempotently at startup
  (that startup behavior is owned by `009`; this feature only provisions the service and its
  config). No schema, migration, or application persistence changes are introduced.
- **File storage**: Provisions the Cloudflare **R2** Dev documents bucket used by hosted
  environments (§8); MinIO remains the local/test substitute and is unaffected.
- **Security and abuse controls**: No secrets committed; secret values supplied via gitignored
  variables file and stored in the managed secret store; sensitive outputs marked sensitive;
  long-lived static cloud keys avoided in favor of federated identity (§7, §8). Edge protection in
  front of the API is provisioned via the proxied custom domain (§2 edge requirement).
- **Observability**: Provisions the `dev-sentry-dsn` secret slot so the runtime can report to the
  Dev Sentry project with environment/release tags (§8); this feature wires the secret slot, not the
  application's Sentry initialization.
- **Quality gates**: This is an infrastructure-configuration feature; the 95% line-coverage gate
  applies to application code and is not meaningful for declarative infrastructure files. Validation
  is instead by plan/apply success, idempotent re-plan (no drift), and verification against the
  `009` environment matrix. Repowise-maintained regions MUST be refreshed for the PR per §9. PR
  scope is a focused, cross-cutting infrastructure slice (allowed under §11).
- **Accessibility / Frontend testing**: Not applicable — no UI is added by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator can stand up a complete, pipeline-compatible Dev environment from a clean
  account using only the repo, a credentials login, and a populated secrets file — with **zero**
  resources created by hand in a cloud console (other than the documented one-time state-bucket
  bootstrap and day-(-1) account prerequisites).
- **SC-002**: 100% of the resource names/values the `009` `deploy-dev` job depends on (per the
  environment matrix) match what the configuration produces — verified by a name-by-name checklist.
- **SC-003**: Re-running the plan immediately after a successful apply reports **no** changes (zero
  drift), demonstrating idempotency.
- **SC-004**: The database-connection secret is populated automatically from the database output
  with no manual copy/paste, and its value is in .NET keyword format (not a URI).
- **SC-005**: No secret value (database password, API tokens/keys, credentials) appears anywhere in
  committed repository files or in non-sensitive plan/apply output; the only places real secrets
  live are the gitignored variables file and the managed secret store.
- **SC-006**: After apply, an operator can complete pipeline wiring using **only** the configuration
  outputs (no console spelunking), and the outputs explicitly tell them to enable the pipeline last.
- **SC-007**: A pull request touching infrastructure produces a plan preview and applies **nothing**
  to live resources; the apply only runs behind its gate after merge.
- **SC-008**: Only this repository can assume the deployer identity — an impersonation attempt
  attributed to any other repository is rejected by the trust condition.
- **SC-009**: Adding a new environment (Staging/Prod) requires only new input values and a new
  isolated state target, with no edits to the reusable resource definitions — demonstrated by a
  design/dry-run walkthrough.

## Assumptions

- **Day-(-1) prerequisites are done manually, once**: a Google Cloud project with billing, a
  Cloudflare account, and a managed-database (Neon) account with an API key already exist. These are
  account-level boundaries that cannot themselves be created by this configuration.
- **Initial apply credentials** are provided by the operator at run time (cloud application-default
  login + edge API token + database API key) and are never committed; CI uses federated identity
  instead.
- **State-bucket bootstrap**: the remote state bucket is created via a documented bootstrap step
  (manual creation or a tiny local-state bootstrap) before the main configuration uses the remote
  backend; this is the single accepted manual cloud step.
- **Tool is OpenTofu** and the layout lives under `infra/` in the repo, as decided in the handoff.
- **The community database provider `kislerdm/neon`** is acceptable despite being
  community-maintained (not vendor-verified); its version is pinned and the risk is documented.
- **The application image** `sakurapatch/nekohoa-api:<sha>` is built and published by the existing
  `009` pipeline (Docker Hub); this feature references/pulls images and never builds them. A
  placeholder/bootstrap image may be used for the very first service creation before the pipeline
  has pushed a real revision.
- **The custom-domain certificate ordering** for `api-dev.nekohoa.com` may require a documented
  two-step (grey-cloud → proxied Full(strict)) rather than a single converging apply; this is
  acceptable and documented rather than treated as a defect.
- **GitHub Actions secret/variable creation itself is performed by the operator** (this
  configuration prints the values to set, but does not push them into GitHub on the operator's
  behalf), keeping the repo free of any mechanism that could write its own CI secrets.
- **Reasonable defaults** are used for unspecified resource settings (e.g., bucket location, IAM
  member formats) consistent with the `009` contract and the constitution.
