#nullable enable

using System.Collections.Immutable;
using ServiceSiteScheduling.TrackParts;

namespace ServiceSiteScheduling.NoProto
{
    public record Location(
        ImmutableArray<TrackPart> TrackParts,
        ImmutableArray<Facility> Facilities,
        ImmutableArray<TaskType> TaskTypes
    );

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

    public record TrackPart(
        ulong Id,
        TrackPartType? Type,
        ImmutableArray<ulong> ASide,
        ImmutableArray<ulong> BSide,
        double? Length,
        string? Name,
        bool SawMovementAllowed,
        bool ParkingAllowed
    );

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
