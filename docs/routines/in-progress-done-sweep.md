# Routine: In Progress → Done sweep

A scheduled Claude Code routine. Once a day it makes sure every unfinished story has an agent on it,
in two repos:

- `nbon12/hoa_management_system`
- `nbon12/mmo-engine`

If an agent is already working on a story, the routine leaves it alone. If a matching session is
idle, the routine nudges it. If there is none, the routine starts a new cloud session. That agent
finishes what a session can do, and closes the story when it's done. The routine itself never closes
issues or changes code. It reports to you what it handed off and what is waiting on you.

## How it works

1. **Triage.** For each open story that shows work, the routine reads the issue, its PRs and its
   linked docs on `main`, then sorts it into one of four classes:

   | Class | Meaning | Gets an agent? |
   |---|---|---|
   | Finished | Looks done, but is still open | yes, to verify and close it |
   | Agent work | Something left that a session can do | yes, to do it |
   | Owner only | Everything left needs you: approvals, secrets, console steps, decisions | no, it goes in your summary |
   | Unclear | Can't tell | no, it goes in your summary |

2. **Look for an agent already on the story.** It checks, in this order:
   - **Cloud sessions, and local sessions with Remote Control.** First a session tagged
     `story:<repo>:<n>`. Otherwise a session whose title or latest summary names the issue; the
     routine then tags it, so the next run finds it exactly.
   - **`@claude` runs.** An in-progress run of the Claude workflow for that issue.
   - **Agents it can't see,** such as local sessions without Remote Control. A push or PR update for
     the story in the last 3 hours counts as an agent on it.
3. **Act.**

   | Agent | What the routine does |
   |---|---|
   | Working | Nothing. |
   | Waiting on you | Tells you. It never answers for you. |
   | Idle | Sends it the worker message. The message is queued, so it never interrupts a turn. |
   | None, archived or failed | Starts a new cloud session with the worker message, tagged `story:<repo>:<n>`. |

4. **The worker** checks the story against the done rules:
   - **Done:** it posts the evidence on the issue and closes it as completed. The project's
     **Item closed** workflow then moves the card to Done.
   - **Not done:** it does the remaining work through the repo's normal PR flow, then checks again.
   - **Steps only you can do:** it lists them on the issue and stops.

**Limits per run:**
- At most 3 new sessions and 5 messages.
- A story isn't dispatched again within 72 hours of its last dispatch.
- Reopening a story the sweep closed is a veto: the routine leaves it alone until you comment.

### Why not `@claude`

The routine counts a running `@claude` job as an agent on the story, but it doesn't start new work
that way. In this repo the Claude Action can read, edit, build, test and push. It can't close an
issue or check a PR's state. It also starts with no memory of earlier work, and its logs are public.
A cloud session has the full GitHub tools, shows up in your Claude app, and can be resumed.

## Setup (once)

1. **Project auto-move.** In <https://github.com/users/nbon12/projects/1>, open **⋯ → Workflows**. Set
   both **Item closed** and **Pull request merged** to Status **Done**. If `mmo-engine`'s issues are
   on a different project, do the same there.
2. **Create the routine** in Claude Code on the web (scheduled tasks):
   - **Name:** In Progress → Done sweep
   - **Repositories:** both repos above.
   - **Schedule:** daily at 6:47 AM America/New_York (`CRON_TZ=America/New_York 47 6 * * *`), after
     the 5:30 AM routines.
   - **Prompt:** the contents of [`.routines/done-story-sweep.md`](../../.routines/done-story-sweep.md),
     copied as it is.
   - **Notifications:** push on.

   The worker sessions it starts use the routine's environment and permission mode. That mode has to
   let an unattended session edit, commit, push and open PRs, as your other routines already do.
3. **Steering.** Worker sessions are titled `Story <repo>#<n>: …` and show up in your Claude app. You
   can watch or redirect them there. To stop the sweep from touching a story, reopen it after a sweep
   closure, or close it yourself.

## Prompt

The prompt is in [`.routines/done-story-sweep.md`](../../.routines/done-story-sweep.md). It includes the
worker message the routine sends to each agent.

## Notes

- **Cost.** Each run reads up to 50 issues per repo and starts at most 3 sessions. A worker is a full
  agent run, so the cap and the 72-hour cooldown are what limit the spend.
- **Messages to your own sessions.** A worker message arrives in an idle session as a new turn,
  marked as coming from another session. Archive a session to stop the sweep messaging it. If the
  story still needs an agent, the sweep then starts a new session for it.
- **Changing it.** The routine doesn't read the repo. Edit `.routines/done-story-sweep.md`, then
  paste the new version into the routine.
