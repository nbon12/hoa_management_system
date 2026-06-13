# RTK - Rust Token Killer

**Usage**: Token-optimized CLI proxy (60-90% savings on dev operations). Most
commands are auto-rewritten to `rtk <cmd>` by the Claude Code PreToolUse hook,
so no action is needed for normal use.

## Meta commands (run rtk directly)

```bash
rtk gain              # Show token savings analytics
rtk gain --history    # Command usage history with savings
rtk discover          # Analyze Claude Code history for missed opportunities
rtk proxy <cmd>       # Run a raw command without filtering (debugging)
```

## Verify

```bash
rtk --version         # Should print: rtk X.Y.Z
which rtk             # Verify the binary resolves
```

Setup lives in `.claude/settings.json` (SessionStart installer +
PreToolUse(Bash) rewrite hook) and `.claude/hooks/`.
