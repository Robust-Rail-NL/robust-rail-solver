#nullable enable

using System.Diagnostics;
using ServiceSiteScheduling.Matching;
using ServiceSiteScheduling.Servicing;
using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Tasks;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Initial
{
    class SimpleHeuristic
    {
        public static PlanGraph Construct(Random random, int debugLevel = 0)
        {
            ShuntTrainUnit[] shunttrainunits = ProblemInstance
                .Current.TrainUnits.Select(tu => new ShuntTrainUnit(tu))
                .ToArray();
            ShuntTrain[] arrivalshunttrains = ProblemInstance
                .Current.ArrivalsOrdered.Select(t => new ShuntTrain(
                    t.Units.Select(tu => shunttrainunits[tu.Index]),
                    t.InStanding
                ))
                .ToArray();

            GenerateMatching(
                random,
                shunttrainunits,
                out TrainMatching matching,
                out Dictionary<ShuntTrainUnit, Unit> reversematching
            );

            // Determine how to split
            Dictionary<ShuntTrain, List<ShuntTrain>> splitparts = [];
            List<ShuntTrain> partialshunttrains = [];
            foreach (ShuntTrain st in arrivalshunttrains)
            {
                var parts = st.Units.GroupBy(stu => reversematching[stu].Part);
                splitparts[st] = [];
                if (parts.Count() > 1)
                    foreach (var part in parts)
                    {
                        ShuntTrain partialtrain = new(part);
                        splitparts[st].Add(partialtrain);
                        partialshunttrains.Add(partialtrain);
                    }
                else
                {
                    splitparts[st].Add(st);
                    partialshunttrains.Add(st);
                }
                if (debugLevel > 1)
                    Console.WriteLine(
                        $"Split part shunt train {st} to {string.Join(",", splitparts[st].Select(un => un.ToString()))}"
                    );
            }
            // Add arrival and initial routing tasks
            List<ArrivalTask> arrivals = [];
            List<StandInTask> standins = [];
            List<RoutingTask> routings = [];
            BinaryHeap<MoveTask> moveheap = new(
                (a, b) =>
                {
                    int compare = a.Start.CompareTo(b.Start);
                    if (compare != 0)
                        return compare;
                    if (a.AllNext.Intersect(b.AllPrevious).Any())
                        // if there exists any c so that a -> c -> b
                        return -1;
                    if (b.AllNext.Intersect(a.AllPrevious).Any())
                        // if there exists any c so that b -> c -> a
                        return 1;

                    if (
                        a.AllPrevious.Any(task => task is ArrivalTask)
                        && b.AllPrevious.Any(task => task is ArrivalTask)
                    )
                        return 0;
                    if (a.AllPrevious.Any(task => task is ArrivalTask))
                        return -1;
                    if (b.AllPrevious.Any(task => task is ArrivalTask))
                        return 1;

                    if (
                        a.AllPrevious.Any(task => task is ServiceTask)
                        && b.AllPrevious.Any(task => task is ServiceTask)
                    )
                        return 0;
                    if (a.AllPrevious.Any(task => task is ServiceTask))
                        return -1;
                    if (b.AllPrevious.Any(task => task is ServiceTask))
                        return 1;
                    return 0;
                }
            );
            Dictionary<ShuntTrain, RoutingTask> previousTask = [];
            for (int i = 0; i < ProblemInstance.Current.ArrivalsOrdered.Length; i++)
            {
                ArrivalTrain train = ProblemInstance.Current.ArrivalsOrdered[i];
                ShuntTrain shunttrain = arrivalshunttrains[i];

                Time departuretime = shunttrain.Units.Max(u =>
                    reversematching[u].Train.Departure.Time
                );

                TrackTask firstTask;
                if (train.InStanding)
                {
                    // Instanding: already parked in the yard, so there is no arrival to
                    // plan. StandIn marks the chain head so that plan serialisation and
                    // the PlanGraph structure check can still recognise it as one.
                    var initialParking = new StandInTask(new ShuntTrain(shunttrain), train.Track);
                    initialParking.Start = initialParking.End = train.Time;
                    initialParking.ArrivalSide = train.Side;
                    standins.Add(initialParking);
                    firstTask = initialParking;
                }
                else
                {
                    var arrival = new ArrivalTask(shunttrain, train.Track, train.Side, train.Time);
                    arrivals.Add(arrival);
                    firstTask = arrival;
                }

                foreach (ShuntTrainUnit stu in shunttrain)
                    stu.Arrival = firstTask;

                // Add route from first task
                RoutingTask routing = new(new ShuntTrain(shunttrain));
                routing.Start = routing.End = train.Time;
                routing.Previous = firstTask;
                routing.FromTrack = firstTask.Track;
                firstTask.Next = routing;
                moveheap.Insert(routing);
                routings.Add(routing);
                if (debugLevel > 1)
                    Console.WriteLine(
                        $"Add routing task {routing} from {(train.InStanding ? "initial parking" : "arrival")} on track {firstTask.Track} at time {routing.Start}--{routing.End}"
                    );

                foreach (ShuntTrain part in splitparts[shunttrain])
                    previousTask[part] = routing;
            }
            // Add departure tasks
            List<DepartureTask> departures = [];
            List<StandOutTask> standouts = [];
            Dictionary<ShuntTrainUnit, DepartureRoutingTask> departuremapping = [];
            foreach (Matching.Train dt in matching.DepartureTrains)
            {
                var shunttrain = matching.GetShuntTrain(dt);

                TrackTask finalTask;
                if (dt.Departure.OutStanding)
                {
                    // Outstanding: stays in the yard, so there is no departure to plan.
                    // StandOut marks the chain tail; see StandInTask.
                    var finalParking = new StandOutTask(
                        new ShuntTrain(shunttrain),
                        dt.Departure.Track
                    );
                    finalParking.Start = finalParking.End = dt.Departure.Time;
                    finalParking.Deadline = dt.Departure.Time;
                    finalParking.ArrivalSide =
                        dt.Departure.Track.Access == Side.Both ? Side.A : dt.Departure.Track.Access;
                    dt.Task = finalParking;
                    finalTask = finalParking;
                    standouts.Add(finalParking);
                }
                else
                {
                    var departure = new DepartureTask(
                        shunttrain,
                        dt.Departure.Track,
                        dt.Departure.Side,
                        dt.Departure.Time
                    );
                    dt.Task = departure;
                    departures.Add(departure);
                    finalTask = departure;
                }

                // add route to final task
                var todeparture = new DepartureRoutingTask(
                    new ShuntTrain(shunttrain, shunttrain.InStanding)
                );
                todeparture.Start = todeparture.End = finalTask.Start;
                todeparture.Next = finalTask;
                finalTask.Previous = todeparture;

                finalTask.ArrivalSide =
                    finalTask.Track.Access == Side.Both ? Side.A : finalTask.Track.Access;
                todeparture.ToSide = finalTask.ArrivalSide;
                todeparture.ToTrack = finalTask.Track;
                dt.Routing = todeparture;

                foreach (ShuntTrainUnit unit in shunttrain)
                    departuremapping[unit] = todeparture;
            }
            // Jobshop
            // create the schedule data structure
            var schedule = new Dictionary<ServiceResource, LinkedList<ServiceTask>>();
            foreach (var type in ProblemInstance.Current.ServiceTypes)
            foreach (var resource in type.Resources)
            {
                schedule[resource] = new LinkedList<ServiceTask>();
            }

            List<ServiceTask> candidates = [];
            Dictionary<ShuntTrain, LinkedList<ServiceTask>> orderedservices = [];
            Dictionary<ShuntTrain, Time> earlieststart = [];

            // Add shunting trains
            foreach (ShuntTrain partial_shunttrain in partialshunttrains)
            {
                RoutingTask previousroute = previousTask[partial_shunttrain];
                DepartureRoutingTask departure = departuremapping[partial_shunttrain.Units.First()];

                Time departuretime = partial_shunttrain.Units.Max(u =>
                    reversematching[u].Train.Departure.Time
                );
                Time releasedate = previousroute.Start;
                Time duedate = departuretime - partial_shunttrain.ServiceDuration;
                ServiceTask? first = null;

                var trainservicetypes = partial_shunttrain
                    .Units.Aggregate(
                        Array.Empty<ServiceType>(),
                        (set, unit) =>
                            set.Concat(unit.RequiredServices.Select(s => s.Type)).ToArray()
                    )
                    .Distinct()
                    .ToList();
                // randomize
                Shuffle(trainservicetypes, random);
                foreach (ServiceType type in trainservicetypes)
                {
                    // ServiceTask initialized with <null> track (check full initialization at end of this method)
                    ServiceTask service = new(new ShuntTrain(partial_shunttrain), null!, type, null)
                    {
                        Start = releasedate,
                    };
                    releasedate += service.MinimumDuration;
                    duedate += service.MinimumDuration;
                    service.End = duedate;
                    if (first == null)
                    {
                        first = service;
                        orderedservices[partial_shunttrain] = new LinkedList<ServiceTask>();
                    }
                    orderedservices[partial_shunttrain].AddLast(service);

                    // Add route to service
                    var to = new RoutingTask(new ShuntTrain(partial_shunttrain));
                    to.Start = to.End = service.Start;
                    to.Next.Add(service);
                    service.Previous = to;
                    routings.Add(to);

                    // Add parking before service: initialized with parking at <null> track (check full initialization at end of this method)
                    var parking = new ParkingTask(new ShuntTrain(partial_shunttrain), null!);
                    parking.Previous = previousroute;
                    previousroute.Next.Add(parking);
                    parking.Next = to;
                    to.Previous = parking;

                    // Add route from service
                    var from = new RoutingTask(service.Train);
                    from.Start = from.End = service.End;
                    service.Next = from;
                    from.Previous = service;
                    routings.Add(from);

                    previousroute = from;
                }

                // Add first candidate
                if (first != null)
                    candidates.Add(first);

                departure.Start = departure.End = Math.Max(previousroute.End, departure.End);

                // Add parking before departure: initialized with parking at <null> track (check full initialization at end of this method)
                var departureparking = new ParkingTask(new ShuntTrain(partial_shunttrain), null!);
                departureparking.Previous = previousroute;
                previousroute.Next.Add(departureparking);
                departureparking.Next = departure;
                departure.Previous.Add(departureparking);
            }
            int comparison(ServiceTask a, ServiceTask b)
            {
                Time aStart = Math.Max(
                    a.Previous.Start,
                    schedule
                        .Where(kvp => a.Type.Resources.Contains(kvp.Key))
                        .Min(kvp => kvp.Value?.Last?.Value?.End ?? 0)
                );
                Time aEnd = aStart + a.MinimumDuration;
                Time bStart = Math.Max(
                    b.Previous.Start,
                    schedule
                        .Where(kvp => b.Type.Resources.Contains(kvp.Key))
                        .Min(kvp => kvp.Value?.Last?.Value?.End ?? 0)
                );
                Time bEnd = bStart + b.MinimumDuration;

                if (aEnd <= bStart)
                    return 1;
                if (bEnd <= aStart)
                    return -1;

                Time aDue = Math.Max(aEnd, a.End);
                Time bDue = Math.Max(bEnd, b.End);
                return bDue.CompareTo(aDue);
            }

            // Apply modified due date rule
            while (candidates.Count > 0)
            {
                // Sort the candidates
                candidates.Sort(comparison);

                // Select the best
                int selectedindex = candidates.Count - 1;
                var selected = candidates[selectedindex];
                while (
                    selectedindex > 0
                    && random.NextDouble() < 0.5
                    && comparison(candidates[selectedindex - 1], selected) == 0
                )
                {
                    selectedindex--;
                    selected = candidates[selectedindex];
                }
                ServiceResource resource = null!;
                Time t = int.MaxValue;

                // Find the machine with earliest completion time
                foreach (
                    var kvp in schedule.Where(kvp => selected.Type.Resources.Contains(kvp.Key))
                )
                    if ((kvp.Value?.Last?.Value?.End ?? 0) < t)
                    {
                        resource = kvp.Key;
                        t = kvp.Value?.Last?.Value?.End ?? 0;
                    }

                // Add it to the machine
                Debug.Assert(resource != null);
                var machineschedule = schedule[resource];
                selected.Start = Math.Max(t, selected.Previous.End);
                if (earlieststart.TryGetValue(selected.Train, out Time value))
                    selected.Start = Math.Max(selected.Start, value);
                selected.End = selected.Start + selected.MinimumDuration;
                var last = machineschedule.Last;
                if (last != null) // if the schedule is not empty
                {
                    last.Value.NextServiceTask = selected;
                    selected.PreviousServiceTask = last.Value;
                }
                machineschedule.AddLast(selected);

                // Display machineschedule
                selected.Resource = resource;
                if (resource.First == null)
                    resource.First = selected;
                resource.Last = selected;

                if (resource is ServiceLocation location)
                {
                    selected.Track = location.Track;
                    selected.ArrivalSide =
                        location.Track.Access == Side.Both ? Side.A : location.Track.Access;
                }

                foreach (var task in selected.Next.AllNext)
                    earlieststart[task.Train] = selected.End + Time.Minute;

                // Update route to service
                RoutingTask to = (RoutingTask)selected.Previous;
                to.Start = to.End = selected.Start;
                to.ToTrack = selected.Track;
                to.ToSide = selected.ArrivalSide;
                moveheap.Insert(to);

                // Update route from service
                RoutingTask from = (RoutingTask)selected.Next;
                from.Start = from.End = selected.End;
                from.FromTrack = selected.Track;
                moveheap.Insert(from);

                // Add new candidate if available
                orderedservices[selected.Train].RemoveFirst();
                if (orderedservices[selected.Train].Count > 0)
                    candidates[selectedindex] = orderedservices[selected.Train].First!.Value;
                else // Remove from candidate list
                    candidates.RemoveAt(selectedindex);
            }

            // Add routing task before departure/final-parking (from null track)
            foreach (Matching.Train dt in matching.DepartureTrains)
            {
                DepartureRoutingTask routing = dt.Routing;
                foreach (TrackTask previous in routing.Previous)
                    routing.Start = routing.End = Math.Max(routing.End, previous.Previous.End);
                moveheap.Insert(routing);
            }
            var routinggraph = Routing.RoutingGraph.Construct();
            PlanGraph graph = new(
                matching,
                routinggraph,
                shunttrainunits,
                arrivals.ToArray(),
                departures.ToArray(),
                standins.ToArray(),
                standouts.ToArray()
            );

            // Connect movetasks
            MoveTask? prev = null;
            int order = 1;
            while (!moveheap.IsEmpty)
            {
                MoveTask current = moveheap.ExtractFirst();
                if (debugLevel > 1)
                    Console.WriteLine(
                        $"<Intial> Add {order}th move task {current} at time {current.Start}--{current.End}"
                    );

                current.MoveOrder = order++;
                current.PreviousMove = prev;
                current.Graph = graph;
                if (prev != null)
                    prev.NextMove = current;
                else
                    graph.First = current;
                prev = current;
            }
            Debug.Assert(prev != null);
            graph.Last = prev;

            routings.Sort((a, b) => a.MoveOrder.CompareTo(b.MoveOrder));

            // Add Parking
            Dictionary<Track, int> space = [];
            foreach (Track track in ProblemInstance.Current.Tracks)
                space[track] = track.Length;
            // Assign initial parking track before departure and add routing
            foreach (RoutingTask routing in routings)
            {
                // Depart
                if (routing.Previous is not ArrivalTask)
                    space[routing.Previous.Track] += routing.Previous.Train.Length;

                if (
                    !routing.Next.All(task =>
                        task is ParkingTask
                        || task is ServiceTask { Type.LocationType: ServiceLocationType.Free }
                    )
                )
                    continue;

                Track parkingtrack;
                if (
                    routing.Next.Count == 1
                    && routing.Next[0].Next.TaskType == MoveTaskType.Departure
                    && routing.Next[0].Next.AllPrevious.Any(task => task.Track != null)
                )
                {
                    parkingtrack = routing
                        .Next[0]
                        .Next.AllPrevious.First(task => task.Track != null)
                        .Track;
                }
                else
                {
                    // Find track with maximal space
                    var reachable = routing
                        .Train.ParkingLocations.Where(track =>
                            (
                                routinggraph.RoutePossible(
                                    routing.Train,
                                    routing.Previous.Track,
                                    Side.A,
                                    track,
                                    Side.A
                                )
                                || routinggraph.RoutePossible(
                                    routing.Train,
                                    routing.Previous.Track,
                                    Side.B,
                                    track,
                                    Side.A
                                )
                                || routinggraph.RoutePossible(
                                    routing.Train,
                                    routing.Previous.Track,
                                    Side.A,
                                    track,
                                    Side.B
                                )
                                || routinggraph.RoutePossible(
                                    routing.Train,
                                    routing.Previous.Track,
                                    Side.B,
                                    track,
                                    Side.B
                                )
                            )
                            && routing.AllNext.All(task =>
                                task.Next.ToTrack == null
                                || routinggraph.RoutePossible(
                                    task.Train,
                                    track,
                                    Side.A,
                                    task.Next.ToTrack,
                                    Side.A
                                )
                                || routinggraph.RoutePossible(
                                    task.Train,
                                    track,
                                    Side.B,
                                    task.Next.ToTrack,
                                    Side.A
                                )
                                || routinggraph.RoutePossible(
                                    task.Train,
                                    track,
                                    Side.A,
                                    task.Next.ToTrack,
                                    Side.B
                                )
                                || routinggraph.RoutePossible(
                                    task.Train,
                                    track,
                                    Side.B,
                                    task.Next.ToTrack,
                                    Side.B
                                )
                            )
                        )
                        .ToArray();
                    if (reachable.Length == 0)
                        throw new InvalidOperationException(
                            $"No routing possible for train {routing.Train} of length {routing.Train.Length} from previous track <{routing.Previous.Track}> with sufficient length and possible route."
                        );
                    var possibletracks = reachable.Where(track =>
                        space[track] >= routing.Train.Length
                    );
                    if (!possibletracks.Any())
                        possibletracks = reachable;
                    var candidatetracks = possibletracks.OrderBy(track => -space[track]).ToList();
                    int trackindex = 0;
                    while (random.NextDouble() < 0.5 && trackindex < candidatetracks.Count - 1)
                        trackindex++;
                    parkingtrack = candidatetracks[trackindex];
                }

                Side side = Side.None;
                if (parkingtrack.Access == Side.Both)
                {
                    if (
                        routinggraph.RoutePossible(
                            routing.Train,
                            routing.FromTrack,
                            Side.A,
                            parkingtrack,
                            Side.A
                        )
                    )
                        side = Side.A;
                    else if (
                        routinggraph.RoutePossible(
                            routing.Train,
                            routing.FromTrack,
                            Side.B,
                            parkingtrack,
                            Side.A
                        )
                    )
                        side = Side.A;
                    else
                        side = Side.B;
                }
                else
                    side = parkingtrack.Access;

                // Park
                routing.ToTrack = parkingtrack;
                routing.ToSide = side;
                foreach (TrackTask task in routing.Next)
                {
                    task.Track = parkingtrack;
                    task.Next.FromTrack = parkingtrack;
                    task.ArrivalSide = side;
                }
                space[parkingtrack] -= routing.Train.Length;

                foreach (TrackTask task in routing.Next)
                {
                    if (task.Next is DepartureRoutingTask departure)
                    {
                        side = Side.None;
                        if (departure.Next.Track.Access == Side.Both)
                        {
                            if (
                                routinggraph.RoutePossible(
                                    task.Train,
                                    parkingtrack,
                                    Side.A,
                                    departure.Next.Track,
                                    Side.A
                                )
                                || routinggraph.RoutePossible(
                                    task.Train,
                                    parkingtrack,
                                    Side.B,
                                    departure.Next.Track,
                                    Side.A
                                )
                            )
                                side = Side.A;
                            else
                                side = Side.B;
                        }
                        else
                            side = departure.Next.Track.Access;

                        departure.ToSide = departure.Next.ArrivalSide = side;
                    }
                }
                if (debugLevel > 1)
                    Console.WriteLine(
                        $"Set parking track on routing {routing} to parking track {parkingtrack}"
                    );
            }
            Debug.Assert(graph.IsWellFormed());
            return graph;

            static void GenerateMatching(
                Random random,
                ShuntTrainUnit[] shunttrainunits,
                out TrainMatching matching,
                out Dictionary<ShuntTrainUnit, Unit> reversematching
            )
            {
                Matching.BipartiteGraph g = new();
                var initialmatching = g.MaximumMatching();

                // Matching
                matching = g.LocalSearch(initialmatching, random, shunttrainunits);
                reversematching = [];
                foreach (Matching.Train dt in matching.DepartureTrains)
                foreach (Matching.Unit du in dt)
                    reversematching[shunttrainunits[du.Index]] = du;
            }
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                int index = random.Next(i);
                T temp = list[index];
                list[index] = list[i];
                list[i] = temp;
            }
        }
    }
}
