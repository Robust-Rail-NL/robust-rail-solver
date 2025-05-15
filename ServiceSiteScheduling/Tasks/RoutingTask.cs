using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Tasks
{
    class RoutingTask : MoveTask
    {
        public TrackTask Previous { get; set; }
        public List<TrackTask> Next { get; set; }

        public override IList<TrackTask> AllPrevious { get { return new TrackTask[1] { this.Previous }; } }
        public override IList<TrackTask> AllNext { get { return this.Next; } }

        public override Time Duration { get { return (this.route?.Duration ?? 0) + (this.Next.Count - 1) * this.Train.Units[0].Type.SplitDuration; } }
        public override int Crossings { get { return this.route?.Crossings ?? 0; } }
        public override int DepartureCrossings { get { return this.route?.DepartureCrossings ?? 0; } }
        public override int NumberOfRoutes { get { return (this.route?.Duration ?? 0) > 0 ? 1 : 0; } }

        public override Side FromSide { get { return this.route?.DepartureSide ?? null; } }

        public override bool SkipsParking { get { return this.Previous.TaskType != TrackTaskType.Parking && this.Next.Any(task => task.TaskType != TrackTaskType.Parking); } }

        public Stack<RoutingTask> RouteToSkippedParking { get; set; }
        public Stack<ParkingTask> SkippedParking { get; set; }

        public bool IsSplit { get { return this.Next.Count > 1; } }

        public Routing.Route Route { get { return this.route; } }

        public override BitSet CrossingTracks { get { return this.route?.CrossingTracks ?? new BitSet(ProblemInstance.Current.Tracks.Length); } }

        public override BitSet DepartureCrossingTracks { get {
                return (this.route?.DepartureCrossings ?? 0) > 0 ? 
                    new BitSet(ProblemInstance.Current.Tracks.Length, this.FromTrack.Index) :
                    new BitSet(ProblemInstance.Current.Tracks.Length); } }

        private Routing.Route route;

        public RoutingTask(Trains.ShuntTrain train) : base(train, MoveTaskType.Standard)
        {
            this.Next = new List<TrackTask>();
            this.RouteToSkippedParking = new Stack<RoutingTask>();
            this.SkippedParking = new Stack<ParkingTask>();
        }

        public RoutingTask(RoutingTask routing) : base(routing.Train, MoveTaskType.Standard)
        {
            this.Previous = routing.Previous;
            this.Next = new List<TrackTask>(routing.Next);
            this.RouteToSkippedParking = new Stack<RoutingTask>(routing.RouteToSkippedParking);
            this.SkippedParking = new Stack<ParkingTask>(routing.SkippedParking);

            this.Start = routing.Start;
            this.End = routing.End;

            this.PreviousMove = routing.PreviousMove;
            this.NextMove = routing.NextMove;
            this.MoveOrder = routing.MoveOrder;

            this.FromTrack = routing.FromTrack;
            this.ToTrack = routing.ToTrack;
            this.ToSide = routing.ToSide;

            this.RouteToSkippedParking = routing.RouteToSkippedParking;
        }

        public virtual void UpdateArrivalOrder()
        {
            int taskindex = 0;
            TrackTask previous = null;

            foreach (TrackTask task in this.Next)
                if (!(task is DepartureTask))
                    task.Train.Units.Clear();

            foreach (Trains.ShuntTrainUnit unit in this.Train)
            {
                for (int i = 0; i < this.Next.Count; i++)
                {
                    var task = this.Next[i];
                    if (task.Train.UnitBits[unit.Index])
                    {
                        if (!(task is DepartureTask))
                            task.Train.Units.Add(unit);
                        if (previous != task)
                        {
                            this.Next[i] = this.Next[taskindex];
                            this.Next[taskindex++] = task;
                            previous = task;
                        }
                        break;
                    }
                }
            }
        }

        public override void AddRoute(Routing.Route route)
        {
            this.route = route;
        }

        public override string ToString()
        {
            return $"{this.Train}: {this.FromTrack?.ID.ToString() ?? "?"}{this.FromSide?.ToString() ?? ""}->{this.ToTrack?.ID.ToString() ?? "?"}{this.ToSide?.ToString() ?? ""}";
        }

        public string ToRouteString()
        {
            return this.route.ToString() + (this.route.Crossings + this.route.DepartureCrossings > 0 ? " <----- " : "");
        }

        public override bool IsParkingSkipped(Trains.ShuntTrain train)
        {
            return this.SkipsParking;
        }

        public override ParkingTask GetSkippedParking(Trains.ShuntTrain train)
        {
            return this.SkippedParking.Peek();
        }

        public override RoutingTask GetRouteToSkippedParking(Trains.ShuntTrain train)
        {
            return this.RouteToSkippedParking.Peek();
        }

        public override void ReplacePreviousTask(TrackTask task)
        {
            this.Previous = task;
        }

        public override void ReplaceNextTask(TrackTask task)
        {
            this.Next[this.Next.FindIndex(t => t.Train.Equals(task.Train))] = task;
        }

        public override void SkipParking(ParkingTask parking)
        {
            RoutingTask previous = parking.Previous as RoutingTask;

            previous.Remove();

            // update track-route linking
            previous.Previous.Next = this;
            this.Previous = previous.Previous;

            // update state
            this.SkippedParking.Push(parking);
            this.RouteToSkippedParking.Push(previous);
            this.FromTrack = previous.FromTrack;
        }

        public override void UnskipParking(Trains.ShuntTrain train)
        {
            var routing = this.RouteToSkippedParking.Pop();
            var parking = this.SkippedParking.Pop();

            // update consecutive route linking
            routing.InsertBefore(this);

            // update track-route linking
            this.Previous.Next = routing;
            routing.Previous = this.Previous;
            this.Previous = parking;

            // update state
            routing.FromTrack = this.FromTrack;
            this.FromTrack = parking.Track;
        }

        public override IEnumerable<TrackTask> GetPrevious(Func<TrackTask, bool> selector)
        {
            return selector(this.Previous) ? new TrackTask[1] { this.Previous } : new TrackTask[0];
        }

        public override IEnumerable<TrackTask> GetNext(Func<TrackTask, bool> selector)
        {
            return this.Next.Where(selector);
        }

        public override bool AllPreviousSatisfy(Func<TrackTask, bool> predicate)
        {
            return predicate(this.Previous);
        }

        public override bool AllNextSatisfy(Func<TrackTask, bool> predicate)
        {
            return this.Next.All(predicate);
        }
    }
}
