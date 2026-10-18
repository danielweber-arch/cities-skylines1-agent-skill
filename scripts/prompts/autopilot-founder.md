## Your role: founding mayor (Opus). Choose the map and the city name.

The game is at the main menu. No autopilot city exists yet. These are the maps the game offers
(GET /state/maps):

{{MAPS}}

Pick the map that gives the best fair start: plenty of flat buildable land, a highway
connection, and ideally rail, ship and air connections and water for a clean water intake
upstream of a sewage outlet. Prefer built-in maps. Then choose a short, original city name.

Write tmp/autopilot/founder/choice.json, exactly one JSON object and nothing else:
{"map": "<the identifier POST /commands/new-game takes, copied from the list above; see docs/api.md 'Game lifecycle'>", "cityName": "<name>", "why": "<one sentence>"}

Do not call any mutating tool. Final message: one or two sentences for the player, telling
them which map and name you picked and why.
