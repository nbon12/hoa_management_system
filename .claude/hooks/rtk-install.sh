#!/usr/bin/env sh
# SessionStart hook: ensure RTK (Rust Token Killer) is installed.
#
# Cloud / web Claude Code sessions run in ephemeral containers that are
# reclaimed between sessions, so the rtk binary does not persist. This script
# (re)installs it at the start of every session. It is idempotent and must
# never fail the session: on any error it exits 0 and Bash commands simply run
# unproxied for that session.
#
# Installs to ~/.local/bin/rtk (the install script's default, no sudo needed).
set -u

RTK_BIN="${HOME}/.local/bin/rtk"

# Already available? Nothing to do.
if command -v rtk >/dev/null 2>&1 || [ -x "$RTK_BIN" ]; then
  exit 0
fi

# Network required. Keep stdout clean (SessionStart stdout is added to context);
# report status on stderr only.
if curl -fsSL https://raw.githubusercontent.com/rtk-ai/rtk/refs/heads/master/install.sh | sh >/dev/null 2>&1; then
  echo "RTK installed: $("$RTK_BIN" --version 2>/dev/null || echo unknown)" >&2
else
  echo "RTK install skipped (could not download); Bash commands run unproxied this session." >&2
fi

exit 0
