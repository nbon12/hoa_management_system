#!/usr/bin/env bash
# Executable documentation tests: run the examples and claims in our Markdown.
#
#   scripts/docs-test.sh --changed origin/main   # what the PR gate runs
#   scripts/docs-test.sh --all                   # what the weekly sweep runs
#   scripts/docs-test.sh --all --no-exec         # static checks only, no commands run
#   scripts/docs-test.sh --tests-only            # just the harness's own tests
#   scripts/docs-test.sh --changed HEAD~1 -- -vv # extra pytest arguments
#
# Needs Python 3.11 or newer. See docs-tests/README.md for the annotation contract.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VENV="$ROOT/.venv-docs-tests"
REQUIREMENTS="$ROOT/docs-tests/requirements.txt"
PYTHON="${PYTHON:-python3}"

mode=""
base_ref=""
pytest_args=()
exec_args=()
targets=()

usage() {
  # The comment block at the top of this file, up to the first line that is not one.
  sed -n '2,/^[^#]/p' "${BASH_SOURCE[0]}" | sed -n 's/^# \{0,1\}//p'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --all) mode="all"; shift ;;
    --tests-only) mode="tests-only"; shift ;;
    --changed) mode="changed"; base_ref="${2:?--changed needs a base ref}"; shift 2 ;;
    --no-exec) exec_args+=(--docs-no-exec); shift ;;
    -h|--help) usage 0 ;;
    --) shift; pytest_args+=("$@"); break ;;
    *) echo "unknown argument: $1" >&2; usage 1 ;;
  esac
done

if [[ -z "$mode" ]]; then
  mode="changed"
  base_ref="origin/main"
fi

# ── Environment ───────────────────────────────────────────────────────────────
# The venv is rebuilt whenever requirements.txt changes, so a dependency bump
# lands without anyone remembering to reinstall.
if ! "$PYTHON" -c 'import sys; sys.exit(0 if sys.version_info >= (3, 11) else 1)'; then
  echo "docs-test: needs Python 3.11 or newer; $($PYTHON -V 2>&1) is $(command -v "$PYTHON")." >&2
  echo "           Set PYTHON=/path/to/python3.12 to point at a newer one." >&2
  exit 2
fi

stamp="$VENV/.requirements.sha256"
want="$(shasum -a 256 "$REQUIREMENTS" | cut -d' ' -f1)"
if [[ ! -x "$VENV/bin/pytest" || ! -f "$stamp" || "$(cat "$stamp")" != "$want" ]]; then
  echo "docs-test: installing $(basename "$REQUIREMENTS") into $(basename "$VENV")"
  "$PYTHON" -m venv "$VENV"
  "$VENV/bin/python" -m pip install --quiet --upgrade pip
  "$VENV/bin/python" -m pip install --quiet -r "$REQUIREMENTS"
  echo "$want" > "$stamp"
fi

export PYTHONPATH="$ROOT/docs-tests${PYTHONPATH:+:$PYTHONPATH}"

# ── Targets ───────────────────────────────────────────────────────────────────
# `mapfile` would be neater, but macOS still ships bash 3.2. The listing is
# captured rather than piped so that a corpus that cannot be resolved — a broken
# config, say — fails the run instead of quietly reporting zero documents.
read_targets() {
  local listing
  if ! listing="$("$VENV/bin/python" -m docs_test.corpus "$@")"; then
    echo "docs-test: could not work out which documents to test" >&2
    exit 2
  fi
  targets=()
  if [[ -n "$listing" ]]; then
    while IFS= read -r line; do
      targets+=("$line")
    done <<< "$listing"
  fi
}

if [[ "$mode" == "tests-only" ]]; then
  echo "docs-test: the harness's own tests only"
elif [[ "$mode" == "all" ]]; then
  read_targets --all
  echo "docs-test: ${#targets[@]} documents (full corpus)"
else
  git -C "$ROOT" rev-parse --verify --quiet "$base_ref" >/dev/null \
    || { echo "docs-test: $base_ref is not a ref in this repository" >&2; exit 2; }
  read_targets --changed "$base_ref"
  echo "docs-test: ${#targets[@]} documents changed against $base_ref"
  for target in ${targets[@]+"${targets[@]}"}; do echo "  $target"; done
fi

cd "$ROOT"
exec "$VENV/bin/pytest" \
  -c "$ROOT/docs-tests/pytest.ini" \
  --rootdir "$ROOT" \
  ${exec_args[@]+"${exec_args[@]}"} \
  ${pytest_args[@]+"${pytest_args[@]}"} \
  docs-tests/tests \
  ${targets[@]+"${targets[@]}"}
