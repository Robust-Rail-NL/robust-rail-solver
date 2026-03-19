#nullable enable

namespace ServiceSiteScheduling.NoProto
{
    // Represents a single time interval.
    public readonly record struct TimeInterval(double Start, double End);

    public enum SolverBackend
    {
        MIPCL = 0,
        CPLEX = 1,
        LPSOLVE = 2,
        CBC = 3,
    }
}
