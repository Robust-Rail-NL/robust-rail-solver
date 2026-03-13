#nullable enable

namespace ServiceSiteScheduling.NoProto
{
    // Represents a single time interval.
    public class TimeInterval
    {
        public double? Start { get; set; }
        public double? End { get; set; }
    }

    public enum SolverBackend
    {
        MIPCL = 0,
        CPLEX = 1,
        LPSOLVE = 2,
        CBC = 3,
    }
}
