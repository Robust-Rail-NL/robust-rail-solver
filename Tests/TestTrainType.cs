// TrainType's duration fields are config-loaded Time values that later cross
// into PlanGraph's ulong wire format unchanged. A negative one there would
// wrap instead of failing, so the constructor rejects it up front (#60).
namespace Tests.TrainType;

using ServiceSiteScheduling.Trains;

public class TrainTypeDurationValidationTests
{
    [Fact]
    public void NonNegativeDurations_AreAccepted()
    {
        var type = new TrainType(0, "Type", 1, [], 0, 0, 0, 0);

        Assert.Equal(0, type.BaseReversalDuration.Seconds);
    }

    [Fact]
    public void ANegativeBaseReversalDuration_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrainType(0, "Type", 1, [], -1, 0, 0, 0)
        );
    }

    [Fact]
    public void ANegativeVariableReversalDuration_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrainType(0, "Type", 1, [], 0, -1, 0, 0)
        );
    }

    [Fact]
    public void ANegativeCombineDuration_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrainType(0, "Type", 1, [], 0, 0, -1, 0)
        );
    }

    [Fact]
    public void ANegativeSplitDuration_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TrainType(0, "Type", 1, [], 0, 0, 0, -1)
        );
    }
}
