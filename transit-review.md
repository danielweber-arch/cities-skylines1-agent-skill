# TAmpa transit review

Rule: add to the existing transport, never change it. Changes to existing infrastructure go under
Proposals needing approval.

## Baseline (gameTime 2031-11-16, before any change; save TAmpa-transit-before.crp)
- Citizens 25,674. Demand R 4 / C 11 / W 5. Traffic flow 61%, mean segment density 13.6.
- Policies (city): RecreationBoost, Recycling, EducationBoost, FreeTransport, TaxLowerResLow,
  TaxLowerResHigh, TaxLowerComLow, BigBusiness.
- Budgets: PublicTransport 100; Bus 150; Ship 150; Plane 150; Train 100; Metro 100.
- Weekly passengers (info-view average): bus 1,154 (residents 916, tourists 238); ship 40;
  airplane 112; total 1,306 (residents 948, tourists 358).
- Lines: 7 bus lines, 5 complete, 67 stops, 93 vehicles.

| id | line | stops | length m | vehicles | budget | riders/wk |
|---|---|---|---|---|---|---|
| 94 | Bus 3 | 18 | 8,840 | 31 | 140 | 603 |
| 203 | Bus 6 | 17 | 13,336 | 34 | 100 | 164 |
| 246 | Bus 2 | 15 | 2,267 | 6 | 100 | 160 |
| 179 | Bus 1 | 11 | 4,302 | 11 | 100 | 157 |
| 60 | Bus 4 | 4 | 4,135 | 11 | 100 | 72 |
| 22 | Bus 5 | 1 | 0 | 0 | 100 | 0 (incomplete, LineNotConnected) |
| 189 | Bus 7 | 1 | 0 | 0 | 100 | 0 (incomplete, LineNotConnected) |

- Stations with no city line: Train Station 31333 (2994,1177) active; Train Station 47366
  (3256,4414) inactive, no electricity; metro stations 1005, 18470, 32802 with only 3 metro track
  pieces; Airport 29539 (1214,2315); Harbors 23322, 31587; Cargo Harbor 47308; Cargo Center 17763
  inactive, no electricity.

## Check 1 (gameTime 2032-09-22, ~10 real minutes after the new lines)
| measure | baseline | check 1 |
|---|---|---|
| citizens | 25,674 | 31,753 |
| bus riders/week | 1,154 | 1,940 (+68%) |
| all PT riders/week | 1,306 | 2,178 (+67%) |
| bus riders per citizen | 4.5% | 6.1% |
| traffic flow | 61% | 66% |
| avg segment density | 13.6 | 12.0 |

Per line (riders/week, riders per bus): Bus 3 618 (15.8), Bus 1 242 (11.0), Bus 2 234 (15.6),
Bus 6 230 (6.8), Bus 11 180 (6.4, new), Bus 12 170 (8.9, new), Bus 9 89 (8.9, new),
Bus 13 83 (5.9, new), Bus 10 62 (5.6, new), Bus 4 43 (7.2, 50% budget).
Caveat: the game's figure is a moving average; lines created ~10 minutes earlier start from 0 and are
still under-counted. No train riders yet at 31333.

## Check 2 (gameTime 2032-11-21)
| measure | baseline | check 2 |
|---|---|---|
| citizens | 25,674 | 33,070 |
| bus riders/week | 1,154 | 2,248 (+95%) |
| all PT riders/week | 1,306 | 2,425 (+86%) |
| bus riders per citizen | 4.5% | 6.8% |
| traffic flow | 61% | 64% |
Per line: Bus 3 617, Bus 2 295, Bus 11 282, Bus 1 224, Bus 6 223, Bus 12 218, Bus 13 157, Bus 9 125,
Bus 10 61 (flat since check 1: removed), Bus 4 57 (kept at 50%: only line near Grand Mall).
Actions: deleted Bus 10 (line 5, ours) under the user's 75-rider rule; Bus 2 budget 250 -> 300
(19.7 riders per bus, the best).

## Findings
See tmp/tampa/analysis-bus.md and the rail analysis in this conversation's record (tmp/tampa/rail-actions.json).

## Change plan
(pending)

## Proposals needing approval
(pending)
