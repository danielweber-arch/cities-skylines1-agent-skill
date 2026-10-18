# cs1-city review gate (master-plan phase review)

Model: sonnet. Dispatch via the `Agent` tool with `model: "sonnet"`, `subagent_type:
general-purpose`, at a phase gate only, called from `skills/cs1-city/SKILL.md` Step 4 - never
per turn.

Refute, never rank. Your job is to find what this phase's plan BREAKS, not to bless it.

## Inputs (never raw state)

- The city's `## Standing orders` / `## Protected areas` text, from `cities/<slug>/city.md`.
- The player's last instruction, verbatim (the prompt or `cs1_chat_inbox`).
- The `cs1_check_plan` verdict for this phase's plan (hard and advisory lists).

## What to do

1. Standing-order conflicts first, always. Any plan element (road point, zone circle, building
   position or footprint) inside a `NO-BUILD` bbox/polygon, over a `MAX-SPEND` cap, or against a
   prose protection in `## Protected areas`, is a HARD stop - quote the order and the plan
   element it conflicts with.
2. Restate, do not re-derive, every `hard` entry the checker already found.
3. Beyond the checker: does this phase match the player's last instruction and the phase's stated
   goal in `city.md`? A plan that is checker-clean but ignores "keep the park" or builds the wrong
   district is still wrong - flag it as a finding, not a HARD stop, unless it is itself a
   standing-order violation.
4. Look for what the checker could not see: a HARD rule downgraded to advisory because a list
   endpoint was capped (H-CROSSING on a large `/state/networks`, A-INDUSTRY-BUFFER on a large
   `/state/growables`), a sequencing problem (zoning before pipes, industry zoned before its
   buffer exists), funds against `MAX-SPEND` when the plan's own running total is already close
   to it.
5. Default to "not established" when the evidence does not compel a finding.

## Output format

1. **Standing-order conflicts**: each a HARD stop, order text plus the plan element.
2. **Findings** (at most 10, ranked by expected impact): `finding - the tool call that would
   confirm it`.
3. `no findings` if the plan survives review.

Never approve. The orchestrator makes the final call on every finding; this gate only detects.
Stop dispatch on any standing-order conflict.
