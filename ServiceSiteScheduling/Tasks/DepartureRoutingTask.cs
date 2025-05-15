using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Tasks
{
    class DepartureRoutingTask : MoveTask
    {
        private List<Routing.Route> routes;
        private Dictionary<Trains.ShuntTrain, Stack<RoutingTask>> routetoskippedparkings;
        private Dictionary<Trains.ShuntTrain, Stack<ParkingTask>> skippedparkings;

        public List<TrackTask> Previous { get; set; }
        public DepartureTask Next { get; set; }

        public override bool SkipsParking { get { return this.Previous.Any(task => task.TaskType != TrackTaskType.Parking); } }

        public override IList<TrackTask> AllPrevious { get {return this.Previous;} }
        public override IList<TrackTask> AllNext { get { return new TrackTask[1] { this.Next }; } }

        public override Side FromSide { get { return this.routes.Count > 0 ? this.routes[0].DepartureSide : Side.None; } }

        public override Time Duration { get { return this.duration + (this.Previous.Count - 1) * this.Train.Units[0].Type.CombineDuration; } }
        public override int Crossings { get { return this.crossings; } }
        public override int DepartureCrossings { get { return this.departurecrossings; } }
        public override int NumberOfRoutes { get { return this.numberofroutes; } }

        public override BitSet CrossingTracks { get { return this.crossingtracks; } }

        public override BitSet DepartureCrossingTracks { get { return this.departurecrossingtracks; } }

        private Time duration;

        private int crossings, departurecrossings, numberofroutes;
        private BitSet crossingtracks, departurecrossingtracks;

        public DepartureRoutingTask(Trains.ShuntTrain train) : base(train, MoveTaskType.Departure)
        {
            this.routes = new List<Routing.Route>();
            this.routetoskippedparkings = new Dictionary<Trains.ShuntTrain, Stack<RoutingTask>>();
            this.skippedparkings = new Dictionary<Trains.ShuntTrain, Stack<ParkingTask>>();
            this.Previous = new List<TrackTask>();
            this.crossingtracks = new BitSet(ProblemInstance.Current.Tracks.Length);
            this.departurecrossingtracks = new BitSet(ProblemInstance.Current.Tracks.Length);
        }

        public void UpdatePreviousTaskOrder()
        {
            if (this.Previous.Count <= 1)
                return;

            this.Previous.Sort((a, b) => this.Train.Units.IndexOf(a.Train.Units[0]).CompareTo(this.Train.Units.IndexOf(b.Train.Units[0])));
        }

        public override void AddRoute(Routing.Route route)
        {
            this.routes.Add(route);

            if (route.Duration > 0)
            {
                this.numberofroutes++;
                this.duration += route.Duration;
            }

            if (route.Crossings > 0)
            {
                this.crossings += route.Crossings;
                this.crossingtracks |= route.CrossingTracks;
            }
            if (route.DepartureCrossings > 0)
            {
                this.departurecrossings += route.DepartureCrossings;
                if (route.Tracks.Length > 0)
                    this.departurecrossingtracks[route.Tracks[0].Index] = true;
            }
        }

        public void ClearRoutes()
        {
            this.routes.Clear();
            this.duration = this.crossings = this.departurecrossings = this.numberofroutes = 0;
            this.crossingtracks.Clear();
            this.departurecrossingtracks.Clear();
        }

        public IList<Routing.Route> GetRoutes()
        {
            return this.routes;
        }

        public override bool IsParkingSkipped(Trains.ShuntTrain train)
        {
            return this.Previous.Any(task => task.TaskType != TrackTaskType.Parking && task.Train.Equals(train));
        }

        public override ParkingTask GetSkippedParking(Trains.ShuntTrain train)
        {
            return this.skippedparkings[train].Peek();
        }

        public override RoutingTask GetRouteToSkippedParking(Trains.ShuntTrain train)
        {
            return this.routetoskippedparkings[train].Peek();
        }

        public override void SkipParking(ParkingTask parking)
        {
            RoutingTask previous = parking.Previous as RoutingTask;

            previous.Remove();

            // update track-route linking
            previous.Previous.Next = this;
            this.Previous[this.Previous.IndexOf(parking)] = previous.Previous;

            // update state
            if (!this.skippedparkings.ContainsKey(parking.Train))
            {
                this.skippedparkings[parking.Train] = new Stack<ParkingTask>();
                this.routetoskippedparkings[parking.Train] = new Stack<RoutingTask>();
            }

            this.skippedparkings[parking.Train].Push(parking);
            this.routetoskippedparkings[parking.Train].Push(previous);
        }

        public override void UnskipParking(Trains.ShuntTrain train)
        {
            var routing = this.routetoskippedparkings[train].Pop();
            var parking = this.skippedparkings[train].Pop();

            // update consecutive route linking
            routing.InsertBefore(this);

            // update track-route linking
            var previoustask = this.Previous.First(task => task.Train.Equals(train));
            previoustask.Next = routing;
            routing.Previous = previoustask;
            this.Previous[this.Previous.IndexOf(previoustask)] = parking;

            // update state
            this.routetoskippedparkings.Remove(train);
            this.skippedparkings.Remove(train);
            routing.FromTrack = previoustask.Track;
        }

        public override void ReplacePreviousTask(TrackTask task)
        {
            this.Previous[this.Previous.FindIndex(t => t.Train.Equals(task.Train))] = task;
        }

        public override void ReplaceNextTask(TrackTask task)
        {
            this.Next = (DepartureTask)task;
        }

        public override IEnumerable<TrackTask> GetPrevious(Func<TrackTask, bool> selector)
        {
            return this.Previous.Where(selector);
        }

        public override IEnumerable<TrackTask> GetNext(Func<TrackTask, bool> selector)
        {
            return selector(this.Next) ? new TrackTask[1] { this.Next } : new TrackTask[0];
        }

        public override bool AllPreviousSatisfy(Func<TrackTask, bool> predicate)
        {
            return this.Previous.All(predicate);
        }

        public override bool AllNextSatisfy(Func<TrackTask, bool> predicate)
        {
            return predicate(this.Next);
        }

        public override string ToString()
        {
            return $"({this.Train} {this.FromTrack?.ID.ToString() ?? "?"}{this.FromSide}->{this.ToTrack?.ID.ToString() ?? "?"}{this.ToSide} : {string.Join(",", this.routes.Select(route => (route.Tracks.Length > 0 ? $"{route.Train}: {route.Tracks.First().ID}{route.DepartureSide}->{route.Tracks.Last().ID}" : "invalid")))})";
        }
    }
}
