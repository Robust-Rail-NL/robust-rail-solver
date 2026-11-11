# Release notes

## 2.3.1 — 2026-11-11

A fix-only patch release. No change to the interchange format (plans still declare `schemaVersion: 2`) and no change to the output besides the fix below.

### Fixes

- **Stale Reverse arc durations for a freshly computed route (#81).** `RoutingGraph.ComputeRoute` returned a route fresh from Dijkstra with its `Arcs` still pointing at the graph's own shared `Arc` objects. The next Dijkstra run, for any train, re-priced those arcs and overwrote a Reverse arc's `Duration` with the other train's `ReversalDuration`. #55 had fixed this for a cache hit, but not for the route that populates the cache. `ToPlan()` could then lay out a route's Reverse piece with the wrong length, so the pieces no longer summed to the route's end time. The solver logged this as `Move piece for shunting unit N at L (S-E) is anchored Gs away from its own computed duration (Ds) - the route's pieces don't sum to its trusted total; investigate.`, with a start time after the end time in the worst case (`450-420`). It was observed on intermediate plan snapshots written during the search of the Kleine Binckhorst `example1` scenario on an `edge` build; the final plan for that scenario was not affected, and 2.3.0 itself does not log it there. A fresh route now snapshots its Reverse arcs for its own train, as a cache hit already did. A regression test covers it.

Plans that reverse in place could in principle change: a route whose Reverse arc had been re-priced by another train now keeps its own train's duration.

### Compatibility

Same as 2.3.0: `schemaVersion` 2, for use with an evaluator of the same `schemaVersion` (2.3.0 or later).

## 2.3.0 — 2026-11-11

No change to the interchange format: plans still declare `schemaVersion: 2`, and it is meant for use with an evaluator of the same `schemaVersion` (2.3.0 or later). The only addition to the output is one extra line on stdout (see "Machine-readable search statistics").

### Machine-readable search statistics (#74)

At the end of simulated annealing the solver now prints one more line, after the existing "Finished after" and "Neighbors visited" lines, which are unchanged:

```
STATS search_seconds=<s> neighbors=<n> iterations=<n> feasible=true|false time_to_best_seconds=<s>
```

`time_to_best_seconds` is the time at which the best cost was last improved, i.e. when the final plan was found. Both times are measured from program entry (a clock started first thing in `Main`), so they include problem parsing and the tabu phase, not just annealing. Without a started clock (a test calling `Run` directly) they fall back to the annealing stopwatch. Experiment scripts can read this line instead of parsing the human-readable output.

### Fixes

- **Crash for a standing train's first service (#75, #79).** On a scenario with servicing, a train that is already standing in the yard at the start and needs a service could make `ServiceMachineSwitchMove.GetMoves` throw a `NullReferenceException`, which ended the process with exit code 139 (found on a generated 4-train scenario with servicing, and reproduced on 2.2.0). Service switch moves are now skipped for a standing train's first service. A regression test covers it.
- **Two crashes in `ToPlan()` on departures without a route (#65, #66, #67).** A train that genuinely has no route out of its resting track used to crash the solver (`ArgumentNullException` when a departure's route set contained `Route.Invalid`, and an `IndexOutOfRangeException` on `route.Tracks[0]` once that was fixed). The solver now reports such a scenario as infeasible. `RoutingGraph.ComputeRoute` also no longer returns a corrupted copy of a cached `Route.Invalid`: a cache hit returns the sentinel itself. The departure's whole action set is suppressed when any of its routes is invalid, and the failure is recorded in a new `unroutableDepartures` diagnostic, the same pattern as `zeroResourceMoves` and `unverifiedSplitPlacements`. Regression tests added for both.

### Exact lengths (#72)

Track and unit lengths were truncated to whole metres, while the evaluator compares exact lengths, so the solver accepted parkings that the evaluator rejected as too long. Lengths are now kept as whole millimetres internally, rounded half away from zero like the evaluator's `llround`, with a warning for a length with more than 3 decimals. Input, output and the track-length violation sum stay in metres.

Plans can change: a layout that fitted only because of the truncation is no longer produced, and an exact fit that used to be rejected can be.

### Compatibility

`schemaVersion` 2 and the evaluator's length rule from 2.3.0 (exact integer millimetres, #39), which the solver now follows (#72). No other format change.

### Reproducibility note

The solver-versus-planner experiment grid (robust-rail-general, `experiments/issa-grid/`) ran the build `HIP 2.2.0-edge+20261106.965fd71`, which has the same source tree as this release. Results from that experiment can be attributed to this version.

## 2.2.0 — 2026-09-26

Adopts interchange **schemaVersion 2** (see `SCHEMA_CHANGELOG.md` in
`robust-rail-general`): every plan this solver emits now declares
`schemaVersion: 2` and uses a real, explicit `Reverse` action for an
in-place reversal instead of embedding it in a `Move`. Unlike 2.1.0, this
**is** a lockstep release with the evaluator — see "Compatibility" below.

### Explicit reversals: the `Reverse` action (#46, #51)

An in-place reversal used to be invisible to routing whenever a train's
resting side already matched its desired departure side (same-side reuse,
most dead-end/single-access tracks): never priced, never emitted as a real
action, only ever showing up as silent schedule padding.

- `RoutingGraph.ComputeRoute`, `PlanGraph`'s `ComputeRouting` calls, and
  `SimpleHeuristic`'s `RoutePossible` probes now all search from the
  train's true resting vertex/side instead of guessing or hardcoding one.
- A departure's terminal hop — routing onto a fixed `DepartureTask`, with
  no further leg to carry a corrective reversal — gets its own routing mode
  (`RouteDestination.ReadyToDepart`) so this last-hop case is covered too,
  closing #51's gap.
- A cached `Route`'s `Reverse` arc used to keep the duration/cost of
  whichever train first computed it, silently reused for a different train
  reusing that route. Now recomputed per reusing train.
- A `Move`/`Reverse`/`Move` split's per-action durations now match the
  evaluator's own duration model (#52).
- `Plan.Feasibility`/`Cost`/`CostDetails`/`Origin` are now populated from
  the graph's real `SolutionCost` instead of schema-only placeholders
  (`Unknown`/`null`/`null`/`null`).
- A `Track` claiming `parkingAllowed`/`sawMovementAllowed` with zero length
  is now rejected at load, instead of silently mis-costed later.

### Fixes

- **`Time`'s `ulong` conversion no longer wraps on a negative value
  (#60).** A negative `Time` (a legitimate intermediate, e.g. from a slack
  calculation) used to silently wrap to a huge unsigned value when crossed
  into the plan's wire format, corrupting the emitted timestamp instead of
  failing at the actual problem. A `TrainType` duration field that's
  negative from scenario config is now also rejected at construction,
  before it can reach the same conversion.
- **Startup crash when `PlanPath` is a bare filename (#53).**
  `Path.GetDirectoryName` returns `""` for a bare filename, and
  `Directory.CreateDirectory("")` throws.
- **`BipartiteGraph`'s degree-1 pruning swapped arrival/departure indices
  on its departure-side pass (#49).** Silently paired vertices that were
  never actually shown to be uniquely adjacent. Thanks to
  [@MayteSteeghs](https://github.com/MayteSteeghs).

### Hardening

- `Side` is a 4-valued type (`None`/`A`/`B`/`Both`), not a 2-valued enum —
  several `x == Side.A ? … : …` call sites silently treated `None`/`Both`
  as `B`. Added `Debug.Assert` guards where that assumption actually holds,
  and documented the one deliberate exception (an `InStanding` train's
  resting side, which legitimately carries `Side.None`).
- A plan write now warns if a train split doesn't happen immediately after
  arrival — a defensive tripwire for a shape no scenario produces today,
  not a fix for an observed bug.

### Performance

- `Storage`'s per-track index map was recomputed and separately allocated
  in every one of the O(track count²) `Storage` instances `RoutingGraph`
  creates. Computed once and shared instead, cutting O(N³) memory to O(N).
- Dropped the route cache's empty-occupation pre-warm: measured 0 of 90
  pre-warmed entries ever retrieved on a busier fixture, vs. 26/90 on a
  much lighter one — pure upfront cost that gets less useful as load
  increases, not more.
- `Dijkstra`'s per-call reset now clears only the vertices the previous
  call actually touched (amortized constant time), instead of sweeping the
  whole graph (linear time) — sitting on local search's hot cache-miss path.

### Repo hygiene

- Local git hooks are now tracked under `.githooks/`, opt-in via
  `core.hooksPath`; a CSharpier check was added to the pre-push hook
  (CSharpier bumped to 1.3.0).
- Wholesale-dead `POS`/`TimeInterval` types (a standalone experiment with
  zero callers) prefixed `Unused`, not deleted (yet).
- Removed the dead `dev` branch from the .NET workflow's triggers.
- `docker-push.sh`/`docker-push-edge.sh` now work from any directory, gained
  registry build caching, and log out of `ghcr.io` when they exit.

### Compatibility

Every plan this version emits declares `schemaVersion: 2` unconditionally
and uses real `Reverse` actions wherever a reversal occurs — this is not
configurable. Requires evaluator 2.2.0 or later: an evaluator release that
predates schemaVersion 2 support does not reject such a plan outright —
`Reverse` is an unrecognized enum value, silently dropped by
`ignore_unknown_fields`, and the action disappears from evaluation with no
error. Fixed in evaluator 2.2.0 (evaluator#28), released 2026-09-26.

### Publishing

Versioned from `HIP.csproj`'s `<Version>`, bumped via `bump-version.sh`
(see `CONTRIBUTING.md`), pushed to `ghcr.io/robust-rail-nl/hip` via
`docker-push.sh`.

## 2.1.0 — 2026-09-11

A fix-focused release. No interchange-format changes — this is not a
lockstep release with generator/evaluator.

### Two of 2.0.0's known limitations are fixed

Both fixtures called out in 2.0.0 as expected-to-fail now produce valid
plans:

- **#13** — a delayed Arrival now gets a real duration instead of a trailing
  Wait, fixing `6t_custom_example3`'s non-parking-track parking behavior.
- **#14** — outStanding trains are now costed against the scenario end time,
  so the solver no longer produces free-overrun plans that used to trip a
  diagnostic-quality bug on the evaluator side (evaluator#6).

### standingIndex is now honoured (#18)

2.0.0 read `standingIndex` off the wire but didn't act on it. Multiple
inStanding trains sharing a track are now ordered by it, narrowed to only the
genuinely unsafe multi-inStanding-per-track cases.

### Other fixes

- **#24** — a departure Move action is no longer emitted for an empty route;
  a genuinely resourceless Move is now a hard failure on the final delivered
  plan rather than a silent no-op.
- **#26** — fixed split-during-a-move placing units on the wrong track side;
  a plan with an unverified split placement is now refused rather than
  delivered.
- **#32** — task types are now registered from inStanding trains, not just
  arriving ones.
- **#31** — an arriving or departing train whose entry/exit track part isn't
  actually a `GateWay` (e.g. a typo'd ID pointing at an ordinary RailRoad or
  Switch) used to silently vanish from the problem instance instead of
  failing to parse. Now throws, naming the offending train and track part.

### Config: SimulatedAnnealing key rename (#30)

`IterationsUntilReset` held the hard iteration cap and `Reset` held the
no-improvement reset threshold — backwards from what they suggested, and
inconsistent with TabuSearch's naming. Renamed to `MaxIterations` /
`IterationsUntilReset` respectively. `Config.ReadFrom` still accepts the old
pair with a deprecation warning; a config setting all three keys is rejected
as ambiguous.

### Hardening

Internal invariant violations are exceptions with descriptive messages now,
not bare asserts — including a fixed spurious assertion failure in
`PlanGraph.CheckGraphStructure` for uneven-depth combines.

### Repo hygiene

- Added the `edge` image channel (`docker-push-edge.sh`) for running
  not-yet-reviewed fixes without waiting on a `main` PR — see
  `CONTRIBUTING.md`.
- CI now reports test coverage, without gating on it.
- Startup now prints the full version specifier, including build metadata.

### Publishing

Unchanged from 2.0.0: versioned from `HIP.csproj`'s `<Version>`, pushed to
`ghcr.io/robust-rail-nl/hip` via `docker-push.sh`.

## 2.0.0 — 2026-08-20

This is the solver's (HIP's) slice of the shared 2.0.0 release: the same
interchange format, tagged together, across `robust-rail-generator`,
`robust-rail-solver` (HIP) and `robust-rail-evaluator` (TORS). The full
cross-repo picture — verification evidence, what each repo changed, and the
decisions behind the schema — lives in `scenario-planning-inputs`'
`docs/roadmap-2.0.0.md`.

### Protobuf is gone

The solver no longer reads or writes protobuf. `Location`, `Scenario` and
`Plan` are now deserialized directly from the JSON interchange format via the
records in `ServiceSiteScheduling/Interchange/` (renamed from `NoProto` —
named for what it wasn't, a migration leftover, not what it is). `Converter.cs`
and `DeepLook` mode, both protobuf-shaped, are retired along with the
generated `.cs` files.

### Interchange format: breaking changes from the pre-2.0.0 shape

Anything feeding `location.json`/`scenario.json` to the solver against the
pre-migration shape needs to account for:

- **`displayName` → `typePrefix` + `carriages`.** A train unit type is
  identified by the pair, keyed via `typesByPrefixAndCarriages`, not a
  combined string like `"SLT4"`.
- **`TaskSpec.priority` (int) → `TaskSpec.optional` (bool).** The solver only
  ever read `priority` as a zero/non-zero flag.
- **`Resource` is `{ "kind": "trackPart"|"facility"|"staff", "id": <int> }`.**
  Replaces three parallel `Optional[int]` fields.
- **Every ID is an `int`**, including composite ones. Four `stoi()` calls and
  their string-keyed fallbacks are gone; a latent sort bug where unit 10
  sorted before unit 2 went with them.
- **`Plan.trackParts` is dropped.** Infrastructure comes from
  `--path_location`, as it already did; this field was dead weight on the
  wire.
- **Enum wire values are PascalCase** (`"StandIn"`, not `"standIn"`).
- **`standingIndex`** is read but not yet acted on — see solver#18 below.
  Fixture data that carried a decorative `standingIndex` (present but not
  meaningful) has been nulled out to avoid implying an ordering the solver
  doesn't enforce.
- **`reversalDuration` is dropped from the wire format entirely.** It was
  declared on the interchange DTO (`Interchange/Scenario.cs`) but never read
  anywhere — the solver's real computation, `ShuntTrain.ReversalDuration`,
  already derives it locally from `BackNormTime` + `Carriages *
  BackAdditionTime` (`ProblemInstance.cs`), so nothing here changes
  behavior. See generator's `SCHEMA_CHANGELOG.md` ("Unversioned —
  2026-08-21") for the full trace.

`schemaVersion` mismatches are logged as a warning, never a hard reject.

### Known limitation: two canonical fixtures can't produce a valid plan

`6t_custom_example3` and `7t_custom_example1` are expected to fail, not from
anything in this release, but from two open issues:

- `6t_custom_example3`: the solver parks on a non-parking arrival track when
  it can't move into the yard immediately
  ([#13](https://github.com/Robust-Rail-NL/robust-rail-solver/issues/13)).
- `7t_custom_example1`: the solver's cost function has no deadline for
  outStanding trains, so it produces a plan that overruns the scenario
  horizon for free, which then trips a diagnostic-quality bug in the
  evaluator's terminal-state handling rather than a clean failure
  ([#14](https://github.com/Robust-Rail-NL/robust-rail-solver/issues/14),
  evaluator#6).

Both were deferred deliberately rather than blocking 2.0.0.

### Other known issues, not blocking

- **[#17](https://github.com/Robust-Rail-NL/robust-rail-solver/issues/17)** —
  solver and evaluator place a combined inStanding train's members at
  opposite ends of the track, so the solver can route a departing half out of
  the blocked end and call the result feasible. Latent in this corpus: no
  fixture has a combined inStanding train that gets split.
- **[#18](https://github.com/Robust-Rail-NL/robust-rail-solver/issues/18)** —
  the solver ignores `standingIndex`, so the order of several standing units
  on one track is not the one the scenario asked for. Latent in this corpus:
  every scenario leaves the field null (see above).
- **[#19](https://github.com/Robust-Rail-NL/robust-rail-solver/issues/19)** —
  open question, not a defect: splitting a train in place costs no shunt move
  today, and nothing prices the personnel it would need.

[#11](https://github.com/Robust-Rail-NL/robust-rail-solver/issues/11) (a
`System.ArgumentOutOfRangeException` from an aliased `State` on in-place
splits) and #16 (`Deque.RemoveHead` throwing after a successful removal) are
both fixed in this release.

### Repo hygiene

- CI now builds and tests on `ubuntu-24.04-arm` as well as `ubuntu-latest` —
  several developers work on arm64, and this is where the wall-clock-bounded
  local search lives.
- `docker-push.sh` publishes an `-assert` image alongside every release
  (Release optimisation plus `DEBUG`, so `Debug.Assert` survives) for soak
  testing; it is never what the pipeline runs, since an assertions build
  explores less of the neighbourhood in the same time budget and would
  produce different plans on any unconverged scenario.

### Fixed after the initial tag

`v2.0.0` was moved forward once, before any `2.0.0` images were built, to
pick up one fix:

- The no-config Docker run (`docker run hip:2.0.0` with no arguments)
  crashed with an unhandled `DirectoryNotFoundException` (exit 139). The
  fixture directory `example_kleine_binckhorst/` was never copied into the
  image — the Dockerfile's build context is `ServiceSiteScheduling/`, and
  the fixture lives one level up, outside it. Fixed via a named build
  context (`docker-push.sh` now passes `--build-context
  fixtures=../example_kleine_binckhorst`).

### Publishing

The HIP image is versioned from `HIP.csproj`'s `<Version>` element and pushed
to `ghcr.io/robust-rail-nl/hip` via `./ServiceSiteScheduling/docker-push.sh`
(multi-arch: `linux/amd64`, `linux/arm64`). `hip:2.0.0` is a real rebuild from
source at the `v2.0.0` tag, not a re-tag of an `-rc.*` image — earlier `rc`
images kept reporting their own prerelease version when run even after being
re-tagged `2.0.0`, which this avoids. `:latest` moves to `2.0.0` as the first
stable tag of the release; it does not move for `-rc.*`/`-beta.*` builds.
