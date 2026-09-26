# Evaluator repro for #23, symptom (2): bunched departure timing

Reproduces the "several departures share one identical action time, each diverging
from its own scheduled time" symptom described in #23, against a current (2.2.0)
build of `robust-rail-evaluator`'s `TORS` binary.

## Files

- `location.json`, `config.json` - evaluator-format location and business-rules
  config, ported from the `known_problems` branch's `a34dd3c` (pre-2.0.0 interchange
  schema port). Paired with `../scenario_solver.json` (unchanged from that same port,
  already committed at the parent directory level - identical content, not
  duplicated here) as the scenario input.
- `plan_2025_original.json` - the historical 2025 solved plan, fetched verbatim from
  commit `475af158052ba295e07e3996578cdb532245d60f` at
  `ServiceSiteScheduling/database/TUSS-Instance-Generator/scenario_settings/setting_known_problems/setting_multiple_instanding/plan.json`.
  Uses the legacy embedded-`members` shuntingUnit shape, string-typed fields - not
  directly loadable by a current evaluator build (rejected with "ShuntingUnit '0' has
  no memberIDs in the plan JSON").
- `port_plan.py` - the conversion script used to port `plan_2025_original.json`
  forward: `members` (embedded TrainUnit objects) -> `memberIDs` (int ID list) plus
  `parentIDs`/`childIDs: []`, string times/ids -> ints, `resources[].trackPartId`/
  `facilityId` -> `{"kind": "trackPart"|"facility", "id": <int>}`.
- `plan_2025_ported.json` - the result: `python3 port_plan.py plan_2025_original.json
  plan_2025_ported.json`.
- `eval_result_2025_ported.txt` - the evaluator's own `--path_eval_result` output from
  the run below, showing the bunched-timing rejection.

## Reproducing

Requires a local build of `robust-rail-evaluator` (sibling repo,
`ghcr.io/robust-rail-nl/tors` works too, but this was run against a locally built
2.2.0 `TORS`, not `rc.3` - the earlier `rc.3`-based finding in #23 doesn't hold: an
independent finding in the same thread was a red herring from testing a stale 2.1.0
build against the unported plan, and turned out to be unrelated to this symptom).

```
TORS=/path/to/robust-rail-evaluator/build/TORS
cd known_problems/multiple_instanding/evaluator
timeout 30 "$TORS" --mode EVAL_AND_STORE \
  --path_location "$(pwd)" \
  --path_scenario "$(pwd)/../scenario_solver.json" \
  --path_plan "$(pwd)/plan_2025_ported.json" \
  --path_eval_result "$(pwd)/eval_result.txt" \
  --plan_type Solver
```

Always wrap in `timeout` - see `robust-rail-evaluator/doc/known-issue-plan-evaluation-hang.md`
for why (a different, unrelated hang class this scenario does *not* hit, but the
warning about capping output still applies as a matter of habit).

Expected output: 3 of the 6 departures (trains `3001`, `6001`, `7001`) all report
`Action start/end time: 5300`, each diverging from its own distinct scheduled
departure time (3900/5000/5000 respectively) - "The plan is not valid".
