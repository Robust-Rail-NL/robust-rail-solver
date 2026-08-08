#nullable enable

namespace ServiceSiteScheduling.NoProto
{
    public record UnhashableRecord
    {
        public override int GetHashCode()
        {
            throw new NotImplementedException("This type of record cannot be hashed");
        }
    }

    // This message contains the result of a shunting algorithm.
    public record Plan : UnhashableRecord
    {
        // Always emitted on write; a freshly-constructed Plan defaults to the
        // current interchange schema version without callers having to set it.
        public int SchemaVersion { get; init; } = InterchangeSchema.ExpectedVersion;

        public IList<Action> Actions { get; init; } = [];
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
