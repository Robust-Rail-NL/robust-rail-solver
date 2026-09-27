namespace Tests.UnroutableDeparture;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Interchange;
using ServiceSiteScheduling.LocalSearch;
using ServiceSiteScheduling.Routing;
using Tests.InPlaceSplit;

// Regression fixture for #65: a DepartureRoutingTask whose only route is
// Route.Invalid (Routing/Graph.cs's Dijkstra found no path at all for this
// unit to depart - a genuine "no route exists", not merely one local search
// hasn't found yet) used to crash PlanGraph.ToPlan() with an
// ArgumentNullException, since Route.Invalid.Train is null and ToPlan() used
// to pass it straight to GetShuntUnit unconditionally.
//
// Route.Invalid anywhere in a DepartureRoutingTask's route set suppresses
// the whole departure's action set, not just that one route - see
// PlanGraph.cs's own comment for why. This fixture only has one group, so
// the multi-group case can't be exercised directly here; what this test
// verifies is that the suppression is whole-departure: no Move/Reverse
// action *and* no terminal Exit action.
//
// Reuses the same-track-departure fixture (a single unit, one real route)
// and forces Route.Invalid onto its DepartureRoutingTask after construction,
// rather than crafting a topologically-broken fixture: this exercises
// exactly the code path that used to crash, deterministically, without
// depending on the local search actually failing to find a route (which
// isn't reliably reproducible - see #23's own investigation, where the same
// underlying condition only sometimes surfaces depending on scenario/seed).
[Collection(PlanBuilding.Name)]
public class UnroutableDepartureTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void DepartureRoutingTaskWithNoRoute_DoesNotCrashToPlan()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_samesite_departure.json"),
            TestData("scenario_samesite_departure.json")
        );

        var tabuSearch = new TabuSearch(new Random(1), 0);
        var departureTask = tabuSearch.Graph.DepartureTasks.Single();
        var departureRouting = departureTask.GetDepartureRoutingTask();

        // Sanity: a real route exists before we clobber it - otherwise this
        // test wouldn't actually be exercising the "no route at all" case.
        Assert.NotEmpty(departureRouting.GetRoutes());

        departureRouting.ClearRoutes();
        departureRouting.AddRoute(Route.Invalid);

        // Not recomputing the model here (ComputeModel would just re-route
        // this unit for real, since a route does exist in this fixture -
        // undoing the injection). So graph.Cost is left stale from before
        // the injection, and this test doesn't assert on plan.Feasibility:
        // that flip is a separate, simpler claim (SolutionCost.IsFeasible
        // summing Route.Invalid's penalty Crossings into a real
        // ComputeModel() pass), not what this regression is about.
        Plan? plan = null;
        var exception = Record.Exception(() => plan = tabuSearch.Graph.ToPlan());
        Assert.Null(exception);
        Assert.NotNull(plan);

        // The crash is gone *and* it's reported, not silently dropped.
        var unroutable = Assert.Single(tabuSearch.Graph.UnroutableDepartures);
        Assert.Contains(departureRouting.Train.ToString(), unroutable);

        // Whole-departure suppression, not just "the crashing route is
        // skipped": this fixture normally emits a Reverse action for this
        // exact departure (see TestSameTrackDeparture.cs's
        // DepartingFromTheArrivalTrack_EmitsARealReverseAction) and a
        // terminal Exit action for reaching the scenario-fixed departure
        // track. Neither should appear now - a plan with the Reverse but not
        // the Exit (or vice versa) would misrepresent a departure that never
        // actually completed as having partly happened.
        Assert.DoesNotContain(
            plan!.Actions,
            a => a.TaskType.Predefined == PredefinedTaskType.Reverse
        );
        Assert.DoesNotContain(plan.Actions, a => a.TaskType.Predefined == PredefinedTaskType.Exit);
    }
}
