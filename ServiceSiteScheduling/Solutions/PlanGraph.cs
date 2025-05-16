using ServiceSiteScheduling.Parking;
using ServiceSiteScheduling.Routing;
using ServiceSiteScheduling.Matching;
using ServiceSiteScheduling.Tasks;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using ServiceSiteScheduling.TrackParts;
using Google.Protobuf;


namespace ServiceSiteScheduling.Solutions
{
    class PlanGraph
    {
        public ShuntTrainUnit[] ShuntUnits { get; private set; }

        TrackOccupation[] TrackOccupations;
        bool[] outsidetrack;

        public RoutingGraph RoutingGraph;

        public ArrivalTask[] ArrivalTasks { get; private set; }
        DepartureTask[] DepartureTasks;

        public TrainMatching Matching { get; private set; }

        public ArrivalTask FirstArrival { get { return this.ArrivalTasks.First(arrival => arrival.Next.PreviousMove == null); } }

        public MoveTask First { get; set; }
        public MoveTask Last { get; set; }

        public SolutionCost Cost;

        private bool[][] FreeServiceTaskFinished;

        public PartialOrderSchedule POS { get; set; }

        public int testIndex { get; set; }

        public PlanGraph(TrainMatching matching, RoutingGraph graph, ShuntTrainUnit[] shuntunits, ArrivalTask[] arrivals, DepartureTask[] departures)
        {
            this.RoutingGraph = graph;
            this.Matching = matching;
            this.ShuntUnits = shuntunits;
            this.ArrivalTasks = arrivals;
            this.DepartureTasks = departures;

            this.TrackOccupations = new TrackOccupation[ProblemInstance.Current.Tracks.Length];
            this.outsidetrack = new bool[ProblemInstance.Current.Tracks.Length];
            for (int i = 0; i < this.TrackOccupations.Length; i++)
            {
                var track = ProblemInstance.Current.Tracks[i];
                if (!track.IsActive)
                    continue;

                TrackOccupation occupation = new SimpleTrackOccupation(track);

                this.TrackOccupations[i] = occupation;
                this.RoutingGraph.SuperVertices[track.Index].TrackOccupation = occupation;

                if (ProblemInstance.Current.ArrivalsOrdered.Select(t => t.Track).Contains(track))
                    outsidetrack[i] = true;
                if (ProblemInstance.Current.DeparturesOrdered.Select(t => t.Track).Contains(track))
                    outsidetrack[i] = true;
            }

            this.FreeServiceTaskFinished = new bool[ProblemInstance.Current.TrainUnits.Length][];
            for (int i = 0; i < ProblemInstance.Current.TrainUnits.Length; i++)
                this.FreeServiceTaskFinished[i] = new bool[ProblemInstance.Current.FreeServices[i].Length];

            this.testIndex = 0;
        }

        public void UpdateRoutingOrder()
        {
            MoveTask move = this.First;
            int order = 1;
            while (move != null)
            {
                move.MoveOrder = order++;
#if DEBUG
                if (order > 100 * ProblemInstance.Current.TrainUnits.Length)
                    throw new InvalidOperationException("circular references");
#endif
                move = move.NextMove;
            }
        }

        public SolutionCost ComputeModel(MoveTask recomputestart, MoveTask recomputeend)
        {
            for (int i = 0; i < this.TrackOccupations.Length; i++)
                if (this.TrackOccupations[i] != null)
                    this.TrackOccupations[i].Reset();

            this.ComputeLocation(this.First, recomputestart, recomputeend);
            this.ComputeTime(recomputestart, recomputestart.PreviousMove?.End ?? 0);
            return this.ComputeCost();
        }

        public SolutionCost ComputeModel()
        {
            foreach (var departure in this.DepartureTasks)
                (departure.Previous as DepartureRoutingTask).UpdatePreviousTaskOrder();

            return this.ComputeModel(this.First, this.Last);
        }

        public void ComputeLocation(MoveTask start, MoveTask recomputestart, MoveTask recomputeend)
        {
            MoveTask move = start;
            while (move != null)
            {
                if (move.TaskType == MoveTaskType.Standard)
                {
                    var routing = (RoutingTask)move;

                    // Arrive previously if necessary
                    if (routing.Previous.TaskType == TrackTaskType.Arrival)
                        routing.Previous.Arrive(this.TrackOccupations[routing.Previous.Track.Index]);

                    // Departure crossings
                    int departurecrossingsA = 0,
                        departurecrossingsB = 0;

                    // Depart from the previous track
                    if (routing.FromTrack != routing.ToTrack)
                    {
                        if (routing.FromTrack.Access.HasFlag(Side.A))
                            departurecrossingsA = routing.Previous.State.GetCrossings(Side.A);
                        if (routing.FromTrack.Access.HasFlag(Side.B))
                            departurecrossingsB = routing.Previous.State.GetCrossings(Side.B);

                        routing.Previous.Depart(this.TrackOccupations[routing.FromTrack.Index]);
                    }

                    if (move.MoveOrder >= recomputestart.MoveOrder && move.MoveOrder <= recomputeend.MoveOrder)
                        this.ComputeRouting(routing, departurecrossingsA, departurecrossingsB);

                    if (routing.FromTrack == routing.ToTrack)
                        foreach (TrackTask to in routing.Next)
                            to.Replace(routing.Previous);
                    else
                    {
                        {
                            if (routing.ToSide == Side.A)
                            {
                                for (int i = routing.Next.Count - 1; i >= 0; i--)
                                {
                                    var to = routing.Next[i];
                                    to.Arrive(this.TrackOccupations[to.Track.Index]);
                                    if (to is DepartureTask)
                                        to.Depart(this.TrackOccupations[to.Track.Index]);
                                }
                            }
                            else
                            {
                                for (int i = 0; i < routing.Next.Count; i++)
                                {
                                    var to = routing.Next[i];
                                    to.Arrive(this.TrackOccupations[to.Track.Index]);
                                    if (to is DepartureTask)
                                        to.Depart(this.TrackOccupations[to.Track.Index]);
                                }
                            }
                        }
                    }
                }
                else
                {
                    var departure = (DepartureRoutingTask)move;

                    if (move.MoveOrder >= recomputestart.MoveOrder)
                        computeDepartureRoutes(departure);
                    else
                        foreach (var task in departure.Previous)
                            task.Depart(this.TrackOccupations[task.Track.Index]);
                }

                move = move.NextMove;
            }

            if (this.POS == null)
            {
                POS = new PartialOrderSchedule(start);
                POS.InitializePOS();
                POS.CreatePOS();
                POS.DisplayInfrastructure();
                POS.DisplayMovements();
                POS.DisplayMoveLinksOfPOSMove(24, "trainUint");
            }
            else
            {

                if (this.testIndex == 100)
                {
                    Console.WriteLine($"Iteration: {this.testIndex}");
                }
                this.testIndex++;

            }


        }


        public void ComputeTime(MoveTask start, Time time)
        {
            MoveTask move = start;
            while (move != null)
            {
                if (move.TaskType == MoveTaskType.Standard)
                {
                    var routing = (RoutingTask)move;

                    // Compute the starting time
                    if (routing.Previous.TaskType == TrackTaskType.Arrival)
                    {
                        var arrival = (ArrivalTask)routing.Previous;
                        routing.Start = arrival.Start = arrival.ScheduledTime;
                        if (arrival.ArrivalSide == routing.FromSide)
                            routing.Start += routing.Train.ReversalDuration;
                    }
                    else if (routing.Previous.TaskType == TrackTaskType.Service)
                        routing.Start = routing.Previous.Start + ((ServiceTask)routing.Previous).MinimumDuration;
                    else
                        routing.Start = time;

                    routing.Start = Math.Max(routing.Start, time);

                    if (routing.Previous.Previous?.ToSide == routing.FromSide && routing.Start < routing.Previous.Previous.End + routing.Train.ReversalDuration)
                        routing.Start = routing.Previous.Previous.End + routing.Train.ReversalDuration;

                    // Update previous components
                    routing.Previous.End = routing.Start;

                    // Compute the end time
                    routing.End = routing.Start + routing.Duration;
                    time = routing.End;

                    // Update next components
                    foreach (TrackTask next in routing.Next)
                    {
                        if (next.TaskType == TrackTaskType.Service)
                        {
                            var service = (ServiceTask)next;
                            if (service.PreviousServiceTask != null)
                                service.Start = Math.Max(routing.End, service.PreviousServiceTask.Start + service.PreviousServiceTask.MinimumDuration);
                            else
                                service.Start = routing.End;
                        }
                        else
                            next.Start = routing.End;
                    }
                }
                else
                {
                    var departurerouting = (DepartureRoutingTask)move;
                    var reversalduration = departurerouting.Next.DepartureSide == departurerouting.ToSide ? departurerouting.Train.ReversalDuration : (Time)0;
                    departurerouting.Start = Math.Max(time, departurerouting.Next.ScheduledTime - departurerouting.Duration - reversalduration);
                    foreach (var task in departurerouting.Previous)
                    {
                        if (task.TaskType == TrackTaskType.Service)
                            departurerouting.Start = Math.Max(departurerouting.Start, task.Start + ((ServiceTask)task).MinimumDuration);
                    }

                    foreach (var previous in departurerouting.Previous)
                        previous.End = departurerouting.Start;

                    departurerouting.End = departurerouting.Start + departurerouting.Duration;
                    departurerouting.Next.Start = departurerouting.End;
                    departurerouting.Next.End = departurerouting.Next.Start + reversalduration;
                    time = departurerouting.End;
                }

                move = move.NextMove;
            }
        }

        public void OutputMovementSchedule()
        {
            MoveTask move = this.First;
            while (move != null)
            {
                var routing = move as RoutingTask;
                if (routing != null)
                {
                    Console.WriteLine("--> Routing Task");
                    Console.WriteLine($" ===> {routing}");
                    string arrivalmessage = string.Empty;
                    if (routing.Previous.TaskType == TrackTaskType.Arrival)
                    {
                        var arrival = (ArrivalTask)routing.Previous;
                        if (arrival.End < arrival.ScheduledTime + (arrival.ArrivalSide == arrival.Next.FromSide ? arrival.Train.ReversalDuration : (Time)0))
                            arrivalmessage = " <--- " + (arrival.ScheduledTime + (arrival.ArrivalSide == arrival.Next.FromSide ? arrival.Train.ReversalDuration : (Time)0)).ToString();
                    }
                    Console.WriteLine($"{move.Start} | {move.Train} from {routing.FromTrack.PrettyName}{routing.FromSide} to {move.ToTrack.PrettyName}{move.ToSide} | {move.End} {arrivalmessage}");
                    Console.WriteLine($"    {routing.ToRouteString()}");
                }
                else
                {
                    Console.WriteLine("--> Departure Task");

                    var departure = move as DepartureRoutingTask;
                    if (departure != null)
                    {
                        string departuremessage = string.Empty;
                        if (departure.Next.Start + (departure.Next.DepartureSide == departure.ToSide ? departure.Train.ReversalDuration : (Time)0) > departure.Next.ScheduledTime)
                            departuremessage = " <--- " + departure.Next.ScheduledTime.ToString();
                        Console.WriteLine($"{move.Start} | {move.Train} from ({string.Join(",", departure.Previous.Select(task => task.Track.PrettyName))}) to {move.ToTrack.PrettyName}{move.ToSide} {move.End} {departuremessage}");
                        foreach (var route in departure.GetRoutes())
                            Console.WriteLine($"    {route.Train} : {route}");
                    }
                }
                move = move.NextMove;
            }
        }

        public void OutputTrainUnitSchedule()
        {
            foreach (ShuntTrainUnit unit in this.ShuntUnits)
            {
                string line = $"{unit.Name} : {unit.Arrival.Track.PrettyName} (Arrival {unit.Arrival.Start.ToMinuteString()} - {unit.Arrival.End.ToMinuteString()})";
                var move = unit.Arrival.Next;
                while (move != null)
                {
                    var task = move.GetNext(t => t.Train.UnitBits[unit.Index]).First();
                    line += $", {task.Track.PrettyName} ({(task as ServiceTask)?.Type.Name ?? (task is DepartureTask ? "departure" : "parking")} {task.Start.ToMinuteString()} - {task.End.ToMinuteString()})";
                    move = task.Next;
                }
                Console.WriteLine(line);
            }
        }

        public void OutputConstraintViolations()
        {
            foreach (TrackOccupation occupation in this.TrackOccupations)
                if (occupation != null && occupation.ViolatingStates.Count > 0 && !outsidetrack[occupation.Track.Index])
                    Console.WriteLine($"{occupation.Track.ID.ToString()}: {string.Join(", ", occupation.ViolatingStates.Select(state => state.Task.Train.ToString()))}");
        }

        public void ComputeRouting(RoutingTask routing, int departurecrossingsA, int departurecrossingsB)
        {
            Track fromtrack = routing.FromTrack, totrack = routing.ToTrack;
            Side toside = routing.ToSide;

            if (fromtrack.Access == Side.Both)
            {
                var routeA = this.RoutingGraph.ComputeRoute(this.TrackOccupations, routing.Train, fromtrack, Side.A, totrack, toside);
                routeA.DepartureCrossings = departurecrossingsA;

                var routeB = this.RoutingGraph.ComputeRoute(this.TrackOccupations, routing.Train, fromtrack, Side.B, totrack, toside, routeA.TrackState);
                routeB.DepartureCrossings = departurecrossingsB;

                if (routeA.Crossings + routeA.DepartureCrossings < routeB.Crossings + routeB.DepartureCrossings)
                    routing.AddRoute(routeA);
                else if (routeA.Crossings + routeA.DepartureCrossings > routeB.Crossings + routeB.DepartureCrossings)
                    routing.AddRoute(routeB);
                else if (routeA.Duration < routeB.Duration)
                    routing.AddRoute(routeA);
                else
                    routing.AddRoute(routeB);
            }
            else
            {
                var route = this.RoutingGraph.ComputeRoute(this.TrackOccupations, routing.Train, fromtrack, fromtrack.Access, totrack, toside);
                route.DepartureCrossings = fromtrack.Access == Side.A ? departurecrossingsA : departurecrossingsB;
                routing.AddRoute(route);
            }
        }

        public Route ComputeRouting(ShuntTrain train, Track fromtrack, Track totrack, Side toside, int departurecrossingsA, int departurecrossingsB)
        {
            if (fromtrack.Access == Side.Both)
            {
                var routeA = this.RoutingGraph.ComputeRoute(this.TrackOccupations, train, fromtrack, Side.A, totrack, toside);
                routeA.DepartureCrossings = departurecrossingsA;

                var routeB = this.RoutingGraph.ComputeRoute(this.TrackOccupations, train, fromtrack, Side.B, totrack, toside, routeA.TrackState);
                routeB.DepartureCrossings = departurecrossingsB;

                if (routeA.Crossings + routeA.DepartureCrossings < routeB.Crossings + routeB.DepartureCrossings)
                    return routeA;
                if (routeA.Crossings + routeA.DepartureCrossings > routeB.Crossings + routeB.DepartureCrossings)
                    return routeB;
                if (routeA.Duration < routeB.Duration)
                    return routeA;
                return routeB;
            }
            else
            {
                var route = this.RoutingGraph.ComputeRoute(this.TrackOccupations, train, fromtrack, fromtrack.Access, totrack, toside);
                route.DepartureCrossings = fromtrack.Access == Side.A ? departurecrossingsA : departurecrossingsB;
                return route;
            }
        }

        public string RoutingOrdering()
        {
            MoveTask move = this.First;
            string result = string.Empty;
            while (move != null)
            {

                result += $"{move.Start} - {move.End}: {move.ToString()} {move.DepartureCrossings}+{move.Crossings}, ";
                move = move.NextMove;
            }
            return result;
        }

        public SolutionCost ComputeCost()
        {
            SolutionCost cost = new SolutionCost();

            foreach (bool[] finished in this.FreeServiceTaskFinished)
                for (int i = 0; i < finished.Length; i++)
                    finished[i] = true;

            bool checkmaintenance = true;
            BitSet done = new BitSet(ProblemInstance.Current.TrainUnits.Length);

            foreach (ArrivalTask arrival in this.ArrivalTasks)
                if (arrival.End > arrival.ScheduledTime + (arrival.ArrivalSide == arrival.Next.FromSide ? arrival.Train.ReversalDuration : (Time)0))
                {
                    cost.ArrivalDelays++;
                    cost.ArrivalDelaySum += arrival.End - arrival.ScheduledTime;
                    cost.ProblemTrains |= arrival.Train.UnitBits;
                }

            foreach (DepartureTask departure in this.DepartureTasks)
                if (departure.Start + (departure.DepartureSide == departure.Previous.ToSide ? departure.Train.ReversalDuration : (Time)0) > departure.ScheduledTime)
                {
                    cost.DepartureDelays++;
                    cost.DepartureDelaySum += departure.Start - departure.ScheduledTime;
                    cost.ProblemTrains |= departure.Train.UnitBits;
                }

            MoveTask move = this.First;
            while (move != null)
            {
                cost.ShuntMoves += move.NumberOfRoutes;
                cost.RoutingDurationSum += move.Duration;
                cost.Crossings += move.Crossings + move.DepartureCrossings;

                if (move.Crossings > 0)
                {
                    cost.ProblemTrains |= move.Train.UnitBits;
                    cost.ProblemTracks |= move.CrossingTracks;
                }

                if (move.DepartureCrossings > 0)
                {
                    cost.ProblemTrains |= move.Train.UnitBits;
                    cost.ProblemTracks |= move.DepartureCrossingTracks;
                }

                if (move.TaskType == MoveTaskType.Departure)
                {
                    var routes = ((DepartureRoutingTask)move).GetRoutes();
                    if (routes.Count > 1)
                        cost.CombineOnDepartureTrack += routes.Count - 1;
                }

                if (checkmaintenance)
                {
                    foreach (var task in move.AllNext)
                    {
                        if (task.TaskType != TrackTaskType.Parking || task.Train.UnitBits.IsSubsetOf(done))
                            continue;

                        Time time = 0;
                        foreach (ShuntTrainUnit unit in task.Train.Units)
                        {
                            if (done[unit.Index])
                                continue;

                            if (ProblemInstance.Current.FreeServices[unit.Index].Length == 0)
                                done[unit.Index] = true;

                            var tasks = ProblemInstance.Current.FreeServices[unit.Index];
                            var finished = this.FreeServiceTaskFinished[unit.Index];
                            bool allfinished = true;
                            for (int i = 0; i < finished.Length; i++)
                                if (!finished[i] && tasks[i].Type.Tracks.Contains(task.Track) && time + tasks[i].Duration <= task.End - task.Start)
                                {
                                    time += tasks[i].Duration;
                                    finished[i] = true;
                                }
                                else
                                    allfinished &= finished[i];

                            if (allfinished)
                                done[unit.Index] = true;

                            if (time > task.End - task.Start)
                                break;
                        }
                    }
                }

                move = move.NextMove;
            }

            if (checkmaintenance)
                cost.UnplannedMaintenance = ProblemInstance.Current.TrainUnits.Length - done.Count;

            var tracklengthviolations = this.TrackOccupations.Where((graph, i) => !outsidetrack[i] && graph != null && graph.TrackLengthViolations > 0);
            foreach (var occ in tracklengthviolations)
            {
                cost.TrackLengthViolations += occ.TrackLengthViolations;
                cost.TrackLengthViolationSum += occ.TrackLengthViolationSum;
                cost.ProblemTracks[occ.Track.Index] = true;
                foreach (var state in occ.ViolatingStates)
                    foreach (var unit in state.Task.Train.Units)
                        cost.ProblemTrains[unit.Index] = true;
            }

            return cost;
        }

        protected void computeDepartureRoutes(DepartureRoutingTask task)
        {
            task.ClearRoutes();

            TrackTask previous = null;
            ShuntTrain train = null;
            TrackTask first = null, last = null;
            State next = null;
            bool newShuntTrainConstructed = false;
            for (int i = 0; i < task.Previous.Count; i++)
            {
                TrackTask tracktask = task.ToSide == Side.A ? task.Previous[task.Previous.Count - i - 1] : task.Previous[i];

                // get the adjacent state
                var currentnext = task.ToSide == Side.A ? tracktask.State.A : tracktask.State.B;
                tracktask.State.ComputeCrossings();

                // depart the current task
                tracktask.Depart(this.TrackOccupations[tracktask.Track.Index]);

                // compute route for previous train if necessary
                if (tracktask.Track != previous?.Track || (next != tracktask.State))
                {
                    if (train != null)
                        this.computeDepartureRoute(task, train, previous.Track, first, last);

                    train = null;
                }

                if (train == null)
                {
                    newShuntTrainConstructed = false;
                    train = tracktask.Train;
                    first = tracktask;
                }
                else
                {
                    if (!newShuntTrainConstructed)
                    {
                        newShuntTrainConstructed = true;
                        train = new ShuntTrain(train);
                    }

                    if (task.ToSide == Side.B)
                        train.Units.AddRange(tracktask.Train.Units);
                    else
                    {
                        var units = new List<ShuntTrainUnit>(tracktask.Train.Units);
                        units.AddRange(train.Units);
                        train.Units = units;
                    }
                    train.UnitBits |= tracktask.Train.UnitBits;
                }

                last = tracktask;
                previous = tracktask;
                next = currentnext;
            }

            if (train != null)
                this.computeDepartureRoute(task, train, previous.Track, first, last);
        }

        protected void computeDepartureRoute(DepartureRoutingTask move, ShuntTrain train, Track track, TrackTask first, TrackTask last)
        {
            int a = 0, b = 0;
            if (track.Access == Side.Both)
            {
                if (move.ToSide == Side.A)
                {
                    a = first.State.GetCrossings(Side.A);
                    b = last.State.GetCrossings(Side.B);
                }
                else
                {
                    a = last.State.GetCrossings(Side.A);
                    b = first.State.GetCrossings(Side.B);
                }
            }
            else if (track.Access == Side.A)
                a = first.State.GetCrossings(Side.A);
            else
                b = first.State.GetCrossings(Side.B);

            move.AddRoute(this.ComputeRouting(train, track, move.ToTrack, move.ToSide, a, b));
        }

        public bool HasSufficientSpace(ShuntTrain train, Track track, double start, double end)
        {
            return this.TrackOccupations[track.Index].HasSufficientSpace(train, start, end);
        }

        public void CheckCorrectness()
        {
            // Routing order
            MoveTask move = this.First;
            List<TrackTask> tasks = new List<TrackTask>();
            while (move != null)
            {
                if (!move.AllPreviousSatisfy(t => t is ParkingTask) && !move.AllNextSatisfy(t => t is ParkingTask) && !move.SkipsParking)
                    throw new InvalidOperationException("move failed to mention parking skipping");

                foreach (TrackTask task in move.AllPrevious)
                {
                    if (task.Next != move)
                        throw new InvalidOperationException("track-route linkage failure");
                    task.Next.FindAllNext(t => t == task, tasks);
                    if (tasks.Count > 0)
                        throw new InvalidOperationException("track-route circular reference");
                    if (!(task is ArrivalTask))
                    {
                        task.Previous.FindAllPrevious(t => t == task, tasks);
                        if (tasks.Count > 0)
                            throw new InvalidOperationException("track-route circular reference");
                    }

                    var service = task as ServiceTask;
                    if (service != null)
                    {
                        for (ServiceTask s = service.NextServiceTask; s != null; s = s.NextServiceTask)
                        {
                            if (service == s)
                                throw new InvalidOperationException("circular service references");
                            if (service.Resource != s.Resource)
                                throw new InvalidOperationException("invalid service references");
                            if (service.Next.MoveOrder > s.Previous.MoveOrder || service.Next.MoveOrder > s.Next.MoveOrder)
                                throw new InvalidOperationException("resource conflict");
                        }
                        for (ServiceTask s = service.PreviousServiceTask; s != null; s = s.PreviousServiceTask)
                        {
                            if (service == s)
                                throw new InvalidOperationException("circular service references");
                            if (service.Resource != s.Resource)
                                throw new InvalidOperationException("invalid service references");
                            if (service.Previous.MoveOrder < s.Previous.MoveOrder || service.Previous.MoveOrder < s.Next.MoveOrder)
                                throw new InvalidOperationException("resource conflict");
                        }
                    }
                }

                foreach (TrackTask task in move.AllNext)
                {
                    if (task.Previous != move)
                        throw new InvalidOperationException("track-route linkage failure");
                    if (!(task is DepartureTask))
                    {
                        task.Next.FindAllNext(t => t == task, tasks);
                        if (tasks.Count > 0)
                            throw new InvalidOperationException("track-route circular reference");
                    }
                    task.Previous.FindAllPrevious(t => t == task, tasks);
                    if (tasks.Count > 0)
                        throw new InvalidOperationException("track-route circular reference");
                }

                if (move.AllNext.Count > 1 && move.AllNext.Any(task => task.Track != move.AllNext.First().Track || task.ArrivalSide != move.AllNext.First().ArrivalSide))
                    throw new InvalidOperationException("split not on same track");

                if (move.PreviousMove != null && move.PreviousMove.NextMove != move)
                    throw new InvalidOperationException("move-move linkage failure");

                for (MoveTask other = move.NextMove; other != null; other = other.NextMove)
                    if (other == move)
                        throw new InvalidOperationException("circular move-move references");
                for (MoveTask other = move.PreviousMove; other != null; other = other.PreviousMove)
                    if (other == move)
                        throw new InvalidOperationException("circular move-move references");

                move = move.NextMove;
            }
        }

        public void Clear()
        {
            foreach (var location in ProblemInstance.Current.ServiceLocations)
                if (location != null)
                    location.First = location.Last = null;
        }

        public void OutputForDemian()
        {
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter("demian.txt"))
            {
                MoveTask move = this.First;
                while (move != null)
                {
                    var routing = move as RoutingTask;
                    if (routing != null)
                    {
                        string arrivalmessage = string.Empty;
                        if (routing.Previous.TaskType == TrackTaskType.Arrival)
                        {
                            var arrival = (ArrivalTask)routing.Previous;
                            if (arrival.End < arrival.ScheduledTime + (arrival.ArrivalSide == arrival.Next.FromSide ? arrival.Train.ReversalDuration : (Time)0))
                                arrivalmessage = " <--- " + (arrival.ScheduledTime + (arrival.ArrivalSide == arrival.Next.FromSide ? arrival.Train.ReversalDuration : (Time)0)).ToString();
                        }
                        sw.WriteLine($"{move.Start} | {move.Train} from {routing.FromTrack.PrettyName}{routing.FromSide} to {move.ToTrack.PrettyName}{move.ToSide} | {move.End} {arrivalmessage}");
                        sw.WriteLine($"    {routing.ToRouteString()}");
                    }
                    else
                    {
                        var departure = move as DepartureRoutingTask;
                        if (departure != null)
                        {
                            string departuremessage = string.Empty;
                            if (departure.Next.Start + (departure.Next.DepartureSide == departure.ToSide ? departure.Train.ReversalDuration : (Time)0) > departure.Next.ScheduledTime)
                                departuremessage = " <--- " + departure.Next.ScheduledTime.ToString();
                            sw.WriteLine($"{move.Start} | {move.Train} from ({string.Join(",", departure.Previous.Select(task => task.Track.PrettyName))}) to {move.ToTrack.PrettyName}{move.ToSide} {move.End} {departuremessage}");
                            foreach (var route in departure.GetRoutes())
                                sw.WriteLine($"    {route.Train} : {route}");
                        }
                    }
                    move = move.NextMove;
                }
                sw.WriteLine();
                sw.WriteLine();
                sw.WriteLine();

                foreach (ShuntTrainUnit unit in this.ShuntUnits)
                {
                    string line = $"{unit.Name} : {unit.Arrival.Track.PrettyName} (Arrival {unit.Arrival.Start.ToMinuteString()})";
                    move = unit.Arrival.Next;
                    while (move != null)
                    {
                        var task = move.GetNext(t => t.Train.UnitBits[unit.Index]).First();
                        line += $", {task.Track.PrettyName} ({(task as ServiceTask)?.Type.Name ?? (task is DepartureTask ? "departure" : "parking")} {task.Start.ToMinuteString()})";
                        move = task.Next;
                    }
                    sw.WriteLine(line);
                }
            }
        }

        public void GenerateOutput(string path)
        {
            if (ProblemInstance.Current.InterfaceLocation == null || ProblemInstance.Current.InterfaceScenario == null)
                return;

            AlgoIface.Plan plan = new AlgoIface.Plan();

            Dictionary<ShuntTrain, AlgoIface.ShuntingUnit> trainconversion = new Dictionary<ShuntTrain, AlgoIface.ShuntingUnit>();

            MoveTask move = this.First;
            while (move != null)
            {
                if (move.TaskType == MoveTaskType.Standard)
                {
                    var routing = (RoutingTask)move;
                    var endtime = (ulong)routing.End;

                    // Add split
                    if (routing.IsSplit)
                    {
                        var splitaction = new AlgoIface.Action();
                        splitaction.Location = routing.ToTrack.ID;
                        splitaction.TaskType = new AlgoIface.TaskType();
                        splitaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Split;
                        splitaction.EndTime = endtime;
                        splitaction.StartTime = endtime = (ulong)(routing.End - routing.Train.Units[0].Type.SplitDuration * (routing.Next.Count - 1));
                        splitaction.ShuntingUnit = GetShuntUnit(move.Train, trainconversion);
                        plan.Actions.Add(splitaction);

                        // add parent-child relation
                        foreach (var task in routing.Next)
                        {
                            var shuntingunit = GetShuntUnit(task.Train, trainconversion);
                            splitaction.ShuntingUnit.ChildIDs.Add(shuntingunit.Id);
                            shuntingunit.ParentIDs.Add(splitaction.ShuntingUnit.Id);
                        }
                    }

                    // Add move
                    if (move.Duration > 0)
                    {
                        var moveaction = new AlgoIface.Action();
                        moveaction.Location = routing.FromTrack.ID;
                        moveaction.TaskType = new AlgoIface.TaskType();
                        moveaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Move;
                        moveaction.StartTime = (ulong)routing.Start;
                        moveaction.EndTime = endtime;
                        moveaction.ShuntingUnit = GetShuntUnit(move.Train, trainconversion);

                        Infrastructure previous = null;
                        foreach (var arc in routing.Route.Arcs)
                        {
                            foreach (var infra in arc.Path.Path)
                            {
                                if (infra != previous)
                                {
                                    var resource = new AlgoIface.Resource();
                                    resource.TrackPartId = infra.ID;
                                    resource.Name = infra.ID.ToString();
                                    moveaction.Resources.Add(resource);

                                    previous = infra;
                                }
                            }
                        }
                        // remove first
                        moveaction.Resources.RemoveAt(0);
                        // add to plan
                        plan.Actions.Add(moveaction);
                    }
                    else
                    {
                        // Zero move ? 


                    }
                    // Add task
                    AddTrackAction(routing.Previous, trainconversion, plan);
                }
                else
                {
                    var departurerouting = (DepartureRoutingTask)move;
                    var starttime = departurerouting.Start;

                    foreach (var route in departurerouting.GetRoutes())
                    {
                        var tasks = departurerouting.GetPrevious(task => task.Train.UnitBits.Intersects(route.Train.UnitBits));
                        var shuntingunit = GetShuntUnit(route.Train, trainconversion);

                        // Add tasks
                        foreach (var task in tasks)
                            AddTrackAction(task, starttime, trainconversion, plan);

                        // Add merge
                        if (tasks.Count() > 1)
                        {
                            foreach (var task in tasks)
                            {
                                var mergeaction = new AlgoIface.Action();
                                mergeaction.Location = task.Track.ID;
                                mergeaction.TaskType = new AlgoIface.TaskType();
                                mergeaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Combine;
                                mergeaction.StartTime = (ulong)starttime;
                                mergeaction.EndTime = (ulong)(starttime + departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1));
                                mergeaction.ShuntingUnit = GetShuntUnit(task.Train, trainconversion);
                                plan.Actions.Add(mergeaction);

                                // add parent-child-relation
                                mergeaction.ShuntingUnit.ChildIDs.Add(shuntingunit.Id);
                                shuntingunit.ParentIDs.Add(mergeaction.ShuntingUnit.Id);
                            }
                            starttime += departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1);
                        }

                        // Add move
                        var moveaction = new AlgoIface.Action();
                        moveaction.Location = route.Tracks[0].ID;
                        moveaction.TaskType = new AlgoIface.TaskType();
                        moveaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Move;
                        moveaction.StartTime = (ulong)starttime;
                        moveaction.EndTime = (ulong)(starttime + route.Duration - departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1));
                        moveaction.ShuntingUnit = shuntingunit;
                        // add path
                        Infrastructure previous = null;
                        foreach (var arc in route.Arcs)
                        {
                            foreach (var infra in arc.Path.Path)
                            {
                                if (infra != previous)
                                {
                                    var resource = new AlgoIface.Resource();
                                    resource.TrackPartId = infra.ID;
                                    resource.Name = infra.ID.ToString();
                                    moveaction.Resources.Add(resource);

                                    previous = infra;
                                }
                            }
                        }
                        // remove first
                        moveaction.Resources.RemoveAt(0);
                        // add to plan
                        plan.Actions.Add(moveaction);

                        starttime += route.Duration;
                    }

                    var departureshuntunit = GetShuntUnit(departurerouting.Train, trainconversion);

                    // Add merge
                    if (departurerouting.GetRoutes().Count > 1)
                    {
                        foreach (var route in departurerouting.GetRoutes())
                        {
                            var mergeaction = new AlgoIface.Action();
                            mergeaction.Location = departurerouting.Next.Track.ID;
                            mergeaction.TaskType = new AlgoIface.TaskType();
                            mergeaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Combine;
                            mergeaction.StartTime = (ulong)starttime;
                            mergeaction.EndTime = (ulong)departurerouting.End;
                            mergeaction.ShuntingUnit = GetShuntUnit(route.Train, trainconversion);
                            plan.Actions.Add(mergeaction);

                            // add parent-child-relation
                            mergeaction.ShuntingUnit.ChildIDs.Add(departureshuntunit.Id);
                            departureshuntunit.ParentIDs.Add(mergeaction.ShuntingUnit.Id);
                        }
                    }

                    // Add departure
                    this.AddTrackAction(departurerouting.Next, trainconversion, plan);
                }

                move = move.NextMove;
            }

            using (var output = System.IO.File.Create(path))
                plan.WriteTo(output);
        }

        public AlgoIface.Plan GenerateOutputPB()
        {
            if (ProblemInstance.Current.InterfaceLocation == null || ProblemInstance.Current.InterfaceScenario == null)
                return null;

            AlgoIface.Plan plan = new AlgoIface.Plan();

            Dictionary<ShuntTrain, AlgoIface.ShuntingUnit> trainconversion = new Dictionary<ShuntTrain, AlgoIface.ShuntingUnit>();

            MoveTask move = this.First;
            while (move != null)
            {
                if (move.TaskType == MoveTaskType.Standard)
                {
                    var routing = (RoutingTask)move;
                    var endtime = (ulong)routing.End;

                    // Add split
                    if (routing.IsSplit)
                    {
                        var splitaction = new AlgoIface.Action();
                        splitaction.Location = routing.ToTrack.ID;
                        splitaction.TaskType = new AlgoIface.TaskType();
                        splitaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Split;
                        splitaction.EndTime = endtime;
                        splitaction.StartTime = endtime = (ulong)(routing.End - routing.Train.Units[0].Type.SplitDuration * (routing.Next.Count - 1));
                        splitaction.ShuntingUnit = GetShuntUnit(move.Train, trainconversion);
                        plan.Actions.Add(splitaction);

                        // add parent-child relation
                        foreach (var task in routing.Next)
                        {
                            var shuntingunit = GetShuntUnit(task.Train, trainconversion);
                            splitaction.ShuntingUnit.ChildIDs.Add(shuntingunit.Id);
                            shuntingunit.ParentIDs.Add(splitaction.ShuntingUnit.Id);
                        }
                    }

                    // Add move
                    if (move.Duration > 0)
                    {
                        var moveaction = new AlgoIface.Action();
                        moveaction.Location = routing.FromTrack.ID;
                        moveaction.TaskType = new AlgoIface.TaskType();
                        moveaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Move;
                        moveaction.StartTime = (ulong)routing.Start;
                        moveaction.EndTime = endtime;
                        moveaction.ShuntingUnit = GetShuntUnit(move.Train, trainconversion);

                        Infrastructure previous = null;
                        foreach (var arc in routing.Route.Arcs)
                        {
                            foreach (var infra in arc.Path.Path)
                            {
                                if (infra != previous)
                                {
                                    var resource = new AlgoIface.Resource();
                                    resource.TrackPartId = infra.ID;
                                    resource.Name = infra.ID.ToString();
                                    moveaction.Resources.Add(resource);

                                    previous = infra;
                                }
                            }
                        }
                        // remove first
                        moveaction.Resources.RemoveAt(0);
                        // add to plan
                        plan.Actions.Add(moveaction);
                    }

                    // Add task
                    AddTrackAction(routing.Previous, trainconversion, plan);
                }
                else
                {
                    var departurerouting = (DepartureRoutingTask)move;
                    var starttime = departurerouting.Start;

                    foreach (var route in departurerouting.GetRoutes())
                    {
                        var tasks = departurerouting.GetPrevious(task => task.Train.UnitBits.Intersects(route.Train.UnitBits));
                        var shuntingunit = GetShuntUnit(route.Train, trainconversion);

                        // Add tasks
                        foreach (var task in tasks)
                            AddTrackAction(task, starttime, trainconversion, plan);

                        // Add merge
                        if (tasks.Count() > 1)
                        {
                            foreach (var task in tasks)
                            {
                                var mergeaction = new AlgoIface.Action();
                                mergeaction.Location = task.Track.ID;
                                mergeaction.TaskType = new AlgoIface.TaskType();
                                mergeaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Combine;
                                mergeaction.StartTime = (ulong)starttime;
                                mergeaction.EndTime = (ulong)(starttime + departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1));
                                mergeaction.ShuntingUnit = GetShuntUnit(task.Train, trainconversion);
                                plan.Actions.Add(mergeaction);

                                // add parent-child-relation
                                mergeaction.ShuntingUnit.ChildIDs.Add(shuntingunit.Id);
                                shuntingunit.ParentIDs.Add(mergeaction.ShuntingUnit.Id);
                            }
                            starttime += departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1);
                        }

                        // Add move
                        var moveaction = new AlgoIface.Action();
                        moveaction.Location = route.Tracks[0].ID;
                        moveaction.TaskType = new AlgoIface.TaskType();
                        moveaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Move;
                        moveaction.StartTime = (ulong)starttime;
                        moveaction.EndTime = (ulong)(starttime + route.Duration - departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1));
                        moveaction.ShuntingUnit = shuntingunit;
                        // add path
                        Infrastructure previous = null;
                        foreach (var arc in route.Arcs)
                        {
                            foreach (var infra in arc.Path.Path)
                            {
                                if (infra != previous)
                                {
                                    var resource = new AlgoIface.Resource();
                                    resource.TrackPartId = infra.ID;
                                    resource.Name = infra.ID.ToString();
                                    moveaction.Resources.Add(resource);

                                    previous = infra;
                                }
                            }
                        }
                        // remove first
                        if(moveaction.Resources.Count > 0)
                            moveaction.Resources.RemoveAt(0);
                        // add to plan
                        plan.Actions.Add(moveaction);

                        starttime += route.Duration;
                    }

                    var departureshuntunit = GetShuntUnit(departurerouting.Train, trainconversion);

                    // Add merge
                    if (departurerouting.GetRoutes().Count > 1)
                    {
                        foreach (var route in departurerouting.GetRoutes())
                        {
                            var mergeaction = new AlgoIface.Action();
                            mergeaction.Location = departurerouting.Next.Track.ID;
                            mergeaction.TaskType = new AlgoIface.TaskType();
                            mergeaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Combine;
                            mergeaction.StartTime = (ulong)starttime;
                            mergeaction.EndTime = (ulong)departurerouting.End;
                            mergeaction.ShuntingUnit = GetShuntUnit(route.Train, trainconversion);
                            plan.Actions.Add(mergeaction);

                            // add parent-child-relation
                            mergeaction.ShuntingUnit.ChildIDs.Add(departureshuntunit.Id);
                            departureshuntunit.ParentIDs.Add(mergeaction.ShuntingUnit.Id);
                        }
                    }

                    // Add departure
                    this.AddTrackAction(departurerouting.Next, trainconversion, plan);
                }

                move = move.NextMove;
            }

            return plan;
        }


        public AlgoIface.Plan GenerateOutputPB_extended()
        {
            if (ProblemInstance.Current.InterfaceLocation == null || ProblemInstance.Current.InterfaceScenario == null)
                return null;

            AlgoIface.Plan plan = new AlgoIface.Plan();

            Dictionary<ShuntTrain, AlgoIface.ShuntingUnit> trainconversion = new Dictionary<ShuntTrain, AlgoIface.ShuntingUnit>();

            MoveTask move = this.First;
            while (move != null)
            {
                if (move.TaskType == MoveTaskType.Standard)
                {
                    var routing = (RoutingTask)move;
                    var endtime = (ulong)routing.End;
                
                    // Add split
                    if (routing.IsSplit)
                    {
                        var splitaction = new AlgoIface.Action();
                        splitaction.Location = routing.ToTrack.ID;
                        splitaction.TaskType = new AlgoIface.TaskType();
                        splitaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Split;
                        splitaction.EndTime = endtime;
                        splitaction.StartTime = endtime = (ulong)(routing.End - routing.Train.Units[0].Type.SplitDuration * (routing.Next.Count - 1));
                        splitaction.ShuntingUnit = GetShuntUnit(move.Train, trainconversion);
                        plan.Actions.Add(splitaction);

                        // add parent-child relation
                        foreach (var task in routing.Next)
                        {
                            var shuntingunit = GetShuntUnit(task.Train, trainconversion);
                            splitaction.ShuntingUnit.ChildIDs.Add(shuntingunit.Id);
                            shuntingunit.ParentIDs.Add(splitaction.ShuntingUnit.Id);
                        }
                    }

                    // Add move

                    var moveaction = new AlgoIface.Action();
                    moveaction.Location = routing.FromTrack.ID;
                    moveaction.TaskType = new AlgoIface.TaskType();
                    moveaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Move;
                    moveaction.StartTime = (ulong)routing.Start;
                    moveaction.EndTime = endtime;
                    moveaction.ShuntingUnit = GetShuntUnit(move.Train, trainconversion);

                    Infrastructure previous = null;
                    foreach (var arc in routing.Route.Arcs)
                    {
                        foreach (var infra in arc.Path.Path)
                        {
                            if (infra != previous)
                            {
                                var resource = new AlgoIface.Resource();
                                resource.TrackPartId = infra.ID;
                                resource.Name = infra.ID.ToString();
                                moveaction.Resources.Add(resource);

                                previous = infra;
                            }
                        }
                    }
                    if (moveaction.Resources.Count > 0)
                    {
                        // remove first
                        moveaction.Resources.RemoveAt(0);
                    }
                    // add to plan
                    plan.Actions.Add(moveaction);

                    // Add task
                    AddTrackAction(routing.Previous, trainconversion, plan);
                }
                else
                {
                    var departurerouting = (DepartureRoutingTask)move;
                    var starttime = departurerouting.Start;

                    foreach (var route in departurerouting.GetRoutes())
                    {
                        var tasks = departurerouting.GetPrevious(task => task.Train.UnitBits.Intersects(route.Train.UnitBits));
                        var shuntingunit = GetShuntUnit(route.Train, trainconversion);

                        // Add tasks
                        foreach (var task in tasks)
                            AddTrackAction(task, starttime, trainconversion, plan);

                        // Add merge
                        if (tasks.Count() > 1)
                        {
                            foreach (var task in tasks)
                            {
                                var mergeaction = new AlgoIface.Action();
                                mergeaction.Location = task.Track.ID;
                                mergeaction.TaskType = new AlgoIface.TaskType();
                                mergeaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Combine;
                                mergeaction.StartTime = (ulong)starttime;
                                mergeaction.EndTime = (ulong)(starttime + departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1));
                                mergeaction.ShuntingUnit = GetShuntUnit(task.Train, trainconversion);
                                plan.Actions.Add(mergeaction);

                                // add parent-child-relation
                                mergeaction.ShuntingUnit.ChildIDs.Add(shuntingunit.Id);
                                shuntingunit.ParentIDs.Add(mergeaction.ShuntingUnit.Id);
                            }
                            starttime += departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1);
                        }

                        // Add move
                        var moveaction = new AlgoIface.Action();
                        moveaction.Location = route.Tracks[0].ID;
                        moveaction.TaskType = new AlgoIface.TaskType();
                        moveaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Move;
                        moveaction.StartTime = (ulong)starttime;
                        moveaction.EndTime = (ulong)(starttime + route.Duration - departurerouting.Train.Units[0].Type.CombineDuration * (tasks.Count() - 1));
                        moveaction.ShuntingUnit = shuntingunit;
                        // add path
                        Infrastructure previous = null;
                        foreach (var arc in route.Arcs)
                        {
                            foreach (var infra in arc.Path.Path)
                            {
                                if (infra != previous)
                                {
                                    var resource = new AlgoIface.Resource();
                                    resource.TrackPartId = infra.ID;
                                    resource.Name = infra.ID.ToString();
                                    moveaction.Resources.Add(resource);

                                    previous = infra;
                                }
                            }
                        }
                        // remove first
                        moveaction.Resources.RemoveAt(0);
                        // add to plan
                        plan.Actions.Add(moveaction);

                        starttime += route.Duration;
                    }

                    var departureshuntunit = GetShuntUnit(departurerouting.Train, trainconversion);

                    // Add merge
                    if (departurerouting.GetRoutes().Count > 1)
                    {
                        foreach (var route in departurerouting.GetRoutes())
                        {
                            var mergeaction = new AlgoIface.Action();
                            mergeaction.Location = departurerouting.Next.Track.ID;
                            mergeaction.TaskType = new AlgoIface.TaskType();
                            mergeaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Combine;
                            mergeaction.StartTime = (ulong)starttime;
                            mergeaction.EndTime = (ulong)departurerouting.End;
                            mergeaction.ShuntingUnit = GetShuntUnit(route.Train, trainconversion);
                            plan.Actions.Add(mergeaction);

                            // add parent-child-relation
                            mergeaction.ShuntingUnit.ChildIDs.Add(departureshuntunit.Id);
                            departureshuntunit.ParentIDs.Add(mergeaction.ShuntingUnit.Id);
                        }
                    }

                    // Add departure
                    this.AddTrackAction(departurerouting.Next, trainconversion, plan);
                }

                move = move.NextMove;
            }

            return plan;
        }

         public void DisplayMovements()
        {
            MoveTask move = this.First;
            int i = 0;
            while (move != null)
            {
                Console.WriteLine($"Move: {i} --- {move.TaskType}");
                Console.WriteLine($"Start time: {(int)move.Start} - End time: {(int)move.End}");
                Console.WriteLine($"From : {move.FromTrack} -> To : {move.ToTrack} ({move.Train})");

                var routing = move as RoutingTask;

                if (routing != null)
                {
                    Console.WriteLine("Infrastrucre used (tracks):");
                    var tracks = routing.Route.Tracks;
                    var lastTrack = tracks.Last();

                    Console.WriteLine("All Previous tasks:");
                    foreach (TrackTask task in routing.AllPrevious)
                    {
                        Console.WriteLine($"---{task} - Start Time: {(int)task.Start} - End Time: {(int)task.End}----");
                        if (task is ParkingTask parkingTask)
                            Console.WriteLine("@It was a ParkingTask");
                        if (task is ServiceTask serviceTask)
                            Console.WriteLine("@It was a ServiceTask");
                        if (task is ArrivalTask arrivalTask)
                            Console.WriteLine("@It was a ArrivalTask");
                        if (task is DepartureTask departureTask)
                            Console.WriteLine("@It was a DepartureTask");
                    }

                    Console.WriteLine("All Next tasks:");
                    foreach (TrackTask task in routing.AllNext)
                    {
                        Console.WriteLine($"---{task} - Start Time: {(int)task.Start} - End Time: {(int)task.End}----");
                        if (task is ParkingTask parkingTask)
                            Console.WriteLine("@It was a ParkingTask");
                        if (task is ServiceTask serviceTask)
                            Console.WriteLine("@It was a ServiceTask");
                        if (task is ArrivalTask arrivalTask)
                            Console.WriteLine("@It was a ArrivalTask");
                        if (task is DepartureTask departureTask)
                            Console.WriteLine("@It was a DepartureTask");
                    }

                    foreach (Track track in tracks)
                    {
                        if (track != lastTrack)
                        {
                            Console.Write($" A side {track.ASide} -->");
                            Console.Write($" {track} --> ");
                            Console.Write($" B side {track.BSide} -->");
                        }
                        else
                        {
                            Console.Write($" A side {track.ASide} -->");
                            Console.Write($" {track} -->");

                            Console.Write($" B side {track.BSide} ");



                        }

                    }
                    Console.WriteLine("");

                }
                else
                {
                    if (move.TaskType is MoveTaskType.Departure)
                    {
                        Console.WriteLine("All Previous tasks:");
                        foreach (TrackTask task in move.AllPrevious)
                        {
                            Console.WriteLine($"---{task} - Start Time: {(int)task.Start} - End Time: {(int)task.End}----");
                            if (task is ParkingTask parkingTask)
                                Console.WriteLine("@It was a ParkingTask");
                            if (task is ServiceTask serviceTask)
                                Console.WriteLine("@It was a ServiceTask");
                            if (task is ArrivalTask arrivalTask)
                                Console.WriteLine("@It was a ArrivalTask");
                            if (task is DepartureTask departureTask)
                                Console.WriteLine("@It was a DepartureTask");
                        }

                        Console.WriteLine("All Next tasks:");
                        foreach (TrackTask task in move.AllNext)
                        {
                            Console.WriteLine($"---{task} - Start Time: {(int)task.Start} - End Time: {(int)task.End}----");
                            if (task is ParkingTask parkingTask)
                                Console.WriteLine("@It was a ParkingTask");
                            if (task is ServiceTask serviceTask)
                                Console.WriteLine("@It was a ServiceTask");
                            if (task is ArrivalTask arrivalTask)
                                Console.WriteLine("@It was a ArrivalTask");
                            if (task is DepartureTask departureTask)
                                Console.WriteLine("@It was a DepartureTask");
                        }



                        var routingDeparture = move as DepartureRoutingTask;
                        if (routingDeparture != null)
                        {
                            var listOfRoutes = routingDeparture.GetRoutes();

                            Console.WriteLine($"Infrastrucre used (tracks) number of routes {listOfRoutes.Count}:");


                            foreach (Route route in listOfRoutes)
                            {
                                var Tracks = route.Tracks;
                                var lastTrack = Tracks.Last();

                                foreach (Track track in Tracks)
                                {

                                    if (track != lastTrack)
                                    {
                                        Console.Write($" A side {track.ASide} -->");
                                        Console.Write($" {track} --> ");
                                        Console.Write($" B side {track.BSide} -->");
                                    }
                                    else
                                    {
                                        Console.Write($" A side {track.ASide} -->");
                                        Console.Write($" {track} -->");

                                        Console.Write($" B side {track.BSide} ");
                                    }


                                }
                                Console.WriteLine("");
                            }
                            Console.WriteLine("");
                        }
                    }
                }
                i++;
                move = move.NextMove;
            }
        }


        private void AddTrackAction(TrackTask task, Dictionary<ShuntTrain, AlgoIface.ShuntingUnit> trainconversion, AlgoIface.Plan plan)
        {
            this.AddTrackAction(task, task.End, trainconversion, plan);
        }

        private void AddTrackAction(TrackTask task, Time endtime, Dictionary<ShuntTrain, AlgoIface.ShuntingUnit> trainconversion, AlgoIface.Plan plan)
        {
            var trackaction = new AlgoIface.Action();
            trackaction.Location = task.Track.ID;
            trackaction.ShuntingUnit = GetShuntUnit(task.Train, trainconversion);
            trackaction.TaskType = new AlgoIface.TaskType();
            switch (task.TaskType)
            {
                case TrackTaskType.Arrival:
                    var arrival = (ArrivalTask)task;
                    trackaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Arrive;
                    trackaction.StartTime = trackaction.EndTime = (ulong)arrival.ScheduledTime;

                    var gatewayconnection = ProblemInstance.Current.GatewayConversion[task.Track.ID];
                    trackaction.Location = gatewayconnection.Path[0].ID;
                    Infrastructure previous = null;
                    foreach (var infra in gatewayconnection.Path)
                        if (infra != previous)
                        {
                            var resource = new AlgoIface.Resource();
                            resource.TrackPartId = infra.ID;
                            resource.Name = infra.ID.ToString();
                            trackaction.Resources.Add(resource);

                            previous = infra;
                        }
                    trackaction.Resources.RemoveAt(0);

                    if (endtime > arrival.ScheduledTime)
                    {
                        var nextparking = new AlgoIface.Action();
                        nextparking.Location = task.Track.ID;
                        nextparking.ShuntingUnit = GetShuntUnit(task.Train, trainconversion);
                        nextparking.TaskType = new AlgoIface.TaskType();
                        nextparking.TaskType.Predefined = AlgoIface.PredefinedTaskType.Wait;
                        nextparking.StartTime = trackaction.EndTime;
                        nextparking.EndTime = (ulong)endtime;
                        plan.Actions.Add(nextparking);
                    }
                    break;
                case TrackTaskType.Parking:
                    trackaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Wait;
                    trackaction.StartTime = (ulong)task.Start;
                    trackaction.EndTime = (ulong)endtime;
                    break;
                case TrackTaskType.Service:
                    var service = (ServiceTask)task;

                    if (service.Start > service.Previous.End)
                    {
                        var previousparking = new AlgoIface.Action();
                        previousparking.Location = task.Track.ID;
                        previousparking.ShuntingUnit = GetShuntUnit(task.Train, trainconversion);
                        previousparking.TaskType = new AlgoIface.TaskType();
                        previousparking.TaskType.Predefined = AlgoIface.PredefinedTaskType.Wait;
                        previousparking.StartTime = (ulong)service.Previous.End;
                        previousparking.EndTime = (ulong)service.Start;
                        plan.Actions.Add(previousparking);
                    }

                    trackaction.TaskType.Other = service.Type.Name;
                    trackaction.StartTime = (ulong)service.Start;
                    trackaction.EndTime = trackaction.StartTime + (ulong)service.MinimumDuration;

                    if (endtime - service.Start > service.MinimumDuration)
                    {
                        var nextparking = new AlgoIface.Action();
                        nextparking.Location = task.Track.ID;
                        nextparking.ShuntingUnit = GetShuntUnit(task.Train, trainconversion);
                        nextparking.TaskType = new AlgoIface.TaskType();
                        nextparking.TaskType.Predefined = AlgoIface.PredefinedTaskType.Wait;
                        nextparking.StartTime = trackaction.EndTime;
                        nextparking.EndTime = (ulong)endtime;
                        plan.Actions.Add(nextparking);
                    }

                    var facilityresource = new AlgoIface.Resource();
                    facilityresource.FacilityId = ProblemInstance.Current.FacilityConversion[service.Type].Id;
                    facilityresource.Name = ProblemInstance.Current.FacilityConversion[service.Type].Id.ToString();
                    trackaction.Resources.Add(facilityresource);
                    break;
                case TrackTaskType.Departure:
                    trackaction.TaskType.Predefined = AlgoIface.PredefinedTaskType.Exit;
                    trackaction.StartTime = trackaction.EndTime = (ulong)task.End;

                    gatewayconnection = ProblemInstance.Current.GatewayConversion[task.Track.ID];
                    previous = null;
                    for (int i = gatewayconnection.Path.Length - 1; i >= 0; i--)
                    {
                        var infra = gatewayconnection.Path[i];
                        if (infra != previous)
                        {
                            var resource = new AlgoIface.Resource();
                            resource.TrackPartId = infra.ID;
                            resource.Name = infra.ID.ToString();
                            trackaction.Resources.Add(resource);
                        }
                    }
                    trackaction.Resources.RemoveAt(0);
                    break;
            }
            plan.Actions.Add(trackaction);
        }

        private AlgoIface.ShuntingUnit GetShuntUnit(ShuntTrain train, Dictionary<ShuntTrain, AlgoIface.ShuntingUnit> trainconversion)
        {
            AlgoIface.ShuntingUnit shuntingunit = null;
            if (!trainconversion.TryGetValue(train, out shuntingunit))
            {
                shuntingunit = new AlgoIface.ShuntingUnit();
                foreach (var unit in train.Units)
                    shuntingunit.Members.Add(ProblemInstance.Current.TrainUnitConversion[unit.Base]);
                shuntingunit.Id = ((trainconversion.Count > 0 ? trainconversion.Max(kvp => int.Parse(kvp.Value.Id)) : -1) + 1).ToString();
                trainconversion[train] = shuntingunit;
            }
            return shuntingunit;
        }
    }
}