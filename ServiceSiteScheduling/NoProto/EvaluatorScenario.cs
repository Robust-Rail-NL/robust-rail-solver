#nullable enable

using System.Collections;

namespace ServiceSiteScheduling.NoProto
{
    // A Scenario contains the part of the problem specification which varies daily,
    // that is the trains which come in and go out of the shunting area.
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

    public class TrainUnit2
    {
        // A unique identifier of the unit
        public string? Id { get; set; }

        public TrainUnitType? Type { get; set; }

        public string? TypeDisplayName { get; set; }

        public IList<TaskSpec>? tasks { get; set; }
    }

    // A ShuntingUnit is a combination of TrainUnits,
    // and moves as a unit at some point in time.
    public class ShuntingUnit2
    {
        public ShuntingUnit2() { }

        public ShuntingUnit2(ShuntingUnit other)
        {
            this.Id = other.Id;
            if (other.Members != null)
                this.Members = new List<TrainUnit>(other.Members);
            if (other.ParentIDs != null)
                this.ParentIDs = new List<string>(other.ParentIDs);
            if (other.ChildIDs != null)
                this.ChildIDs = new List<string>(other.ChildIDs);
            this.StandingType = other.StandingType;
        }

        // Unique ID of this ShuntingUnit
        public string? Id { get; set; }

        // The TrainUnits contained in this ShuntingUnit
        public IList<TrainUnit>? Members { get; set; }

        // The TrainUnits contained in this ShuntingUnit
        public IList<string>? MemberIDs { get; set; }

        // The parents of a current ShuntingUnit,
        // that is, the shuntingunits which have been merged into this one,
        // or the shuntingunit that has been split into (among others) this one.
        public IList<string>? ParentIDs { get; set; }

        // The children of the current ShuntingUnit,
        // that is, the shuntingunits which contain parts of this shuntingunit.
        // Alternatively, ShuntingUnit S has parent P iff P has child S.
        public IList<string>? ChildIDs { get; set; }

        // If field is defined it states InStanding when the train unit was alredy on the yard even if the action says it is an arrival
        // or it states OutStanding when the train unit will stay in the shunting yards after the scenario ends even if the action is an exite one
        public string? StandingType { get; set; }
    }

    // A task specification specifies a certain task.
    public class TaskSpec2
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
