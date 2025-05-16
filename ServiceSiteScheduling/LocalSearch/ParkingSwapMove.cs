using ServiceSiteScheduling.Solutions;

namespace ServiceSiteScheduling.LocalSearch
{
    class ParkingSwapMove : LocalSearchMove
    {
        public IList<Tasks.TrackTask> ParkingFirst { get; private set; }
        public IList<Tasks.TrackTask> ParkingSecond { get; private set; }

        protected TrackParts.Track[] firsttracks, secondtracks;
        protected Side[] firstsides, secondsides;

        public ParkingSwapMove(PlanGraph graph, IList<Tasks.TrackTask> parkingfirst, IList<Tasks.TrackTask> parkingsecond) : base(graph)
        {
            this.ParkingFirst = parkingfirst;
            this.ParkingSecond = parkingsecond;

            this.firsttracks = new TrackParts.Track[parkingfirst.Count];
            this.firstsides = new Side[parkingfirst.Count];
            for (int i = 0; i < parkingfirst.Count; i++)
            {
                this.firsttracks[i] = parkingfirst[i].Track;
                this.firstsides[i] = parkingfirst[i].ArrivalSide;
            }

            this.secondtracks = new TrackParts.Track[parkingsecond.Count];
            this.secondsides = new Side[parkingsecond.Count];
            for (int i = 0; i < parkingsecond.Count; i++)
            {
                this.secondtracks[i] = parkingsecond[i].Track;
                this.secondsides[i] = parkingsecond[i].ArrivalSide;
            }
        }

        public override SolutionCost Execute()
        {
            TrackParts.Track firsttrack = this.ParkingFirst.First().Track, secondtrack = this.ParkingSecond.First().Track;
            Side firstside = this.ParkingFirst.First().ArrivalSide, secondside = this.ParkingSecond.First().ArrivalSide;

            changeTrack(this.ParkingSecond, firsttrack, firstside);
            changeTrack(this.ParkingFirst, secondtrack, secondside);

            return base.Execute();
        }

        public override SolutionCost Revert()
        {
            TrackParts.Track firsttrack = this.ParkingFirst.First().Track, secondtrack = this.ParkingSecond.First().Track;
            Side firstside = this.ParkingFirst.First().ArrivalSide, secondside = this.ParkingSecond.First().ArrivalSide;

            changeTrack(this.ParkingSecond, firsttrack, firstside);
            changeTrack(this.ParkingFirst, secondtrack, secondside);

            return base.Revert();
        }

        private void changeTrack(IEnumerable<Tasks.TrackTask> set, TrackParts.Track track, Side side)
        {
            foreach (var task in set)
            {
                task.Track = track;
                task.ArrivalSide = side;

                task.Previous.ToTrack = track;
                task.Previous.ToSide = side;

                if (task.Next.TaskType == Tasks.MoveTaskType.Standard)
                    task.Next.FromTrack = track;
            }
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            var swapmove = move as ParkingSwapMove;
            if (swapmove == null)
                return false;

            return this.ParkingFirst.Intersect(swapmove.ParkingFirst).Count() > 0 || 
                this.ParkingFirst.Intersect(swapmove.ParkingSecond).Count() > 0 ||
                this.ParkingSecond.Intersect(swapmove.ParkingFirst).Count() > 0 ||
                this.ParkingSecond.Intersect(swapmove.ParkingSecond).Count() > 0;
        }

        public override string ToString()
        {
            return $"{this.Cost.BaseCost.ToString("N1")}: swap parking {this.ParkingFirst.First().Previous.Train} at {this.ParkingFirst.First().Track.ID.ToString()} with {this.ParkingSecond.First().Previous.Train}  at {this.ParkingSecond.First().Track.ID.ToString()}";
        }

        public static IList<ParkingSwapMove> GetMoves(PlanGraph graph)
        {
            List<ParkingSwapMove> moves = new List<ParkingSwapMove>();

            HashSet<Tasks.TrackTask> parkingtasks = new HashSet<Tasks.TrackTask>();

            for (var movetask = graph.First; movetask != null; movetask = movetask.NextMove)
            {
                var routing = movetask as Tasks.RoutingTask;
                if (routing != null)
                {
                    // Check if all tasks are parking tasks
                    if (routing.Next.All(task => task.TaskType == Tasks.TrackTaskType.Parking || (task as Tasks.ServiceTask)?.Type.LocationType == Servicing.ServiceLocationType.Free))
                    {
                        if (routing.Next.Any(task => task.Next.TaskType == Tasks.MoveTaskType.Departure && task.Next.AllPrevious.Count > 1))
                            continue;

                        var end = routing.LatestNext()?.MoveOrder ?? double.PositiveInfinity;

                        for (var nextmovetask = routing.NextMove; nextmovetask != null && nextmovetask.MoveOrder < end; nextmovetask = nextmovetask.NextMove)
                        {
                            if (nextmovetask.TaskType != Tasks.MoveTaskType.Standard || nextmovetask.Train.UnitBits.Intersects(routing.Train.UnitBits) || nextmovetask.ToTrack == routing.ToTrack)
                                continue;

                            if (nextmovetask.AllNext.Any(task => task.Next.TaskType == Tasks.MoveTaskType.Departure && task.Next.AllPrevious.Count > 0))
                                continue;

                            if (!nextmovetask.AllNext.All(task => task.TaskType == Tasks.TrackTaskType.Parking || (task as Tasks.ServiceTask)?.Type.LocationType == Servicing.ServiceLocationType.Free))
                                continue;

                            if (!nextmovetask.Train.ParkingLocations.Contains(routing.ToTrack) || !routing.Train.ParkingLocations.Contains(nextmovetask.ToTrack))
                                continue;

                            var move = new ParkingSwapMove(graph, routing.Next, nextmovetask.AllNext.ToList());
                            moves.Add(move);
                        }
                    }
                }
            }

            return moves;
        }
    }
}
