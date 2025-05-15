using ServiceSiteScheduling.Tasks;

namespace ServiceSiteScheduling.Parking
{
    class State : DequeNode<State>
    {
        public TrackTask Task { get; private set; }
        public TrackOccupation TrackOccupation { get; set; }
        public List<State> StatesA { get; private set; }
        public List<State> StatesB { get; private set; }
        public Side ArrivalSide { get; set; }
        public Side DepartureSide { get; set; }

        public int DistanceA { get; set; }
        public int DistanceB { get; set; }

        public bool HasArrived { get; set; }
        public bool HasDeparted { get; set; }
        public bool ExceedsTrackLength { get; set; }
        public int Crossings { get { if (!this.crossingsComputed) this.ComputeCrossings(); return this.DepartureSide == Side.A ? this.CrossingsA : this.CrossingsB; } }

        protected int CrossingsA, CrossingsB;
        protected List<State> CrossingStatesA, CrossingStatesB;
        protected bool crossingsComputed = false;

        public State(TrackTask task)
        {
            this.Task = task;
            this.StatesA = new List<State>();
            this.StatesB = new List<State>();
            this.CrossingStatesA = new List<State>();
            this.CrossingStatesB = new List<State>();
            this.HasArrived = this.HasDeparted = this.ExceedsTrackLength = false;
        }

        public List<State> GetAdjacent(Side side) { return side == Side.A ? this.StatesA : this.StatesB; }

        public int GetDistance(Side side) { return side == Side.A ? this.DistanceA : this.DistanceB; }

        public void SetDistance(Side side, int value) { if (side == Side.A) this.DistanceA = value; else this.DistanceB = value; }

        public int GetCrossings(Side side) { if (!this.crossingsComputed) this.ComputeCrossings(); return side == Side.A ? this.CrossingsA : this.CrossingsB; }

        public void Reset()
        {
            this.DistanceA = this.DistanceB = 0;
            this.StatesA.Clear();
            this.StatesB.Clear();
            this.HasArrived = this.HasDeparted = this.ExceedsTrackLength = false;
            this.CrossingsA = this.CrossingsB = 0;
            this.crossingsComputed = false;
            this.CrossingStatesA.Clear();
            this.CrossingStatesB.Clear();
        }

        public override string ToString()
        {
            return $"{this.Task.Train} at {this.Task.Track.ID}";
        }

        public void ComputeCrossings()
        {
            if (this.TrackOccupation.Track.Access.HasFlag(Side.A))
                this.ComputeCrossings(Side.A);
            if (this.TrackOccupation.Track.Access.HasFlag(Side.B))
                this.ComputeCrossings(Side.B);
            this.crossingsComputed = true;
        }

        protected void ComputeCrossings(Side side)
        {
            List<State> states = side == Side.A ? this.CrossingStatesA : this.CrossingStatesB;
            var next = this.Next(side);
            while (next != null)
            {
                states.Add(next);
                next = next.Next(side);
            }

            if (side == Side.A)
                this.CrossingsA = states.Count > 0 ? 1: 0;
            else
                this.CrossingsB = states.Count > 0 ? 1 : 0;
        }
    }
}
