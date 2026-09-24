namespace Tests;

using ServiceSiteScheduling;

// Regression test for issue #53: a bare-filename PlanPath crashed startup,
// because Path.GetDirectoryName returns "" for it and
// Directory.CreateDirectory("") throws.
public class TestPlanPathDirectory
{
    [Fact]
    public void BareFilenameDoesNotThrow()
    {
        Program.EnsureParentDirectoryExists("plan.json");
    }
}
