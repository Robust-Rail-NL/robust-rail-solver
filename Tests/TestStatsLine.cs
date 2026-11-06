namespace Tests.StatsLine;

using System.Text.RegularExpressions;
using ServiceSiteScheduling;
using ServiceSiteScheduling.LocalSearch;
using ServiceSiteScheduling.Utilities;
using Tests.InPlaceSplit;

// SimulatedAnnealing.Run ends with one machine-readable STATS line, printed
// after the existing human-readable "Finished after" / "Neighbors visited"
// lines, which must stay as they are (scripts parse them).
//
// Same collection as the other tests that mutate ProblemInstance.Current;
// also needed here because the test redirects the process-wide Console.Out.
[Collection(PlanBuilding.Name)]
public class StatsLineTests
{
    private static string TestData(string name) =>
        Path.Combine(Directory.GetCurrentDirectory(), "TestData", name);

    [Fact]
    public void FormatStatsLine_UsesInvariantCultureAndFixedKeyOrder()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
            Assert.Equal(
                "STATS search_seconds=12.34 neighbors=56789 iterations=1234 feasible=true time_to_best_seconds=1.50",
                SimulatedAnnealing.FormatStatsLine(12340, 56789, 1234, true, 1500)
            );
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Run_PrintsStatsLineAfterTheExistingSummaryLines()
    {
        ProblemInstance.Current = ProblemInstance.ParseJson(
            TestData("location_samesite_departure.json"),
            TestData("scenario_samesite_departure.json")
        );

        var random = new Random(1);
        var tabu = new TabuSearch(random, 0);
        var annealing = new SimulatedAnnealing(random, tabu.Graph);

        var original = Console.Out;
        var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            annealing.Run(new Time(30), true, 1000, 15, 0.97, 2000, 2000, 0.2);
        }
        finally
        {
            Console.SetOut(original);
        }

        string[] lines = captured
            .ToString()
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        int finished = Array.FindIndex(lines, l => l.StartsWith("Finished after "));
        int neighbors = Array.FindIndex(lines, l => l.StartsWith("Neighbors visited: "));
        int stats = Array.FindIndex(lines, l => l.StartsWith("STATS "));

        Assert.True(finished >= 0, "existing 'Finished after' line must remain");
        Assert.Equal(finished + 1, neighbors);
        Assert.Equal(neighbors + 1, stats);
        Assert.Matches(
            new Regex(
                @"^STATS search_seconds=\d+\.\d\d neighbors=\d+ iterations=\d+ feasible=(true|false) time_to_best_seconds=\d+\.\d\d$"
            ),
            lines[stats]
        );
    }
}
