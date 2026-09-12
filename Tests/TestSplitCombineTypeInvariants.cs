// PlanGraph.Units0Side (#26) determines which physical end of a track a
// split's units land on by walking backward through a train's move history,
// and gives up as soon as it crosses an earlier split -- it was never taught
// to reconstruct position within a prior split's several simultaneously
// placed branches. It additionally assumes a combine can never precede a
// split, so it never has to consider that case at all.
//
// Both halves of that are currently unreachable through any real
// location/scenario input (see PlanGraph.SplitsNotAtArrival for the split
// half, checked at runtime on every write because search code could change).
//
// The combine half is a mix of two different kinds of guarantee. That a
// combine can only ever exist on a DepartureRoutingTask (RoutingTask.Previous
// is singular, DepartureRoutingTask.Previous is a list) is pinned purely by
// property type -- no graph walk can observe a *future* widening of either
// property before it happens, so these tests pin that shape directly here.
// That a combine's DepartureRoutingTask.Next always leads straight to a
// terminal Departure/StandOut, never a further stop, is instead a value-level
// invariant already checked against every real graph by
// PlanGraph.CheckGraphStructure (see its "must not be a Departure move"
// asserts) -- these tests only additionally pin that Next stays a single
// TrackTask, not a collection, which is the one half CheckGraphStructure
// can't defend against a future change widening it.
namespace Tests;

using System.Reflection;
using ServiceSiteScheduling.Tasks;

public class TestSplitCombineTypeInvariants
{
    private static PropertyInfo GetProperty(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.True(property != null, $"{type.Name}.{name} no longer exists");
        return property!;
    }

    [Fact]
    public void RoutingTask_Previous_IsASingleTrackTask_NeverACombine()
    {
        var property = GetProperty(typeof(RoutingTask), nameof(RoutingTask.Previous));

        Assert.True(
            property.PropertyType == typeof(TrackTask),
            $"RoutingTask.Previous is now {property.PropertyType}, not a single TrackTask. "
                + "PlanGraph.Units0Side (#26) assumes a plain RoutingTask can never represent "
                + "a combine (multiple predecessors) -- only DepartureRoutingTask can. If "
                + "you're widening this to support multiple predecessors, read #26 first."
        );
    }

    [Fact]
    public void DepartureRoutingTask_Previous_IsAList_OnlyPlaceACombineCanExist()
    {
        var property = GetProperty(
            typeof(DepartureRoutingTask),
            nameof(DepartureRoutingTask.Previous)
        );

        Assert.True(
            property.PropertyType == typeof(List<TrackTask>),
            $"DepartureRoutingTask.Previous is now {property.PropertyType}, not List<TrackTask>. "
                + "This is currently the only place a combine can exist; PlanGraph.Units0Side "
                + "(#26) depends on that staying true."
        );
    }

    // Pins only that Next can't become a collection (which would let a
    // combine branch into more than one further stop). Whether the single
    // TrackTask it does point to is actually a terminal Departure/StandOut
    // is a separate, value-level invariant already checked by
    // PlanGraph.CheckGraphStructure against every real graph -- not
    // something this reflection test re-verifies.
    [Fact]
    public void DepartureRoutingTask_Next_StaysASingleTrackTask_NotACollection()
    {
        var property = GetProperty(typeof(DepartureRoutingTask), nameof(DepartureRoutingTask.Next));

        Assert.True(
            property.PropertyType == typeof(TrackTask),
            $"DepartureRoutingTask.Next is now {property.PropertyType}, not a single TrackTask. "
                + "PlanGraph.Units0Side (#26) assumes a combine can never branch into more than "
                + "one further stop. If you're widening this to a collection, read #26 first -- "
                + "and note PlanGraph.CheckGraphStructure separately checks that Next actually "
                + "leads to a terminal Departure/StandOut, which this test does not."
        );
    }
}
