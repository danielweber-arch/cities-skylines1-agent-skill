# Progress: Ashford Transit
currentPhase: P4
lastSave: Ashford-P3 (Ashford-P3.crp, 7,271,644 B, 2026-09-25 ~22:20 local)
completedPhases: [P1, P2, P3]
createdEntities:
  P1: nodes [23540, 26565, 32005, 17628, 19543, 24000, 19793, 1884] reusedNodes [31748, 28996] segments [22179, 752, 5514, 8212, 30098, 15758, 23993, 20076, 10915] buildings [] opIds []
  P2: nodes [4489,18907,20428,12450,4038,19220,26525,19698,21054,16378,30051,881,23593,24221,4258,19785,23837,24080,10692,3271,21641,31012,22030,22775] reusedNodes [32005,17628,19543,24000,19793,1884] segments [8919,22642,33421,35845,14046,662,34803,9947,34337,35954,10707,31087,33009,1908,29647,31563,31944,28634,20731,15906,36723,25730,20394,2590,7577,15389,25316,36668,11273,27879,20277,8053,9745,16384,27902,7144,36524,31267,10588,31148,32428,30808,11198,30692] blockCenters [(1760,1858),(1840,1858),(1920,1858),(2000,1858),(2080,1858),(1760,1938),(1840,1938),(1920,1938),(2000,1938),(2080,1938),(1760,2018),(1840,2018),(1920,2018),(2000,2018),(2080,2018),(1760,2098),(1840,2098),(1920,2098),(2000,2098),(2080,2098)] buildings [] opIds [ashford-d1-grid-01]
  P3: nodes [28072,23860,22583,27464,20301,4357,14503,22809] segments [19917 (power feed), 3561, 945, 33576 (power east), 3800, 35000, 22950, 10230 (pipes)] bulldozed [24965 (our dead-end power stub)] buildings [10643 Wind Turbine (1920,2260), 11872 Water Tower (1760,2162)] opIds []
  P4: nodes [] segments [] buildings [] opIds []
acceptance:
  P1: {roadAnomaliesNoDeadEnds: 0, localRoadComponents: 1, disconnectedLocalRoadComponents: 0, cityConnectedToOutside: true, problemsTotal: 0, maxNodeHeightDeltaVsHighway_m: 8.44}
  P3: {buildingsWithElectricityProblem: 0 (was 33), buildingsWithWaterProblem: 0 (was 2), buildingAnomalies: 0, ourFacilitiesProblems: none, problemsTotal: 2 (Crime 1, ElectricityNotConnected 1 on operator node 14810)}
  P2: {gridRoadAnomalies: 0, cityRoadAnomalies: 1 (roadTerrainCliff on operator-built segment 15994, not ours), localRoadComponents: 1, disconnectedLocalRoadComponents: 0, zoneBlockCount: 264, gridNodeY: [154.3, 174.4], steepestAdjacentPair_m: 8.51}
saves: [Ashford-P1, Ashford-P2, Ashford-P3]
operatorBuilt (not ours, do not bulldoze):
  - Richardson Avenue: Medium Road with Wide Sidewalks Trees from spine node (1560,1978) north to a bridge at (1786–2012, 3074–3194); last segment 15994 has a 40 m terrain cliff
  - zones and 28 growables around x 1592–1856, z 1838–2122 (27 ResidentialLow, 1 CommercialLow)
  - Nuclear Power Plant 48976, Advanced Wind Turbines 31239/32284, Water Intake 21582, ~52 pipes and a power line from the plant, Fire House 5680, Landfill 8062, Medical Clinic 43818, a Police Station, zoning of most of D1 (2698 ResidentialLow + 197 CommercialLow cells, 82 growables)
  - Water Outlet id 46572 at (3405, y 67, 2284), angle 162; water body is there, ~1.3 km east of D1
  - simulation running at speed 3 (operator unpaused)
openIssues:
  - pre-existing RoadNotConnected on highway nodes 7466 (1008,1952) and 11436 (1006,1992); not ours, watch whether outside traffic reaches the city in P6
  - /capture renders sky only (TODOS.md); no visual QA this run
  - money not exposed; budget by research costs, 20% reserve
  - set-simulation-speed paused:false returned "InvalidOperationException: Already in the same thread. Call directly" (P1 boundary); paused:true works; under test
