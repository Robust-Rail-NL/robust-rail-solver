#nullable enable

using System.Collections.Immutable;

namespace ServiceSiteScheduling.NoProto
{
    // This message contains the result of a shunting algorithm.
    public record Plan
    {
        public ImmutableArray<Action> Actions { get; set; } = [];

        // A list of all TrackParts. From this a rail graph can be constructed.
        // This field should be temporary and be replaced as soon as we send input to the algorithm.
        public IList<TrackPart>? TrackParts { get; set; }
    }

    public record Action
    {
        // The time interval of this action.
        // Times are in seconds since the epoch.
        public ulong? StartTime { get; set; }
        public ulong? EndTime { get; set; }

        // The type of this action (e.g. cleaning, moving, waiting)
        public TaskType? TaskType { get; set; }

        // The ShuntingUnit to which this Action applies
        public ShuntingUnit? ShuntingUnit { get; set; }

        // The TrackPart ID on which this Action occurs.
        // If taskType = Move, then trackPart specifies the move destination,
        // and the resources specify the path.
        public ulong? Location { get; set; }

        // Other resources besides the TrackPart involved with the Action.
        // For example for taskType = InternalCleaning there could be
        // a CleaningPlatform Facility Resource.
        public IList<Resource> Resources { get; set; } = [];

        // Train units involved in this Action
        // If not specified, all train units are involved.
        public IList<string>? TrainUnitIds { get; set; }

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
