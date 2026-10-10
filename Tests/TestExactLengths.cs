namespace Tests.ExactLengths;

using ServiceSiteScheduling;
using ServiceSiteScheduling.Initial;
using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Utilities;
using Tests.InPlaceSplit;

// Track and unit lengths are kept as exact whole millimetres rather than truncated
// to metres, so a fractional-metre layout is judged the way the evaluator judges it.
[Collection(PlanBuilding.Name)]
public class ExactLengthsTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    private static string Rewritten(string name, string from, string to)
    {
        string text = File.ReadAllText(TestData(name));
        Assert.Contains(from, text);
        string path = Path.Combine(Path.GetTempPath(), $"len_{Guid.NewGuid():N}.json");
        File.WriteAllText(path, text.Replace(from, to));
        return path;
    }

    [Theory]
    [InlineData(247.2, 247200)]
    [InlineData(431.02, 431020)]
    [InlineData(24.9, 24900)]
    [InlineData(0.0005, 1)] // half a millimetre rounds away from zero
    [InlineData(0.0004, 0)]
    public void FromMetres_RoundsToTheNearestMillimetre(double metres, int expected) =>
        Assert.Equal(expected, Lengths.FromMetres(metres, "test"));

    [Fact]
    public void ToMetres_InvertsFromMetres() =>
        Assert.Equal(247.2, Lengths.ToMetres(Lengths.FromMetres(247.2, "test")));

    // The through track with the given length, and two units of 50.1 m (100.2 m together).
    // Truncated to whole metres the units would be 100 m, and 100.1 m of track 100 m.
    private static SolutionCost CostWithTrack(string trackLength)
    {
        string scenario = Rewritten("scenario_through_track_fifo.json", "50.0", "50.1");
        string location = Rewritten(
            "location_through_track_short.json",
            "\"length\": 80.0",
            $"\"length\": {trackLength}"
        );
        ProblemInstance.Current = ProblemInstance.ParseJson(location, scenario);
        return SimpleHeuristic.Construct(new Random(1)).ComputeModel();
    }

    [Fact]
    public void ExactlyFullFractionalTrack_IsNotAnOverrun()
    {
        var cost = CostWithTrack("100.2");
        Assert.Equal(0, cost.TrackLengthViolations);
        Assert.Equal(0, cost.TrackLengthViolationSum);
    }

    [Fact]
    public void FractionalOverrun_IsCountedExactlyInMetres()
    {
        var cost = CostWithTrack("100.1");
        Assert.Equal(1, cost.TrackLengthViolations);
        Assert.Equal(0.1, cost.TrackLengthViolationSum, 9);
    }

    [Theory]
    [InlineData(247.2, true)]
    [InlineData(431.02, true)]
    [InlineData(0.1 + 0.2, true)] // multiplication error only
    [InlineData(247.2004, false)]
    [InlineData(10.0005, false)]
    public void IsWholeMillimetres_FlagsMoreThanThreeDecimals(double metres, bool expected) =>
        Assert.Equal(expected, Lengths.IsWholeMillimetres(metres));
}
