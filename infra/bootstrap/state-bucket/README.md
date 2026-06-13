# State-bucket bootstrap (run once)

Resolves the backend chicken-and-egg: the GCS remote-state backend every environment uses needs a
bucket that does not exist until something creates it. This tiny config uses **local state** (no
backend block) to create that versioned bucket and to enable the GCP APIs the environment module
needs (FR-020, FR-031).

## Prerequisites

- A GCP project with billing enabled.
- `gcloud auth application-default login` completed (operator ADC).
- OpenTofu ≥ 1.8.

## Run

```bash
cd infra/bootstrap/state-bucket
tofu init                       # local state — no backend yet
tofu apply \
  -var gcp_project_id=<PROJECT> \
  -var bucket_name=<UNIQUE_STATE_BUCKET>     # globally-unique
```

This creates:

- `google_storage_bucket.<UNIQUE_STATE_BUCKET>` — **versioned**, uniform bucket-level access.
- The enabled APIs: `run`, `secretmanager`, `iam`, `iamcredentials`, `sts`, `storage`.

## After

Record the bucket name (also printed as the `bucket_name` output) and put it in each environment's
`backend.tf`:

```hcl
terraform {
  backend "gcs" {
    bucket = "<UNIQUE_STATE_BUCKET>"   # ← from here
    prefix = "state/dev"               # state/staging, state/prod for the others
  }
}
```

> The bootstrap's own `terraform.tfstate` stays local. Do not commit it (covered by `infra/.gitignore`).
> `force_destroy = false` protects the bucket from an accidental `tofu destroy`.
