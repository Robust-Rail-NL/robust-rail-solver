#nullable enable

namespace ServiceSiteScheduling.Interchange
{
    public record UnhashableRecord
    {
        public override int GetHashCode()
        {
            throw new NotImplementedException("This type of record cannot be hashed");
        }
    }

    // A producer's own verdict on whether its submitted Plan satisfies all
    // hard constraints - not a claim about whether some other plan for the
    // same Scenario might exist. Unknown is the default for a producer that
    // doesn't compute this (or a plan predating this field), not a third
    // kind of "maybe".
    public enum Feasibility
    {
        Unknown,
        Feasible,
        Infeasible,
    }

    // This message contains the result of a shunting algorithm.
    public record Plan : UnhashableRecord
    {
        // Always emitted on write; a freshly-constructed Plan defaults to the
        // current interchange schema version without callers having to set it.
        public int SchemaVersion { get; init; } = InterchangeSchema.ExpectedVersion;

        // Required, mirroring the interchange schema: a plan is its actions, so
        // reading one that has none is a malformed file rather than an empty
        // result. Callers building a Plan set it explicitly.
        public required IList<Action> Actions { get; init; }

        public Feasibility Feasibility { get; init; } = Feasibility.Unknown;

        // Free text identifying what produced this plan, e.g.
        // "robust-rail-solver 2.0.0-edge+20260826.a1b2c3d" - for a human
        // debugging a failed evaluation, not for programmatic parsing.
        public string? Producer { get; init; }

        // This producer's own total objective/cost value for this plan, in
        // whatever units and scale it uses - not comparable across
        // different producers.
        public double? Cost { get; init; }

        // Free-form human-readable breakdown of Cost, e.g.
        // SolutionCost.ToString()'s output. Deliberately not structured: the
        // term breakdown is producer-specific, unlike the total itself.
        public string? CostDetails { get; init; }
    }

    public record Action
    {
        // The time interval of this action.
        // Times are in seconds since the epoch.
        public ulong? StartTime { get; set; }
        public ulong? EndTime { get; set; }

        // The type of this action (e.g. cleaning, moving, waiting)
        public required TaskType TaskType { get; set; }

        // The ShuntingUnit to which this Action applies
        public required ShuntingUnit ShuntingUnit { get; set; }

        // The TrackPart ID on which this Action occurs.
        // If taskType = Move, then trackPart specifies the move destination,
        // and the resources specify the path.
        public ulong? Location { get; set; }

        // Other resources besides the TrackPart involved with the Action.
        // For example for taskType = InternalCleaning there could be
        // a CleaningPlatform Facility Resource.
        public IList<Resource> Resources { get; set; } = [];

        // Compute hash code from all fields except the lists.
        public override int GetHashCode()
        {
            return HashCode.Combine(
                this.StartTime,
                this.EndTime,
                this.TaskType,
                this.ShuntingUnit,
                this.Location
            );
        }

        // Determine equality based on all fields except the lists.
        public virtual bool Equals(Action? other)
        {
            if (other == null)
                return false;
            if (other.StartTime != this.StartTime || other.EndTime != this.EndTime)
                return false;
            if (!(this.ShuntingUnit?.Equals(other.ShuntingUnit) ?? other.ShuntingUnit == null))
                return false;
            if (other.Location != this.Location)
                return false;
            return true;
        }
    }
}
