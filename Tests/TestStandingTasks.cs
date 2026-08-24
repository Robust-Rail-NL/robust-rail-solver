// Covers the StandIn/StandOut task types introduced for inStanding and
// outStanding trains, and the two places where getting them wrong is silent
// rather than loud: the parking-like predicate and the tie-break order used
// when serialising a plan.
namespace Tests.StandingTasks;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Interchange;
using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Tasks;

public class TrackTaskKindTests
{
    // TrackTask's constructor only assigns fields, so a task can be built without
    // a train or a track. Both are irrelevant to what these tests ask.
    private static ParkingTask Parking() => new(null!, null!);

    private static StandInTask StandIn() => new(null!, null!);

    private static StandOutTask StandOut() => new(null!, null!);

    [Fact]
    public void StandingTasks_AreNotOrdinaryParking()
    {
        // The Parking*Move operators test for TrackTaskType.Parking so that they
        // leave these alone: an inStanding train starts, and an outStanding train
        // ends, on the track and at the time its scenario dictates, so relocating
        // one produces a plan that contradicts its own scenario.
        Assert.Equal(TrackTaskType.Parking, Parking().TaskType);
        Assert.Equal(TrackTaskType.StandIn, StandIn().TaskType);
        Assert.Equal(TrackTaskType.StandOut, StandOut().TaskType);
    }

    [Fact]
    public void StandingTasks_AreParkingLike()
    {
        // Code asking "is the train standing still here?" must still say yes: the
        // train is stationary on a track in all three cases.
        Assert.True(Parking().IsParkingLike);
        Assert.True(StandIn().IsParkingLike);
        Assert.True(StandOut().IsParkingLike);
    }

    [Fact]
    public void MovingAndServicingTasks_AreNotParkingLike()
    {
        Assert.False(new ArrivalTask(null!, null!, Side.A, 0).IsParkingLike);
    }

    [Fact]
    public void StandingTasks_AreStillParkingTasks()
    {
        // They derive from ParkingTask so that the parking and track-occupation
        // machinery keeps treating them as it always did.
        Assert.IsAssignableFrom<ParkingTask>(StandIn());
        Assert.IsAssignableFrom<ParkingTask>(StandOut());
    }
}

public class TaskTypeOrderTests
{
    // StandIn and StandOut are emitted with zero duration at the scenario start and
    // end, so their timestamps tie with the Wait beside them and this comparator is
    // the only thing deciding which comes first.

    [Fact]
    public void StandIn_SortsWithArrive_BeforeWhatFollowsIt()
    {
        Assert.Equal(
            PlanGraph.TaskTypeOrder(PredefinedTaskType.Arrive),
            PlanGraph.TaskTypeOrder(PredefinedTaskType.StandIn)
        );
        Assert.True(
            PlanGraph.TaskTypeOrder(PredefinedTaskType.StandIn)
                < PlanGraph.TaskTypeOrder(PredefinedTaskType.Wait)
        );
        Assert.True(
            PlanGraph.TaskTypeOrder(PredefinedTaskType.StandIn)
                < PlanGraph.TaskTypeOrder(PredefinedTaskType.Move)
        );
    }

    [Fact]
    public void StandOut_SortsWithExit_AfterWhatPrecedesIt()
    {
        Assert.Equal(
            PlanGraph.TaskTypeOrder(PredefinedTaskType.Exit),
            PlanGraph.TaskTypeOrder(PredefinedTaskType.StandOut)
        );
        Assert.True(
            PlanGraph.TaskTypeOrder(PredefinedTaskType.StandOut)
                > PlanGraph.TaskTypeOrder(PredefinedTaskType.Wait)
        );
        Assert.True(
            PlanGraph.TaskTypeOrder(PredefinedTaskType.StandOut)
                > PlanGraph.TaskTypeOrder(PredefinedTaskType.Move)
        );
    }

    [Fact]
    public void ArriveSortsFirstAndExitLast_AmongTheOrdinaryTypes()
    {
        PredefinedTaskType[] inOrder =
        [
            PredefinedTaskType.Arrive,
            PredefinedTaskType.Move,
            PredefinedTaskType.Wait,
            PredefinedTaskType.Split,
            PredefinedTaskType.Combine,
            PredefinedTaskType.Exit,
        ];

        for (int i = 1; i < inOrder.Length; i++)
        {
            Assert.True(
                PlanGraph.TaskTypeOrder(inOrder[i - 1]) < PlanGraph.TaskTypeOrder(inOrder[i]),
                $"{inOrder[i - 1]} should sort before {inOrder[i]}"
            );
        }
    }

    [Fact]
    public void ACustomTaskType_SortsLast_RatherThanCollidingWithMove()
    {
        // A custom (Other) type has no Predefined value. Using default as the
        // fallback would give it Move's order, since default(PredefinedTaskType)
        // is Move, and silently sort Move after Exit.
        Assert.True(
            PlanGraph.TaskTypeOrder(null) > PlanGraph.TaskTypeOrder(PredefinedTaskType.Exit)
        );
        Assert.NotEqual(
            PlanGraph.TaskTypeOrder(PredefinedTaskType.Move),
            PlanGraph.TaskTypeOrder(null)
        );
    }
}
