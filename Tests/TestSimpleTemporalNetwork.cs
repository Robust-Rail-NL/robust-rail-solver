// Covers SimpleTemporalNetwork in isolation, with no PlanGraph/task-type
// knowledge involved. The no-edge-means-unconstrained case is the crux of the
// #43 fix: two time-points with no constraint between them must not affect
// each other, unlike the old shared-clock walk that floored every move by
// whatever happened to precede it in one global list.
namespace Tests.SimpleTemporalNetwork;

using ServiceSiteScheduling.Solutions;

public class DispatchTests
{
    [Fact]
    public void AVertexWithNoIncomingEdges_DispatchesToZero()
    {
        var network = new SimpleTemporalNetwork();
        var point = network.CreateTimePoint("lonely");

        network.Dispatch();

        Assert.Equal(0, (int)point.Value);
    }

    [Fact]
    public void RequireAbsolute_SetsAFixedLowerBound()
    {
        var network = new SimpleTemporalNetwork();
        var point = network.CreateTimePoint();
        network.RequireAbsolute(point, 100);

        network.Dispatch();

        Assert.Equal(100, (int)point.Value);
    }

    [Fact]
    public void Require_PropagatesFromValuePlusMinDistance()
    {
        var network = new SimpleTemporalNetwork();
        var a = network.CreateTimePoint("a");
        var b = network.CreateTimePoint("b");
        network.RequireAbsolute(a, 10);
        network.Require(a, b, 5);

        network.Dispatch();

        Assert.Equal(10, (int)a.Value);
        Assert.Equal(15, (int)b.Value);
    }

    [Fact]
    public void ADispatchedValue_IsTheMaxOverAllIncomingEdges()
    {
        var network = new SimpleTemporalNetwork();
        var a = network.CreateTimePoint("a");
        var b = network.CreateTimePoint("b");
        var c = network.CreateTimePoint("c");
        network.RequireAbsolute(a, 10);
        network.RequireAbsolute(b, 100);
        network.Require(a, c, 5); // c >= 15
        network.Require(b, c, 1); // c >= 101

        network.Dispatch();

        Assert.Equal(101, (int)c.Value);
    }

    [Fact]
    public void TwoTimePointsWithNoEdgeBetweenThem_DoNotConstrainEachOther()
    {
        // The bug #43 fixes: today's ComputeTime floors every move by whatever
        // happened to precede it in one global list, even with no real
        // relationship. Here, b has no edge to/from a at all, so a's large
        // value must not leak into b.
        var network = new SimpleTemporalNetwork();
        var a = network.CreateTimePoint("a");
        var b = network.CreateTimePoint("b");
        network.RequireAbsolute(a, 100_000);
        network.RequireAbsolute(b, 1);

        network.Dispatch();

        Assert.Equal(100_000, (int)a.Value);
        Assert.Equal(1, (int)b.Value);
    }

    [Fact]
    public void AnExactZeroWeightCycle_ConvergesInsteadOfThrowing()
    {
        var network = new SimpleTemporalNetwork();
        var a = network.CreateTimePoint("a");
        var b = network.CreateTimePoint("b");
        network.RequireAbsolute(a, 42);
        network.Require(a, b, 0);
        network.Require(b, a, 0); // a <-> b, weight 0 both ways: a valid "simultaneous" pair

        network.Dispatch();

        Assert.Equal(42, (int)a.Value);
        Assert.Equal(42, (int)b.Value);
    }

    [Fact]
    public void APositiveWeightCycle_ThrowsInsteadOfSilentlyMiscomputing()
    {
        var network = new SimpleTemporalNetwork();
        var a = network.CreateTimePoint("a");
        var b = network.CreateTimePoint("b");
        network.Require(a, b, 5);
        network.Require(b, a, 1); // a >= b + 1 >= (a + 5) + 1: impossible

        Assert.Throws<InvalidOperationException>(() => network.Dispatch());
    }

    [Fact]
    public void ANegativeMinDistance_ThrowsInsteadOfBeingSilentlyAccepted()
    {
        var network = new SimpleTemporalNetwork();
        var a = network.CreateTimePoint("a");
        var b = network.CreateTimePoint("b");

        Assert.Throws<ArgumentOutOfRangeException>(() => network.Require(a, b, -1));
    }

    [Fact]
    public void ACyclicVertex_DoesNotKeepAStaleValueFromBeforeDispatch()
    {
        // Kahn's phase never touches a cyclic vertex (it's never dequeued), so without
        // an explicit reset, ResolveCycle would relax from whatever Value it already
        // held going in -- e.g. left over from reusing the same TimePoints for an
        // earlier Dispatch(). Since relaxation only ever takes a max, a stale too-high
        // value could never correct itself back down.
        var network = new SimpleTemporalNetwork();
        var a = network.CreateTimePoint("a");
        var b = network.CreateTimePoint("b");
        network.RequireAbsolute(a, 1);
        network.Require(a, b, 0);
        network.Require(b, a, 0);

        a.Value = 1_000; // simulate a stale leftover value
        b.Value = 1_000;

        network.Dispatch();

        Assert.Equal(1, (int)a.Value);
        Assert.Equal(1, (int)b.Value);
    }
}
