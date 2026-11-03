# Executable documentation tests

Our Markdown makes claims: this command exists, that payload looks like this, this
file is over there. Code moves and the claims quietly stop being true. This suite
makes every one of them a test case.

It is built on [Sybil](https://sybil.readthedocs.io/) — "automated testing for the
examples in your code and documentation" — which turns each example into its own
pytest item identified by file and line, so a failure points at the block that
drifted rather than at the document:

```text
specs/008-config-validation/quickstart.md:67: this shell example describes things
that are not in the repository:
  - `npm run lint` is documented but neko-hoa/package.json has no 'lint' script
```

Around Sybil sit this repository's own parsers, evaluators and claim checks, in
`docs-tests/docs_test/`.

## Running it

```bash
scripts/docs-test.sh --changed origin/main   # what the pull request gate runs
scripts/docs-test.sh --all                   # what the weekly sweep runs
scripts/docs-test.sh --all --no-exec         # static checks only, nothing is run
scripts/docs-test.sh --changed HEAD~1 -- -vv # pass anything else through to pytest
```

The script creates `.venv-docs-tests/` on first use and reinstalls it whenever
`docs-tests/requirements.txt` changes. It needs Python 3.11 or newer (the harness
reads TOML with `tomllib`); set `PYTHON=/path/to/python3.12` if the default
`python3` is older, as it is on a stock macOS. Nothing else has to be installed.

The corpus the suite will look at is printable on its own:

```bash docs-test=run
PYTHONPATH=docs-tests python3 -m docs_test.corpus --all | tail -3
```

<a id="what-is-checked"></a>

## What is checked

| Example | Check |
|---|---|
| `bash`, `sh`, `shell`, `zsh`, `console` | parses as bash (`<placeholders>` allowed); every repository thing it names really exists; runs, if annotated |
| `json` | parses, as one document or as one payload per line |
| `jsonc` | the same, with `//` and `/* */` comments and trailing commas allowed |
| `yaml`, `yml` | parses, single- or multi-document |
| any document | relative links resolve, `#anchors` exist, and backticked repository paths exist (see below) |

The claims a shell example makes are checked without running anything:

- `npm run <script>` against `neko-hoa/package.json`
- `dotnet … --project <path>` and `dotnet test <project>` against the repository
- `docker compose <command> <service>` against `docker-compose.yaml`
- `gh workflow run <file>` against `.github/workflows/`
- any word that looks like a repository path, resolved against the block's
  `cwd=` and then the repository root

A path git ignores — `coverage/`, `neko-hoa/dist/`, `appsettings.Secrets.json` —
is never a failure: documentation may name it and we cannot verify it.

**A backticked path rooted at one of the repository's top-level directories is a
claim that the path exists** — `specs/…`, `docs/…`, `neko-hoa/…`,
`HOAManagementCompany/…` and the rest of `PATH_ROOTS` in
`docs-tests/docs_test/claims.py`. Prose with a slash in it (`api/v1/properties`,
`and/or`) is not a claim, and neither is a path with an ellipsis in it
(`specs/…/tasks.md`), which is a shape. When writing about something that was
removed, leave the path as prose; don't backtick it.

## Annotating a block

Annotations go on the fence's info line, after the language. GitHub reads only the
first word of that line as the language and ignores the rest, so annotations are
invisible in rendered documentation.

<!-- skip: start "illustrations of the annotation syntax" -->

Run this block and expect it to succeed:

````markdown
```bash docs-test=run
test -f docker-compose.yaml
```
````

Run it somewhere else, once the tools it needs are there, and give it longer:

````markdown
```bash docs-test=run cwd=neko-hoa needs="npm, node" timeout=300
npm run build
```
````

Expect a specific exit code, or specific output:

````markdown
```bash docs-test=run exit=1
dotnet run --project HOAManagementCompany -- --nonsense
```

```bash docs-test=run expect="Build succeeded"
dotnet build HOAManagementCompany
```
````

Leave an example out of the suite, with a reason that shows up in the report:

````markdown
```json docs-test=skip reason="response fields, not a whole payload"
"RateLimiting": { "AuthPermitsPerMinute": 20 }
```
````

<!-- skip: end -->

Sybil's own skip directive does the same thing and is the right tool when the block
cannot carry an annotation: put `<!-- skip: next "illustrative" -->` on the line
before one example, or wrap a stretch of them in `<!-- skip: start "illustrations of the annotation syntax" -->` and
`<!-- skip: end -->` — as the illustrations above are wrapped, which is why they are
reported as skipped rather than run.

Keys: `docs-test` (`run`, `check`, `skip`), `reason`, `cwd`, `needs`, `timeout`,
`exit`, `expect`. An unrecognised key next to a recognised one is a failure rather
than a silently ignored typo, and an annotation on a language we don't check is a
failure too — otherwise it would never run.

## Runnable examples have to be safe

`docs-test=run` executes the block with `bash -e -o pipefail` from the repository
root (or `cwd=`), so it has to be hermetic on any checkout: no pushes, no `apply`,
no deletes, nothing that needs a credential. `DEFAULT_DENYLIST` in
`docs-tests/docs_test/config.py` refuses the obvious ones — matched
case-insensitively, checked for every `docs-test=run` block whether or not this
run executes anything — and lives in code so that editing the config cannot make
the gate destructive. `needs=` skips a block, rather than failing it, where the
tool is not installed, which is how the same document works on a laptop and on a
runner that has more installed than it does. No block may ask for longer than
`MAX_TIMEOUT` (300s).

**The denylist is a backstop against a careless example, not a sandbox.** It
matches command patterns, so it cannot stop `node -e '…'` or anything else that
reaches the same place by another route. Be clear-eyed about what the gate is:
it executes blocks the pull request introduced, before anyone has reviewed them,
exactly as the test workflow runs the branch's test code. What bounds that is the
job it runs in — `contents: read`, no secrets, the checkout token not persisted
into `.git/config`, and a 10-minute `timeout-minutes` — not the denylist. On a
fork's pull request GitHub narrows it further, to a read-only token and no
secrets at all. If that trade stops being worth it, pass `--docs-no-exec` in the
gate and let the weekly sweep be the only place blocks run.

## Scope

`docs-tests/docs-test.toml` holds the scope, and only the scope:

- **`corpus.include` / `corpus.exclude`** — every tracked Markdown file, minus
  vendored Spec Kit commands, generated agent context and the wireframe bundle.
- **`corpus.point_in_time`** — documents that record a moment: `tasks.md`,
  `plan.md`, `research.md`, handoffs, patch notes. Their examples are still
  checked, and so are their links and the claims inside their examples; what is
  not checked is the backticked paths in their prose, because constitution
  section 11 puts the freshness obligation on `spec.md` and leaves these as
  point-in-time artifacts.
- **`references.ignore_paths`** — deliberately hypothetical paths, like the
  `infra/insecure.tf` in a walkthrough of what a Trivy finding looks like.

Vendored and excluded directories are genuinely out:

```bash docs-test=run
corpus="$(PYTHONPATH=docs-tests python3 -m docs_test.corpus --all)"
test -n "$corpus"
! printf '%s\n' "$corpus" | grep -q '^\.cursor/'
```

## In CI

[`.github/workflows/docs-test.yml`](../.github/workflows/docs-test.yml) runs it
two ways:

- **Pull request gate** (blocking) — the documents the pull request touches, plus
  this harness's own tests. Seconds, not minutes. It sets up Python, but the
  GitHub runner image also carries git, node/npm and dotnet, so a `docs-test=run`
  block runs there whenever the image happens to have what `needs=` asks for.
  Keep annotated blocks quick, and remember the default 60s timeout.
- **Weekly sweep** — the whole corpus with pinned .NET and Node, so annotated
  blocks run against the versions we ship, plus an external link check. It files
  one issue when something has rotted and closes it when the sweep is clean again.
  The link check skips what our documentation *names* rather than links to —
  localhost and private addresses, Stripe and GitHub OIDC endpoints cited as CSP
  origins, environments that come and go, and URLs with a placeholder in them.
  The exclusions are listed in the workflow; add to them rather than letting the
  sweep cry wolf.

A pull request that touches no documents collects no document tests, so the gate
costs nothing on code-only changes.

## Working on the harness

```bash docs-test=skip reason="the suite would be running itself"
scripts/docs-test.sh --tests-only
```

`docs-tests/tests/` unit-tests each check, and `test_suite.py` runs the whole
plugin over the deliberately broken documents in `docs-tests/tests/fixtures/` and
counts the outcomes. Those fixtures are excluded from the real corpus; when you
change them, update the expected counts at the top of `test_suite.py`.

To check a new language, add it to `DEFAULT_LANGUAGES` in
`docs-tests/docs_test/config.py`, write an evaluator beside `docs_test/data.py`
that returns a message (or `None`), and register it in `docs_test/fences.py`.
