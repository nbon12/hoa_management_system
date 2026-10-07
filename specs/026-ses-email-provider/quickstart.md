# Quickstart: Amazon SES setup (owner checklist) — 026

The repository can't do these steps for you. Until they're done, the SES sandbox tests **skip**
and don't block releases (FR-012). Total time is about 30 minutes, plus DNS propagation and the
production-access review.

## 1. Pick a region

Use **`us-east-1`** unless you have a reason not to. Every step below, and the `SES_REGION` secret,
must use the same region. SES identities and settings are per region.

## 2. Verify the sending domain `mail.nekohoa.com`

1. Open the AWS console, go to **Amazon SES** (region `us-east-1`), then **Configuration → Identities → Create identity**.
2. Choose **Domain** and enter `mail.nekohoa.com`. Leave **Easy DKIM** selected (RSA 2048-bit), and
   leave "Publish DNS records to Route 53" **off**.
3. SES shows **3 CNAME records** (`xxxx._domainkey.mail.nekohoa.com → xxxx.dkim.amazonses.com`).
4. In **Cloudflare → nekohoa.com → DNS**, add all 3 CNAMEs with **Proxy status: DNS only** (grey
   cloud). Proxied records break DKIM.
5. Wait until the identity shows **Verified** in SES. This usually takes minutes, occasionally up
   to 72 hours.

Optional but recommended: add a DMARC record `_dmarc.mail.nekohoa.com TXT "v=DMARC1; p=none;"`.

## 3. Turn on the account-level suppression list

Go to **SES → Configuration → Suppression list**, edit it, and enable suppression for both
**Bounces** and **Complaints**.

## 4. Create a send-only IAM user

1. Go to **IAM → Users → Create user**, name it `nekohoa-ses-ci`, and leave console access off.
2. Attach an **inline policy** (JSON). Replace `<ACCOUNT_ID>` with your 12-digit AWS account ID:

   ```json
   {
     "Version": "2012-10-17",
     "Statement": [{
       "Effect": "Allow",
       "Action": "ses:SendEmail",
       "Resource": "arn:aws:ses:us-east-1:<ACCOUNT_ID>:identity/mail.nekohoa.com"
     }]
   }
   ```

   That's the only permission it needs.
3. Open the user, go to **Security credentials → Create access key → "Application running outside
   AWS"**, and copy the **Access key ID** and **Secret access key**.
4. Rotate this key periodically: create a new key, update the secrets, then delete the old key.

## 5. Add GitHub repository secrets

Go to **GitHub → nbon12/hoa_management_system → Settings → Secrets and variables → Actions → New
repository secret**:

| Secret | Value |
|---|---|
| `SES_REGION` | `us-east-1` |
| `SES_FROM_EMAIL` | `no-reply@mail.nekohoa.com` |
| `SES_ACCESS_KEY_ID` | the access key ID from step 4 |
| `SES_SECRET_ACCESS_KEY` | the secret access key from step 4 |

## 6. Request SES production access

Go to **SES → Account dashboard → Request production access**. Mail type is **Transactional**;
the website is `https://nekohoa.com`. Describe the use case: payment receipts, failed-payment
alerts and verification codes sent only to opted-in HOA residents; bounces and complaints handled by
the account-level suppression list.

CI **doesn't** need this, because simulator addresses work in the SES sandbox. It's only needed
before emailing real residents.

## 7. After the feature PR merges

1. The first push to `main` should show **Integration (provider sandbox)** green, with the SES tests
   **Passed** (not Skipped), followed by **Push Docker Image** and **Deploy to Dev**.
2. Delete the old secrets `SENDGRID_API_KEY` and `SENDGRID_FROM_EMAIL`.
3. Optionally, close the SendGrid account and delete its API keys (`nekohoa`, `NekoHOA-Local`). If
   your local `appsettings.Secrets.json` has a `SendGrid` block, remove it and add a `Ses` block if
   you want local email.

## Local run of the SES sandbox tests (optional)

```bash
export Ses__Region=us-east-1
export Ses__FromEmail=no-reply@mail.nekohoa.com
export Ses__AccessKeyId=...        # from step 4
export Ses__SecretAccessKey=...
# Ses__SimulatorOnly defaults to true in the harness.
dotnet test --filter "Category=Sandbox&FullyQualifiedName~SesSandboxTests"
```

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Tests **Skipped** "SES not configured" | One of the four secrets is missing or blank |
| `SES rejected: MessageRejectedException` | Identity not verified yet, or verified in a different region than `SES_REGION` |
| `SES rejected: AmazonSimpleEmailServiceV2Exception (HTTP 403)` | IAM policy region/account/identity ARN mismatch, or wrong keys |
| `SES rejected: … (HTTP 401/403)` with a signature error | Secret key pasted with extra characters; re-paste it |
| Tests **Skipped** "provider unavailable" | SES throttling or outage; the next push retries |
