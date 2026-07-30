#nullable enable

using System.Collections;

namespace ServiceSiteScheduling.NoProto
{
    // A Scenario contains the part of the problem specification which varies daily,
    // that is the trains which come in and go out of the shunting area.
    public record Scenario
    {
        public int? SchemaVersion { get; init; }

        public required IList<TrainUnitType> TrainUnitTypes { get; init; }

        public required IList<IncomingTrain> In { get; init; }
        public required IList<TrainRequest> Out { get; init; }

        public IList<IncomingTrain>? InStanding { get; init; }
        public IList<TrainRequest>? OutStanding { get; init; }

        public required ulong StartTime { get; init; }
        public required ulong EndTime { get; set; }
    }

    // Defines all trains arriving at the shunting area.
    public record ScenarioIn
    {
        public IList<IncomingTrain> Trains { get; init; } = [];
    }

    // Defines all requests for trains leaving the shunting area.
    public record ScenarioOut
    {
        public IList<TrainRequest> TrainRequests { get; init; } = [];
    }

    // Defines all trains that were alrady at the shunting area (before the scenario starts).
    public record ScenarioInStanding
    {
        public IList<IncomingTrain> Trains { get; init; } = [];
    }

    // Defines all trains that will stay at the shunting area (after the scenario ends).
    public record ScenarioOutStanding
    {
        public IList<TrainRequest> TrainRequests { get; init; } = [];
    }

    // An incoming train
    public record IncomingTrain
    {
        // The TrackPart ID of the location this train arrives over.
        public required ulong EntryTrackPart { get; set; }

        // The TrackPart ID of the location this train is at after arriving.
        public required ulong FirstParkingTrackPart { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch.
        public ulong? Arrival { get; set; }
        public ulong? Departure { get; set; }

        public string? Id { get; set; }

        public IList<IncomingTrainUnit> Members { get; init; } = [];

        // The index of the train unit when in- or outstanding, with lower indices
        // at the A-side of the track
        public double? StandingIndex { get; set; }
    }

    // A request for a train to leave the shunting area
    public class TrainRequest
    {
        // The TrackPart ID of the location this train leaves over.
        public required ulong LeaveTrackPart { get; set; }

        // The TrackPart ID of the location this train is at before leaving.
        public required ulong LastParkingTrackPart { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch.
        public ulong? Arrival { get; set; }
        public ulong? Departure { get; set; }

        // Name of this train
        public string? DisplayName { get; set; }

        // Outgoing train units; if in a TrainUnit the id field is not specified, then any train unit will do, provided that the other fields (train type, number of carriages) are still adhered to.
        public IList<TrainUnit>? TrainUnits { get; set; }

        // The index of the train unit when in- or outstanding, with lower indices
        // at the A-side of the track
        public double? StandingIndex { get; set; }
    }

    public class EvaluatorScenario
    {
        public IList<Train>? In { get; set; }
        public IList<Train>? InStanding { get; set; }
        public IList<Train>? Out { get; set; }
        public IList<Train>? OutStanding { get; set; }

        public IList<NonServiceTraffic>? NonServiceTraffic { get; set; }
        public IList<DisabledTrackPart>? DisabledTrackPart { get; set; }
        public IList<MemberOfStaff>? Workers { get; set; }

        public required ulong StartTime { get; set; }
        public required ulong EndTime { get; set; }

        public IList<TrainUnitType>? TrainUnitTypes { get; set; }
    }

    // An incoming/leaving train or a train which stays on the location
    // If at the beginning or the end multiple trains are on the same track,
    // The order of the list specifies the order on the Track from A to B
    public class Train
    {
        // The TrackPart ID of the location this train arrives over.
        public ulong? SideTrackPart { get; set; }

        // The TrackPart ID of the location this train is at after arriving.
        public ulong? ParkingTrackPart { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch.
        // If time is 0, train is already on the location or stays on the location
        public ulong? Time { get; set; }

        // The unique identifier of the Train
        public string? Id { get; set; }

        // The train units in the train
        public IList<TrainUnit>? Members { get; set; }

        // For outstanding trains: set to true to allow departures from any track, instead of just the parkingTrackPart
        public bool? CanDepartFromAnyTrack { get; set; }

        // The index of the train unit when in- or outstanding, with lower indices
        // at the A-side of the track
        public double? StandingIndex { get; set; }

        public string? MinimumDuration { get; set; }
    }

    public class NonServiceTraffic
    {
        // The reserved part of the location send in trackparts.
        public List<ulong>? Members { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch
        public ulong? Arrival { get; set; }
        public ulong? Departure { get; set; }
        public string? Id { get; set; }
    }

    public class DisabledTrackPart
    {
        public ulong? TrackPart { get; set; }

        // Arrival on the track, and departure from the track
        // Times are in seconds since the epoch
        public ulong? Arrival { get; set; }
        public ulong? Departure { get; set; }
    }

    // TrainUnit represents a combination of carriages which can move independently.
    public record TrainUnit
    {
        // A unique identifier of the unit
        public string? Id { get; set; }

        public string? TypeDisplayName { get; set; }

        public IList<TaskSpec> Tasks { get; init; } = [];
    }

    public record IncomingTrainUnit
    {
        public required string Id { get; set; }

        public required string TypeDisplayName { get; set; }

        // Tasks for this train unit
        public IList<TaskSpec> Tasks { get; init; } = [];
    }

    // TrainUnitType is a type of train unit
    public class TrainUnitType
    {
        public override bool Equals(object? obj)
        {
            return obj is TrainUnitType other
                && other.DisplayName == this.DisplayName
                && other.Carriages == this.Carriages;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.DisplayName, this.Carriages);
        }

        // Name of the train unit type
        // For example, "SGM" or "SLT".
        public required string DisplayName { get; set; }

        // Number of carriages. This is the total number of carriages,
        // including the first and last carriage.
        public required uint Carriages { get; set; }

        // Length of this train unit, in meters
        public double? Length { get; set; }

        // The time it takes to reverse ("kopmaken"), in seconds
        public ulong? ReversalDuration { get; set; }

        // Time it takes to perform a combine in seconds
        public ulong? CombineDuration { get; set; }

        // Time it takes to perform a split in seconds
        public ulong? SplitDuration { get; set; }

        // kopmaaktijd = backNormTime + #carriage * backAdditionTime
        public ulong? BackNormTime { get; set; }
        public ulong? BackAdditionTime { get; set; }

        // this is the speed of the train but that is yet to be determinded wether that is here or location specific #warning
        public ulong? TravelSpeed { get; set; }

        // Startup + Shutdown
        public ulong? StartUpTime { get; set; }

        // for example: "SLT" or "VIRM"
        public string? TypePrefix { get; set; }

        // This TrainUnitType needs a locomotive, e.g. it cannot drive itself
        public bool NeedsLoco { get; set; }

        // Can pull/push other wagons
        public bool IsLoco { get; set; }

        // This train needs electricity, so it can only drive on electrified trackparts
        public bool NeedsElectricity { get; set; }

        // Prefix of train IDs of this type (i.e., the last two digits are removed)
        // For example, for SLT4 this is 24
        public int? IdPrefix { get; set; }
    }

    // A ShuntingUnit is a combination of TrainUnits,
    // and moves as a unit at some point in time.
    public class ShuntingUnit
    {
        public ShuntingUnit(string id)
        {
            this.MemberIDs = [];
            this.ParentIDs = [];
            this.ChildIDs = [];
            this.Id = id;
        }

        public ShuntingUnit(ShuntingUnit other)
        {
            this.Id = other.Id;
            this.MemberIDs = new List<string>(other.MemberIDs);
            this.ParentIDs = new List<string>(other.ParentIDs);
            this.ChildIDs = new List<string>(other.ChildIDs);
            this.StandingType = other.StandingType;
        }

        // Unique ID of this ShuntingUnit
        public string Id { get; set; }

        // The TrainUnits contained in this ShuntingUnit
        public IList<string> MemberIDs { get; set; }

        // The parents of a current ShuntingUnit,
        // that is, the shuntingunits which have been merged into this one,
        // or the shuntingunit that has been split into (among others) this one.
        public IList<string> ParentIDs { get; set; }

        // The children of the current ShuntingUnit,
        // that is, the shuntingunits which contain parts of this shuntingunit.
        // Alternatively, ShuntingUnit S has parent P iff P has child S.
        public IList<string> ChildIDs { get; set; }

        // If field is defined it states InStanding when the train unit was alredy on the yard even if the action says it is an arrival
        // or it states OutStanding when the train unit will stay in the shunting yards after the scenario ends even if the action is an exite one
        public string? StandingType { get; set; }
    }

    // A task specification specifies a certain task.
    public class TaskSpec
    {
        // The type of the task
        public TaskType? Type { get; set; }

        // The priority; higher values indicate that this task is more important.
        public uint? Priority { get; set; } // TODO set deprecation? or remove wholesale?

        // Time this task takes, in seconds
        public ulong? Duration { get; set; }

        // The skills required to perform the task. Each entry in the list indicates that a member of staff
        // with the given skill is required.
        // Examples:
        // [] => no personnel required
        // ["B-controle"] => one member of staff with skill "B-controle" required
        // ["B-controle", "B-controle"] => two members of staff with skill "B-controle" required
        public IList<string>? RequiredSkills { get; set; }
    }

    // A member of staff is a human that is able to perform various tasks at the facility.
    public class MemberOfStaff
    {
        // A unique ID which is referenced by other messages
        public ulong? Id { get; set; }

        // The type of staff, e.g. engineer, cleaning team, etc.e
        public string? Type { get; set; }

        // The skills the member of staff possesses.
        public IList<string>? Skills { get; set; }

        // The time intervals during which the member of staff is present
        public IList<TimeInterval>? Shifts { get; set; }

        // The time intervals in which breaks must take place.
        public IList<TimeInterval>? BreakWindows { get; set; }
    }
}
