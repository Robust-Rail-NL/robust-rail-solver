#nullable enable

using System.Diagnostics;

namespace ServiceSiteScheduling.Tasks
{
    class DepartureTask : TrackTask, IFixedSchedule
    {
        public Utilities.Time ScheduledTime { get; set; }
        public Side DepartureSide { get; private set; }
        public new TrackParts.Track Track
        {
            get => base.Track ?? throw new InvalidOperationException("Track is null");
            set => base.Track = value;
        }

        public DepartureTask(
            Trains.ShuntTrain train,
            TrackParts.Track track,
            Side side,
            Utilities.Time time
        )
            : base(train, track, TrackTaskType.Departure)
        {
            this.ScheduledTime = time;
            this.Start = this.End = time;
            this.DepartureSide = side;
        }

        public DepartureRoutingTask GetDepartureRoutingTask()
        {
            Debug.Assert(Previous.TaskType == MoveTaskType.Departure);
            return (DepartureRoutingTask)this.Previous;
        }
    }
}
