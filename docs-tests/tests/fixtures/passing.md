# Passing fixture

Every example here is expected to pass, and the counts in `test_suite.py` depend on it.

## Claims that hold

```bash
cd neko-hoa && npm run test:ci
dotnet test HOAManagementCompany.Tests --filter "Category!=Sandbox"
docker compose up -d db
bash scripts/docs-test.sh --all --no-exec
gh workflow run docs-test.yml
```

Paths relative to an annotated working directory resolve against it:

```bash cwd=neko-hoa
node scripts/stamp-headers.mjs http://localhost:5212
```

A placeholder is a documentation convention, not a redirection:

```bash
tofu plan -var bucket_name=<your-bucket>
```

## Data

```json
{ "role": "BoardMember", "startDate": "2026-10-12" }
```

Alternative bodies, one per line:

```json
{ "role": "BoardMember", "userId": "…" }
{ "role": "BoardMember", "email": "new@example.com" }
```

```jsonc
// a comment, and a trailing comma
{ "enabled": true, }
```

```yaml
services:
  db:
    image: postgres:17
```

## Annotations

```json docs-test=skip reason="fields, not a whole payload"
"RateLimiting": { "AuthPermitsPerMinute": 20 }
```

```text docs-test=skip reason="an unchecked language can still be skipped explicitly"
anything at all
```

<!-- skip: next "Sybil's own skip directive works too" -->
```json
{ not json at all }
```

## Execution

```bash docs-test=run
test -f docs-tests/docs-test.toml
echo "the harness runs from the repository root"
```

```bash docs-test=run exit=1
test -f /tmp/docs-test-no-such-file
```

```bash docs-test=run expect="docs-tests/README.md"
python3 -m docs_test.corpus --all | grep docs-tests/README.md
```

```bash docs-test=run needs=definitely-not-a-real-tool
definitely-not-a-real-tool --version
```

## References

A link to [the repository root](../../..), to [the harness](../../README.md) and to a [heading in it](../../README.md#what-is-checked),
an inline path `docs-tests/docs_test/plugin.py`, a [hypothetical doc](../../../docs/deliberately-hypothetical.md)
that `docs-test.toml` ignores, and a path that only exists when something is built,
`neko-hoa/dist/neko-hoa/browser/_headers`.
