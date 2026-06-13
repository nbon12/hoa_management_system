#!/usr/bin/env sh
# PreToolUse(Bash) hook: route the command through RTK so its output is
# token-optimized before it reaches the model context.
#
# rtk reads the tool payload on stdin and prints a `hookSpecificOutput` JSON
# rewrite (e.g. `git status` -> `rtk git status`). If the command is not a
# rewrite target, rtk prints nothing and exits 0. If rtk is not installed
# (e.g. the SessionStart installer could not reach the network), we pass
# through unchanged: exit 0 with no output, so Claude runs the original
# command untouched.
RTK="$(command -v rtk 2>/dev/null || true)"
if [ -z "$RTK" ] && [ -x "${HOME}/.local/bin/rtk" ]; then
  RTK="${HOME}/.local/bin/rtk"
fi
[ -n "$RTK" ] && [ -x "$RTK" ] || exit 0

exec "$RTK" hook claude
