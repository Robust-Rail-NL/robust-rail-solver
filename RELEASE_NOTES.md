# Release notes

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

### One more schema-adjacent change after the initial cut

`reversalDuration` is dropped from the wire format entirely, landed after
this file was first written. It was declared on the interchange DTO
(`Interchange/Scenario.cs`) but never read anywhere — the solver's real
computation, `ShuntTrain.ReversalDuration`, already derives it locally from
`BackNormTime` + `Carriages * BackAdditionTime` (`ProblemInstance.cs`), so
nothing here changes behavior. See generator's `SCHEMA_CHANGELOG.md`
("Unversioned — 2026-08-21") for the full trace.

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

### Publishing

The HIP image is versioned from `HIP.csproj`'s `<Version>` element and pushed
to `ghcr.io/robust-rail-nl/hip` via `./ServiceSiteScheduling/docker-push.sh`
(multi-arch: `linux/amd64`, `linux/arm64`). The `hip:2.0.0` tag points at the
same image digest already verified as `2.0.0-rc.2` — re-tagged, not rebuilt,
so the tag names exactly the bytes that were tested. `:latest` moves to
`2.0.0` as the first stable tag of the release; it does not move for
`-rc.*`/`-beta.*` builds.
