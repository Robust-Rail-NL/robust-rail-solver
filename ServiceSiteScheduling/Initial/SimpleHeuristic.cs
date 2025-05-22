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
        public static PlanGraph Construct(Random random)
        {
            ShuntTrainUnit[] shunttrainunits = ProblemInstance.Current.TrainUnits.Select(tu => new ShuntTrainUnit(tu)).ToArray();
            ShuntTrain[] arrivalshunttrains = ProblemInstance.Current.ArrivalsOrdered.Select(t => new ShuntTrain(t.Units.Select(tu => shunttrainunits[tu.Index]), t.IsItInStanding())).ToArray();

            Matching.BipartiteGraph g = new Matching.BipartiteGraph();
            var initialmatching = g.MaximumMatching();

            // Matching
            Matching.TrainMatching matching = g.LocalSearch(initialmatching, random, shunttrainunits);
            Dictionary<ShuntTrainUnit, Matching.Unit> reversematching = new Dictionary<ShuntTrainUnit, Matching.Unit>();
            foreach (Matching.Train dt in matching.DepartureTrains)
                foreach (Matching.Unit du in dt)
                    reversematching[shunttrainunits[du.Index]] = du;

            // Determine how to split
            Dictionary<ShuntTrain, List<ShuntTrain>> splitparts = new Dictionary<ShuntTrain, List<ShuntTrain>>();
            List<ShuntTrain> serviceshunttrains = new List<ShuntTrain>();
            foreach (ShuntTrain st in arrivalshunttrains)
            {
                var parts = st.Units.GroupBy(stu => reversematching[stu].Part);
                splitparts[st] = new List<ShuntTrain>();
                if (parts.Count() > 1)
                    foreach (var part in parts)
                    {
                        ShuntTrain partialtrain = new ShuntTrain(part);
                        splitparts[st].Add(partialtrain);
                        serviceshunttrains.Add(partialtrain);
                    }
                else
                {
                    splitparts[st].Add(st);
                    serviceshunttrains.Add(st);
                }
            }

            // Add arrival and inital routing tasks
            List<ArrivalTask> arrivals = new List<ArrivalTask>();
            List<RoutingTask> routings = new List<RoutingTask>();
            BinaryHeap<MoveTask> moveheap = new BinaryHeap<MoveTask>((a, b) =>
            {
                int compare = a.Start.CompareTo(b.Start);
                if (compare == 0)
                {
                    if (a.AllNext.Intersect(b.AllPrevious).Count() > 0)
                        return -1;
                    if (b.AllNext.Intersect(a.AllPrevious).Count() > 0)
                        return 1;

                    if (a.AllPrevious.Any(task => task is ArrivalTask) && b.AllPrevious.Any(task => task is ArrivalTask))
                        return 0;
                    if (a.AllPrevious.Any(task => task is ArrivalTask))
                        return -1;
                    if (b.AllPrevious.Any(task => task is ArrivalTask))
                        return 1;

                    if (a.AllPrevious.Any(task => task is ServiceTask) && b.AllPrevious.Any(task => task is ServiceTask))
                        return 0;
                    if (a.AllPrevious.Any(task => task is ServiceTask))
                        return -1;
                    if (b.AllPrevious.Any(task => task is ServiceTask))
                        return 1;
                    return 0;
                }
                return compare;
            });
            Dictionary<ShuntTrain, RoutingTask> previousTask = new Dictionary<ShuntTrain, RoutingTask>();
            for (int i = 0; i < ProblemInstance.Current.ArrivalsOrdered.Length; i++)
            {
                ArrivalTrain train = ProblemInstance.Current.ArrivalsOrdered[i];
                ShuntTrain shunttrain = arrivalshunttrains[i];

                Time departuretime = shunttrain.Units.Max(u => reversematching[u].Train.Departure.Time);
                // due date including service time
                Time due = departuretime - shunttrain.ServiceDuration;

                // add arrival operation
                ArrivalTask arrival = new ArrivalTask(shunttrain, train.Track, train.Side, train.Time);
                arrivals.Add(arrival);
                foreach (ShuntTrainUnit stu in shunttrain)
                    stu.Arrival = arrival;

                // Add route from arrival
                RoutingTask routing = new RoutingTask(new ShuntTrain(shunttrain));
                routing.Start = routing.End = arrival.ScheduledTime;
                routing.Previous = arrival;
                routing.FromTrack = arrival.Track;
                arrival.Next = routing;
                moveheap.Insert(routing);
                routings.Add(routing);

                foreach (ShuntTrain part in splitparts[shunttrain])
                    previousTask[part] = routing;
            }

            // Add departure tasks
            List<DepartureTask> departures = new List<DepartureTask>();
            Dictionary<ShuntTrainUnit, DepartureRoutingTask> departuremapping = new Dictionary<ShuntTrainUnit, DepartureRoutingTask>();

            foreach (Matching.Train dt in matching.DepartureTrains)
            {

                var shunttrain = matching.GetShuntTrain(dt);
                DepartureTask departure = new DepartureTask(shunttrain, dt.Departure.Track, dt.Departure.Side, dt.Departure.Time);
                dt.Task = departure;
                departures.Add(departure);

                // add route to departure
                var todeparture = new DepartureRoutingTask(new ShuntTrain(shunttrain, shunttrain.InStanding));
                todeparture.Start = todeparture.End = departure.Start;
                todeparture.Next = departure;
                departure.Previous = todeparture;

                departure.ArrivalSide = departure.ArrivalSide = departure.Track.Access == Side.Both ? Side.A : departure.Track.Access;
                todeparture.ToSide = departure.ArrivalSide;
                todeparture.ToTrack = departure.Track;
                dt.Routing = todeparture;

                foreach (ShuntTrainUnit unit in shunttrain)
                    departuremapping[unit] = todeparture;
            }

            // Jobshop
            // create the schedule data structure
            var schedule = new Dictionary<ServiceResource, LinkedList<ServiceTask>>();
            foreach (var type in ProblemInstance.Current.ServiceTypes)
                foreach (var resource in type.Resources)
                    schedule[resource] = new LinkedList<ServiceTask>();
                                
            List<ServiceTask> services = new List<ServiceTask>();
            List<ServiceTask> candidates = new List<ServiceTask>();
            List<ParkingTask> parkings = new List<ParkingTask>();
            Dictionary<ShuntTrain, LinkedList<ServiceTask>> orderedservices = new Dictionary<ShuntTrain, LinkedList<ServiceTask>>();
            Dictionary<ShuntTrain, Time> earlieststart = new Dictionary<ShuntTrain, Time>();

            // Add service tasks
            foreach (ShuntTrain shunttrain in serviceshunttrains)
            {
                RoutingTask previousroute = previousTask[shunttrain];
                DepartureRoutingTask departure = departuremapping[shunttrain.Units.First()];

                Time departuretime = shunttrain.Units.Max(u => reversematching[u].Train.Departure.Time);
                Time releasedate = previousroute.Start;
                Time duedate = departuretime - shunttrain.ServiceDuration;
                ServiceTask service = null, first = null;

                var trainservicetypes = shunttrain.Units.Aggregate(new ServiceType[0], (set, unit) => set.Concat(unit.RequiredServices.Select(s => s.Type)).ToArray()).Distinct().ToList();
                // randomize
                Shuffle(trainservicetypes, random);
                foreach (ServiceType type in trainservicetypes)
                {
                    service = new ServiceTask(new ShuntTrain(shunttrain), null, type, null);
                    service.Start = releasedate;
                    releasedate += service.MinimumDuration;
                    duedate += service.MinimumDuration;
                    service.End = duedate;

                    if (first == null)
                    {
                        first = service;
                        orderedservices[shunttrain] = new LinkedList<ServiceTask>();
                    }

                    services.Add(service);
                    orderedservices[shunttrain].AddLast(service);

                    // add route to service
                    var to = new RoutingTask(new ShuntTrain(shunttrain));
                    to.Start = to.End = service.Start;
                    to.Next.Add(service);
                    service.Previous = to;
                    routings.Add(to);

                    // add parking before service
                    var parking = new ParkingTask(new ShuntTrain(shunttrain), null);
                    parking.Previous = previousroute;
                    previousroute.Next.Add(parking);
                    parking.Next = to;
                    to.Previous = parking;
                    parkings.Add(parking);

                    // add route from service
                    var from = new RoutingTask(service.Train);
                    from.Start = from.End = service.End;
                    service.Next = from;
                    from.Previous = service;
                    routings.Add(from);

                    previousroute = from;
                }

                // add first candidate
                if (first != null)
                    candidates.Add(first);

                departure.Start = departure.End = Math.Max(previousroute.End, departure.End);

                // add parking before departure
                var departureparking = new ParkingTask(new ShuntTrain(shunttrain), null);
                departureparking.Previous = previousroute;
                previousroute.Next.Add(departureparking);
                departureparking.Next = departure;
                departure.Previous.Add(departureparking);
                parkings.Add(departureparking);
            }

            Comparison<ServiceTask> comparison = (a, b) =>
            {
                Time aStart = Math.Max(a.Previous.Start, schedule.Where(kvp => a.Type.Resources.Contains(kvp.Key)).Select(kvp => kvp.Value?.Last?.Value?.End ?? 0).Min());
                Time aEnd = aStart + a.MinimumDuration;
                Time bStart = Math.Max(b.Previous.Start, schedule.Where(kvp => b.Type.Resources.Contains(kvp.Key)).Select(kvp => kvp.Value?.Last?.Value?.End ?? 0).Min());
                Time bEnd = bStart + b.MinimumDuration;

                if (aEnd <= bStart)
                    return 1;
                if (bEnd <= aStart)
                    return -1;

                Time aDue = Math.Max(aEnd, a.End);
                Time bDue = Math.Max(bEnd, b.End);

                return bDue.CompareTo(aDue);
            };

            // apply modified due date rule
            while (candidates.Count > 0)
            {
                // sort the candidates
                candidates.Sort(comparison);

                // selected the best
                int selectedindex = candidates.Count - 1;
                var selected = candidates[selectedindex];
                while (selectedindex > 0 && random.NextDouble() < 0.5 && comparison(candidates[selectedindex - 1], selected) == 0)
                {
                    selectedindex--;
                    selected = candidates[selectedindex];
                }


                ServiceResource resource = null;
                Time t = int.MaxValue;
                // find the machine with earliest completion time
                foreach (var kvp in schedule.Where(kvp => selected.Type.Resources.Contains(kvp.Key)))
                    if ((kvp.Value?.Last?.Value?.End ?? 0) < t)
                    {
                        resource = kvp.Key;
                        t = kvp.Value?.Last?.Value?.End ?? 0;
                    }

                // add it to the machine
                var machineschedule = schedule[resource];
                selected.Start = Math.Max(t, selected.Previous.End);
                if (earlieststart.ContainsKey(selected.Train))
                    selected.Start = Math.Max(selected.Start, earlieststart[selected.Train]);
                selected.End = selected.Start + selected.MinimumDuration;
                if (machineschedule.Count > 0)
                {
                    machineschedule.Last.Value.NextServiceTask = selected;
                    selected.PreviousServiceTask = machineschedule.Last.Value;
                }
                machineschedule.AddLast(selected);

                // Display machineschedule
                selected.Resource = resource;
                if (resource.First == null)
                    resource.First = selected;
                resource.Last = selected;

                var location = resource as ServiceLocation;
                if (location != null)
                {
                    selected.Track = location.Track;
                    selected.ArrivalSide = location.Track.Access == Side.Both ? Side.A : location.Track.Access;
                }

                foreach (var task in selected.Next.AllNext)
                    earlieststart[task.Train] = selected.End + Time.Minute;


                // update route to service
                RoutingTask to = selected.Previous as RoutingTask;
                to.Start = to.End = selected.Start;
                to.ToTrack = selected.Track;
                to.ToSide = selected.ArrivalSide;
                moveheap.Insert(to);

                // update route from service
                RoutingTask from = selected.Next as RoutingTask;
                from.Start = from.End = selected.End;
                from.FromTrack = selected.Track;
                moveheap.Insert(from);

                // add new candidate if available
                orderedservices[selected.Train].RemoveFirst();
                if (orderedservices[selected.Train].Count > 0)
                    candidates[selectedindex] = orderedservices[selected.Train].First.Value;
                else // Remove from candidate list
                    candidates.RemoveAt(selectedindex);
            }

            // Update departure routes
            foreach (DepartureTask departure in departures)
            {
                var routing = departure.Previous as DepartureRoutingTask;
                foreach (TrackTask previous in routing.Previous)
                    routing.Start = routing.End = Math.Max(routing.End, previous.Previous.End);
                moveheap.Insert(routing);
            }

            var routinggraph = Routing.RoutingGraph.Construct();

            PlanGraph graph = new PlanGraph(matching, routinggraph, shunttrainunits, arrivals.ToArray(), departures.ToArray());


            // Connect movetasks
            MoveTask prev = null;
            int order = 1;
            while (!moveheap.IsEmpty)
            {
                MoveTask current = moveheap.ExtractFirst();
                
                current.MoveOrder = order++;
                current.PreviousMove = prev;
                current.Graph = graph;
                if (prev != null)
                    prev.NextMove = current;
                else
                    graph.First = current;
                prev = current;
            }
            graph.Last = prev;

            routings.Sort((a, b) => a.MoveOrder.CompareTo(b.MoveOrder));

            // Add Parking
            Dictionary<Track, int> space = new Dictionary<Track, int>();
            foreach (Track track in ProblemInstance.Current.Tracks)
                space[track] = track.Length;

            foreach (RoutingTask routing in routings)
            {
                // Depart
                if (!(routing.Previous is ArrivalTask))
                    space[routing.Previous.Track] += routing.Previous.Train.Length;

                if (!routing.Next.All(task => task is ParkingTask || (task as ServiceTask)?.Type.LocationType == ServiceLocationType.Free))
                    continue;

                Track parkingtrack = null;
                if (routing.Next.Count == 1 && routing.Next[0].Next.TaskType == MoveTaskType.Departure && routing.Next[0].Next.AllPrevious.Any(task => task.Track != null))
                {
                    parkingtrack = routing.Next[0].Next.AllPrevious.Where(task => task.Track != null).First().Track;
                }
                else
                {
                    // Find track with maximal space
                    var reachable = routing.Train.ParkingLocations.Where(track =>
                        (routinggraph.RoutePossible(routing.Train, routing.Previous.Track, Side.A, track, Side.A) ||
                        routinggraph.RoutePossible(routing.Train, routing.Previous.Track, Side.B, track, Side.A) ||
                        routinggraph.RoutePossible(routing.Train, routing.Previous.Track, Side.A, track, Side.B) ||
                        routinggraph.RoutePossible(routing.Train, routing.Previous.Track, Side.B, track, Side.B)) &&
                        routing.AllNext.All(task =>
                            task.Next.ToTrack == null ||
                            routinggraph.RoutePossible(task.Train, track, Side.A, task.Next.ToTrack, Side.A) ||
                            routinggraph.RoutePossible(task.Train, track, Side.B, task.Next.ToTrack, Side.A) ||
                            routinggraph.RoutePossible(task.Train, track, Side.A, task.Next.ToTrack, Side.B) ||
                            routinggraph.RoutePossible(task.Train, track, Side.B, task.Next.ToTrack, Side.B)
                        )).ToArray();
                    var possibletracks = reachable.Where(track => space[track] >= routing.Train.Length);
                    if (possibletracks.Count() == 0)
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
                    if (routinggraph.RoutePossible(routing.Train, routing.FromTrack, Side.A, parkingtrack, Side.A))
                        side = Side.A;
                    else if (routinggraph.RoutePossible(routing.Train, routing.FromTrack, Side.B, parkingtrack, Side.A))
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
                    var next = task.Next as RoutingTask;
                    if (next != null)
                        next.FromTrack = parkingtrack;
                    task.ArrivalSide = side;
                }
                space[parkingtrack] -= routing.Train.Length;
                
                foreach (TrackTask task in routing.Next)
                {
                    var departure = task.Next as DepartureRoutingTask;
                    if (departure != null)
                    {
                        side = Side.None;
                        if (departure.Next.Track.Access == Side.Both)
                        {
                            if (routinggraph.RoutePossible(task.Train, parkingtrack, Side.A, departure.Next.Track, Side.A) || routinggraph.RoutePossible(task.Train, parkingtrack, Side.B, departure.Next.Track, Side.A))
                                side = Side.A;
                            else
                                side = Side.B;
                        }
                        else
                            side = departure.Next.Track.Access;

                        departure.ToSide = departure.Next.ArrivalSide = side;
                    }
                }
            }

            return graph;
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
