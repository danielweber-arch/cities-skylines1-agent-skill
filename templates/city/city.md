# City: <name>

id: unknown
name: <name>
map: unknown
bindOnLoad: true

Goal: <population target>, no unaddressed problems
Style: <one line>
Layout: <one line>
Industry: <placement rule>
Must have: <services>
Avoid: <hazards>
Save cadence: <rule>
Map: <map name or "unknown"> | Save file: <name> | Started: <date>

## Standing orders

<!--
Machine-readable lines only go here, one per line, exactly this syntax (parsed by
mcp-server/src/checker.ts per wave2-spec.md section 4):
- NO-BUILD <name>: bbox <minX>,<minZ> <maxX>,<maxZ>
- NO-BUILD <name>: polygon <x>,<z>;<x>,<z>;...
- MAX-SPEND <number>
Everything else under this heading is prose for the reviewer, not parsed.
-->

## Protected areas

<!-- Prose description of each protected area goes here. Pair every protected area
     with a machine-readable "- NO-BUILD <name>: bbox ..." line above once the
     boundary is known. -->

## Research summary

<!-- Confirmed facts about prefabs, costs, milestones, unlocks on this install. -->

## Site survey

<!-- Baseline state read from the bridge before any build: highway attach points,
     water, rail, owned tiles. -->
