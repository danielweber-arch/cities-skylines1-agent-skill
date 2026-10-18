# templates/layouts/

Reusable road-network layout templates for `cs1_stamp_layout` (see `wave2-spec.md` section 5).
**Geometry is UNVERIFIED IN GAME** for every template here: these were authored and validated
offline on 2026-10-01 with the game not running. `validate.py` checks internal consistency
(segment length, node spacing, grade) but cannot check that the real bridge accepts the shapes,
that ramps actually clear each other, or that curvature (not modelled here; every segment is a
straight chord) is driveable.

## Templates

| Template | Footprint (m) | For |
|---|---|---|
| `roundabout-small.json` | 320 x 320 | Local/collector junction, single-lane ring, r=30 m, 4 Basic Road legs. |
| `roundabout-large.json` | 440 x 440 | Collector/arterial junction, r=60 m, 4 Medium Road legs. |
| `diamond.json` | 640 x 640 | Highway x arterial, 4 direct ramps, arterial crosses over on an elevated deck. Smallest footprint of the 3 highway-arterial interchanges here. |
| `trumpet.json` | 640 x 640 | One terminating road meets a through highway: 1 direct ramp + 1 loop ramp. |
| `parclo.json` | 640 x 640 | Partial cloverleaf: 3 direct ramps + 1 loop ramp (NW quadrant). Between diamond and cloverleaf in footprint and capacity. |
| `cloverleaf.json` | 1040 x 1040 | Full cloverleaf: 4 loop ramps, no at-grade turns. Largest footprint; watch for ramp weaving (see its notes). |
| `collector-distributor.json` | 320 x 460 | A Large Road running parallel to a highway, moving ramp weaving off the mainline. |
| `cargo-hub-frontage.json` | 640 x 190 | Medium Road collector + Basic Road frontage loop + a parallel Train Track spur, for an industrial/cargo block. |
| `bus-metro-interchange-block.json` | 400 x 200 | Road-only block (Medium Road spine + Basic Road access loop) sized for a Metro Station or Bus Depot; place the building and transit lines separately. |

Every template's `footprint` field in the JSON is the authoritative bbox; the table above rounds it.

## Schema

See `wave2-spec.md` section 5 for the full field reference. Summary: `anchor` is the layout's local
origin (world point = anchor + rotate(local, angleDeg), `x' = x*cos(a) + z*sin(a), z' = -x*sin(a) +
z*cos(a)`, standard CS1 clockwise-from-above angle convention). `roles` maps a logical role (e.g.
`main`, `ramp`, `cross`) to a `default` prefab and an `allowed` list; `cs1_stamp_layout`'s caller may
override a role's prefab with anything in `allowed`. `nodes` are named local points with
`x`, `z`, `elevation` (elevation 0 = ground). `segments` connect two named nodes by role.

## Geometry rules applied here (binding, from wave2-spec.md section 5)

- **Every segment <= 100 m.** Longer logical connections (e.g. a 200 m highway span, a 291 m loop
  ramp) are split into multiple sub-100 m segments with intermediate nodes (linearly interpolated
  x/z/elevation), named `<from>_<to>_s<segmentIndex>_i<k>`, so a later `build-grid` or repair call
  snaps onto a real node instead of the middle of a long chord.
- **Ramps that cross the mainline carry elevation on both nodes of the crossing pieces**, rising or
  falling over >= 100 m of run per side to clear an 8 m crossing at <= 8% grade (lessons.md:716). In
  `diamond.json`, `parclo.json` and `cloverleaf.json` the crossing arterial goes up and over the
  highway (not the reverse) via a short elevated deck (`crossDeck` role); ramps climb to/from that
  deck's elevation using ordinary ground-prefab nodes with nonzero `elevation`, the same practice
  used for a real build in Portville (lessons.md "a ground prefab with node elevation does not raise
  the terrain sample" / "Portville IC built" entries, 2026-09-27) rather than an "Elevated" ramp
  prefab, since no elevated Highway Ramp prefab name is confirmed anywhere in this repo (see below).
- **Every prefab name appears verbatim** in at least one of docs/api.md, lessons.md, knowledge.md,
  transit.md, or a `*-master-plan.md` file. Confirmed and used:

| Prefab | Confirmed in |
|---|---|
| `Highway` | city-ashford.md, portville-master-plan.md, knowledge.md, lessons.md, TODOS.md, tampa-master-plan.md, city.md (now cities/portville/city.md), SKILL.md, transit-progress.md |
| `Highway Ramp` | city-ashford.md |
| `Basic Road` | city-ashford.md, portville-master-plan.md, knowledge.md, lessons.md, TODOS.md, tampa-master-plan.md, city.md, SKILL.md, transit-progress.md, docs/api.md |
| `Medium Road` | city-ashford.md, portville-master-plan.md, knowledge.md, lessons.md, TODOS.md, tampa-master-plan.md, city.md, transit-progress.md |
| `Large Road` | city-ashford.md, portville-master-plan.md, knowledge.md, lessons.md, TODOS.md, tampa-master-plan.md, city.md, transit-progress.md |
| `Medium Road Elevated` | progress-portville.md (now cities/portville/progress.md), lessons.md, TODOS.md, tampa-master-plan.md, city.md, transit-progress.md |
| `Train Track` | progress-portville.md, lessons.md, TODOS.md, city.md, transit-progress.md, docs/api.md |
| `Bus Depot` | city-ashford.md, portville-master-plan.md, progress-portville.md, lessons.md, TODOS.md, city.md, transit-progress.md |

  **Not used because not confirmed verbatim anywhere searched:** `Highway Elevated`,
  `Highway Ramp Elevated` (neither string appears in any `.md` file in this repo as of 2026-10-01).
  This is why the elevated highway-crossing templates (`diamond`, `parclo`, `cloverleaf`) put the
  elevation on the *arterial* crossing over the highway (`Medium Road Elevated`, which IS confirmed)
  rather than elevating the highway itself, and why ramps use node elevation on the plain ground
  `Highway Ramp` prefab instead of a nonexistent elevated variant.
- **No two distinct nodes closer than 8 m** (the bridge's snap distance) unless they are the same
  point, checked by `validate.py`.

## Validator

```bash
python3 templates/layouts/validate.py
```

Loads every `*.json` here and asserts: required schema fields present; every segment's `from`/`to`
resolves to a real node; every segment's XZ chord length <= 100 m; every segment's grade
(`|dElevation| / xzLength`) <= 8%; no two distinct-named nodes closer than 8 m unless identical; and
that each role's `default` prefab is itself in that role's own `allowed` list. Turbine (the
C#/compiled side) is skipped entirely: this validator is pure Python over the JSON files, nothing
here touches the game or a compiled assembly.

Last run (2026-10-01, game not running): **all 9 templates passed.**
