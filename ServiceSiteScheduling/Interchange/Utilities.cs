#nullable enable

namespace ServiceSiteScheduling.Interchange
{
    // Represents a single time interval. Unused: declared but never
    // constructed or referenced anywhere.
    public readonly record struct UnusedTimeInterval(double Start, double End);

    public enum SolverBackend
    {
        MIPCL = 0,
        CPLEX = 1,
        LPSOLVE = 2,
        CBC = 3,
    }
}
