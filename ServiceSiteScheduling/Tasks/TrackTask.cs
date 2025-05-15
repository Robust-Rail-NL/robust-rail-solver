
namespace ServiceSiteScheduling.Tasks
{
    public enum TrackTaskType { Arrival, Departure, Service, Parking }
    abstract class TrackTask
    {
        public Trains.ShuntTrain Train { get; set; }
        public TrackParts.Track Track { get; set; }
        public MoveTask Previous { get; set; }
        public MoveTask Next { get; set; }
        public Utilities.Time Start { get; set; }
        public Utilities.Time End { get; set; }
        public Parking.State State { get; set; }
        public Side ArrivalSide { get; set; }
        public TrackTaskType TaskType { get { return this.tasktype; } }
        protected readonly TrackTaskType tasktype;

        private Parking.State originalState { get; set; }

        public TrackTask(Trains.ShuntTrain train, TrackParts.Track track, TrackTaskType type)
        {
            this.Train = train;
            this.Track = track;
            this.State = new Parking.State(this);
            this.tasktype = type;
        }

        public override string ToString()
        {
            return $"{this.Start} - {this.End} : {this.Train} at {this.Track.ID}";
        }

        public void Arrive(Parking.TrackOccupation track)
        {
            if (this.originalState != null)
            {
                this.State = this.originalState;
                this.State.Reset();
                this.originalState = null;
            }

            track.Arrive(this);
        }

        public void Depart(Parking.TrackOccupation track)
        {
            track.Depart(this);
        }

        public void Replace(TrackTask other)
        {
            if (this.originalState == null)
                this.originalState = this.State;
            this.State = other.State;
        }

        public List<TrackTask> GetRelatedTasks()
        {
            return this.getRelatedTasks(new List<TrackTask>());
        }

        private List<TrackTask> getRelatedTasks(List<TrackTask> list)
        {
            if (list.Contains(this))
                return list;

            list.Add(this);
            if (this.Previous.AllNext.Count > 1)
                foreach (var task in this.Previous.AllNext)
                    if (!list.Contains(task))
                        task.getRelatedTasks(list);

            if (this.Next.AllPrevious.Count > 1)
                foreach (var task in this.Next.AllPrevious)
                    if (!list.Contains(task))
                        task.getRelatedTasks(list);

            return list;
        }
    }
}
