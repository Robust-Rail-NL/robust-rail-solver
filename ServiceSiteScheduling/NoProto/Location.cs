#nullable enable

using System.Collections.Immutable;
using ServiceSiteScheduling.TrackParts;

namespace ServiceSiteScheduling.NoProto
{
    public record Location
    {
        public ImmutableArray<TrackPart> TrackParts { get; init; } = [];
        public ImmutableArray<Facility> Facilities { get; init; } = [];
        public ImmutableArray<TaskType> TaskTypes { get; init; } = [];
    }

    public record Resource(string? Name, ulong? FacilityId, ulong? TrackPartId)
    {
        internal static Resource FromFacility(Facility facility)
        {
            return new Resource(facility.Id.ToString(), facility.Id, null);
        }

        internal static Resource FromInfra(Infrastructure infra)
        {
            return new Resource(infra.ID.ToString(), null, infra.ID);
        }
    }

    public record Facility(
        ulong? Id,
        string? Type,
        ImmutableArray<ulong> RelatedTrackParts,
        ImmutableArray<TaskType> TaskTypes,
        int? SimultaneousUsageCount
    );

    public enum TrackPartType
    {
        RailRoad,

        // Switches
        Switch,
        EnglishSwitch,
        HalfEnglishSwitch,

        // Other
        Intersection,
        Bumper,
    }

    public record TrackPart
    {
        public ulong Id { get; init; }
        public TrackPartType? Type { get; init; }
        public ImmutableArray<ulong> ASide { get; init; } = [];
        public ImmutableArray<ulong> BSide { get; init; } = [];
        public double? Length { get; init; }
        public string? Name { get; init; }
        public bool SawMovementAllowed { get; init; }
        public bool ParkingAllowed { get; init; }
    }

    public record TaskType
    {
        public PredefinedTaskType? Predefined { get; init; }
        public string? Other { get; init; }

        public TaskType(PredefinedTaskType? predefined, string? other)
        {
            if (predefined == null && other == null || predefined != null && other != null)
            {
                throw new ArgumentException("Exactly one constructor argument must be null");
            }
            this.Predefined = predefined;
            this.Other = other;
        }

        private static readonly Dictionary<PredefinedTaskType, TaskType> TaskTypeMap = [];

        public static TaskType FromPredefined(PredefinedTaskType predefined)
        {
            if (TaskTypeMap.TryGetValue(predefined, out var taskType))
            {
                return taskType;
            }
            else
            {
                taskType = new TaskType(predefined, null);
                TaskTypeMap[predefined] = taskType;
                return taskType;
            }
        }
    }

    public enum PredefinedTaskType
    {
        // Movement
        Move,
        Split,
        Combine,

        // Special
        Wait,
        Arrive,
        Exit,

        // StandOut = 6,
        // StandIn = 7,
    }
}
