# TAmpa session retrospective (2026-09-26/27)

What went wrong or slow, and what to do instead next time. Written for the next session to read
before starting. Detailed Situation/Action/Result/Rule entries are in `lessons.md`; this is the
short list of what cost the most time.

## Biggest time sinks

1. **Main-thread game calls (cost: 5 game restarts, 2 stuck objects, 227 stacked houses).**
   Every game call that can fire a UI or unlock event throws "Already in the same thread" on the
   main thread, part-way through, leaving half-finished state. We hit it five separate times: line
   name/colour, Metro Track milestone, building bulldoze, segment bulldoze, building on/off.
   **Next time:** route every mutation through `SimulationJob` / `SimulationManager.AddAction` from
   the start. Audit the bridge once for any remaining main-thread mutation
   (`NodeHelper.Rollback` still uses the old segment release) instead of finding them one at a time.

2. **Restarting the game for each bridge fix.** Each restart interrupted running agents and cost
   minutes. **Next time:** batch bridge changes; build and install them together; restart once.
   The player now says: no restarts during a play session.

3. **Relayed approvals don't pass the permission classifier.** Demolition and zoning changes by a
   subagent were blocked because the player's approval reached it only through me. Twice.
   **Next time:** any step that demolishes buildings or changes zoning runs in the main session,
   right after the player's own message approving that exact step. Ask for approval with the exact
   wording up front, listing the ids.

4. **Announcing before verifying.** Said "saved" before the file was written (save script only
   checked existence); announced a budget change a shell loop never sent. **Next time:** state it
   in chat only after the check passes (mtime newer than the request; the response `ok`).

5. **Shell quoting in zsh.** `set -- $pair` does not split in zsh; `for row in $(jq -c ...)` splits
   JSON on spaces. Both sent empty or broken requests. **Next time:** loops go in a `bash` script
   file, and JSON rows are read with `jq -c '.[]' | while IFS= read -r row`.

6. **Not saving immediately.** The player closed the game four minutes after the bus-lane swap,
   before the next save, and the change was lost. **Next time:** save right after every verified
   change, not at the end of a batch.

## Game mechanics we had to learn the hard way (now in lessons.md)

- Transport lines don't re-path after the track under them is fixed: recreate or edit the line.
- Rail and metro nodes refuse turns above about 45.8 degrees (`m_maxTurnAngle` 45). Keep bends
  at or below 40 degrees, 30 m straight at platform ends.
- A metro station twice in one line never paths: build one line per direction.
- A metro or feeder bus that parallels an existing line within about 150 m takes its riders
  instead of adding riders.
- The airport is demand-bound, not capacity-bound: a second airport or more attractions don't add
  plane passengers. City size does.
- Line ridership swings about 30% week to week: judge changes on weekly counts over 3+ weeks.
- `place-building` dry runs check nothing unless `validate:true`; they don't check collisions for
  ordinary buildings. Run a footprint guard before every placement.
- Bulldozing and rebuilding a road releases its zone blocks; repaint the zoning right after.
- `set-building-active` used to flip a flag only; it now calls `SetProductionRate` like the game.

## B27 airport transit + Bus 4 (time savers)

- **Route stop orders on the road graph before any dry run.** tmp/tampa/b27/route2.py (no U-turns)
  matched in-game line length within 3-5% and caught a 1.6 km leg from one badly placed stop. Two
  minutes of routing replaced what would have been a create / measure / redesign cycle (~10 min each).
- **One change per phase, then A/B/A.** Lines swing +-30% a week; only reverting the 142 stop showed
  it was costing Blue ~100 riders/week. Stage changes from the start rather than stacking them.
- **Keep a far-away unchanged line as a noise control** (line 97 here). Its 31% swing with nothing
  changed stopped a false revert of Bus 4 over a 142 dip.
- **Check stops for laneId 0**, not only line problem flags: a detached stop sat unnoticed on 142
  from B18 to B27.
- **After a stop edit, poll until length and target vehicles settle (~40 s) before saving.**
- **Game speed:** at speed 3 one game week is about 1.4 min of wall time, so 5 periods per phase
  take ~7-8 minutes. Budget measurement time up front; wait with python sleep loops inside one
  Bash call (fewer tool calls than polling from the shell).

## Process that worked

- Planner agent (read-only) → builder agent (with explicit gates) → orchestrator verifies live.
- Refute-only second opinion on figures, with numbers withheld.
- Every build logged in `transit-progress.md` with ids, so anything can be reverted.
- Dry run, check `createdNodeIds`, diff line ids before and after every create or delete.

## Bridge gaps still open (see TODOS.md)

No read of population (citizens.count includes tourists), education/age mix, line paths, service
capacity, owned tiles. No zone-safe road upgrade, no roundabout primitive, no unlock or cost check
on `place-building`.
