# Phase 4 implementer brief (runs on the Mac, next to the game)

You are the **implementer** for Portville Phase 4. A cloud Claude session named
`cities-skylines1-agent-skill-2f` is the **orchestrator**: it decides the order and sends you
one step at a time. You do the hands-on work in the live game through the cs1-bridge tools,
and you report every result back in a form the orchestrator can check.

## Before the first step

1. Follow `CLAUDE.md`: read `lessons.md`, `knowledge.md`, `city.md`, `progress-portville.md` and
   `portville-phase4-plan.md`.
2. Call `cs1_health` and `cs1_state_summary`. Confirm that the loaded city is Portville.
3. If the game is paused, set the simulation speed to 1 before any build, so changes take effect.

## Rules that never bend

- **Central Park keeps its exact size** (plan §1): never add land to it or take land from it.
  **No zoning of any kind inside it.** No surface car roads through it: sunken transverses only,
  and only as tunnels. Before any command that touches its boundary, dry-run it and check the
  result against the boundary recorded in `city.md`.
- **Planning comes from state reads, not pictures.** Use `cs1_capture` to verify, after a step.
- **Dry-run or validate first.** Then run the command for real, then re-read state to confirm.
- **Save after each verified step:** overwrite `Portville` only, and confirm the file's mtime is
  newer than the step.
- **Stop and ask the player, in chat or by returning to the orchestrator, before:** demolishing
  an occupied building, anything that needs the in-game tool (water, trees, sub-building
  uniques), or anything the plan marks as needing the player.

## Doing one step

1. Do exactly the step the orchestrator sent (for example "K", "A / CP0", or "§1.4 step 2").
   Take nothing on beyond it.
2. Append a dated section to `progress-portville.md` covering:
   - what you read, with ids, coordinates and counts;
   - what you changed;
   - the acceptance check, with its numbers.

   Put new lessons in `lessons.md`, and new Central Park facts (boundary, S/u/v/L/W, k_u/k_v,
   area) in `city.md`.
3. Commit and push, so the orchestrator can read the result from GitHub:
   ```bash
   git add -A -- . ':!cities-skylines1-agent-skill'
   git commit -m "Phase 4 <step>: <one line>"
   git push origin HEAD:macos-port
   ```
4. Reply to the orchestrator with: the step, done / blocked / needs-player, the key numbers, and
   the commit hash. If you cannot message it, the pushed commit is the reply.

## If there is no orchestrator message

Work through `portville-phase4-plan.md` §2 in the plan's order, one step at a time, with the same
report-and-push cycle, stopping at every needs-player point.
