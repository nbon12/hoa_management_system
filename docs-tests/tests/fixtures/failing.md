# Failing fixture

Every example here is expected to fail, one failure each, and the counts in
`test_suite.py` depend on it. Nothing here describes the real repository.

## Syntax

```bash
if [ -f foo ]
echo "missing then/fi"
```

## Claims

```bash
cd neko-hoa && npm run lint
```

```bash
dotnet run --project NoSuchProject
```

```bash
cat specs/999-not-a-feature/spec.md
```

```bash
docker compose exec nosuchservice psql -U hoa
```

```bash
gh workflow run nosuch-workflow.yml
```

## Data

```json
{ "unclosed": true
```

```yaml
services:
  db:
   image: bad
    indent: worse
```

## Annotations

```bash docs-test=run timeuot=30
echo "timeuot is not a key"
```

```json docs-test=run
{ "json": "cannot be run" }
```

```text docs-test=run
this language is not checked at all
```

## Execution

```bash docs-test=run
echo "this block exits non-zero" && exit 3
```

```bash docs-test=run
rm -rf /tmp/docs-test-denylist-check
```

## References

A [link to nothing](./no-such-document.md), a [link to a missing heading](./passing.md#no-such-heading),
and an inline `specs/999-not-a-feature/tasks.md` that was never written.
