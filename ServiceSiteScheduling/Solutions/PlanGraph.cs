#nullable enable

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ServiceSiteScheduling.Interchange;
using ServiceSiteScheduling.Matching;
using ServiceSiteScheduling.Parking;
using ServiceSiteScheduling.Routing;
using ServiceSiteScheduling.Tasks;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using static ServiceSiteScheduling.Interchange.PredefinedTaskType;

namespace ServiceSiteScheduling.Solutions
{
    class PlanGraph
    {
        static readonly ILogger logger = Logging.GetLogger();

        public ShuntTrainUnit[] ShuntUnits { get; private set; }

        ImmutableArray<TrackOccupation> TrackOccupations { get; init; }
        bool[] outsidetrack;

        public RoutingGraph RoutingGraph;

        public ImmutableArray<ArrivalTask> ArrivalTasks { get; init; }
        public ImmutableArray<DepartureTask> DepartureTasks { get; init; }

        /// <summary>
        /// Chain heads of inStanding trains. These are not ArrivalTasks — an
        /// inStanding train is already parked when the scenario starts — but they
        /// play the same structural role, so any traversal of the task graph must
        /// seed from both this and <see cref="ArrivalTasks"/>.
        /// </summary>
        public ImmutableArray<StandInTask> StandInTasks { get; init; }

        public TrainMatching Matching { get; private set; }

        public ArrivalTask? FirstArrival
        {
            get
            {
                return this.ArrivalTasks.FirstOrDefault(arrival =>
                    arrival.Next.PreviousMove == null
                );
            }
        }

        public MoveTask First { get; set; } = null!;
        public MoveTask Last { get; set; } = null!;

        public SolutionCost? Cost;

        // Populated by ComputeLocation whenever a split-during-a-move can't
        // determine which unit actually leads (Units0Side gives up) and has
        // to fall back to the unproven ToSide-only heuristic. The one
        // confirmed trigger is a split immediately following an earlier
        // split of the same train -- a multi-way split, or a later split of
        // one of an earlier split's parts -- since Units0Side only walks
        // back through ordinary single-destination moves; it does not
        // reconstruct position within a multi-way split's several
        // simultaneously-placed parts. A combine cannot precede a split in
        // the current model (a DepartureRoutingTask's Next is always a
        // DepartureTask or StandOutTask, never a further stop), but the
        // guard treats any other unrecognised history the same way, on the
        // assumption that an unhandled case is more likely than a proof it
        // can never occur. Reset on every ComputeModel pass, so this only ever
        // reflects the graph as most recently computed, not stale entries
        // from a discarded candidate. Checked only when writing the actual
        // delivered plan (see WriteJSONFile's validateFinal) -- not here,
        // since ComputeLocation runs on every candidate local search
        // constructs and discards, not just the one that ships.
        private readonly List<string> unverifiedSplitPlacements = [];

        public IReadOnlyList<string> UnverifiedSplitPlacements => this.unverifiedSplitPlacements;

        private bool[][] FreeServiceTaskFinished;

        public PartialOrderSchedule? POS { get; set; }

        public int testIndex { get; set; }

        public PlanGraph(
            TrainMatching matching,
            RoutingGraph graph,
            ShuntTrainUnit[] shuntunits,
            ArrivalTask[] arrivals,
            DepartureTask[] departures,
            StandInTask[] standins
        )
        {
            this.RoutingGraph = graph;
            this.Matching = matching;
            this.ShuntUnits = shuntunits;
            this.ArrivalTasks = ImmutableArray.ToImmutableArray(arrivals);
            this.DepartureTasks = ImmutableArray.ToImmutableArray(departures);
            this.StandInTasks = ImmutableArray.ToImmutableArray(standins);

            TrackOccupation[] occupations = new TrackOccupation[
                ProblemInstance.Current.Tracks.Length
            ];
            this.outsidetrack = new bool[ProblemInstance.Current.Tracks.Length];
            for (int i = 0; i < occupations.Length; i++)
            {
                var track = ProblemInstance.Current.Tracks[i];
                if (!track.IsActive)
                    continue;

                TrackOccupation occupation = new SimpleTrackOccupation(track);
                occupations[i] = occupation;
                this.RoutingGraph.SuperVertices[track.Index].TrackOccupation = occupation;

                if (
                    ProblemInstance
                        .Current.ArrivalsOrdered.Where(t => !t.InStanding)
                        .Select(t => t.Track)
                        .Contains(track)
                )
                    outsidetrack[i] = true;
                if (
                    ProblemInstance
                        .Current.DeparturesOrdered.Where(t => !t.OutStanding)
                        .Select(t => t.Track)
                        .Contains(track)
                )
                    outsidetrack[i] = true;
            }
            this.FreeServiceTaskFinished = new bool[ProblemInstance.Current.TrainUnits.Length][];
            for (int i = 0; i < ProblemInstance.Current.TrainUnits.Length; i++)
                this.FreeServiceTaskFinished[i] = new bool[
                    ProblemInstance.Current.FreeServices[i].Length
                ];
            this.testIndex = 0;
            this.TrackOccupations = ImmutableArray.ToImmutableArray(occupations);
        }

        public void GetShortPlanStatistics()
        {
            int number_moves = 0;
            MoveTask? count_move = this.First;
            while (count_move != null)
            {
                number_moves++;
                count_move = count_move.NextMove;
            }
            Console.WriteLine($"Number of Shunt Units: {this.ShuntUnits.Length}");
            if (this.FirstArrival != null)
                Console.WriteLine(
                    $"PlanGraph starting with arrival at track {this.FirstArrival.Track.PrettyName}"
                );
            Console.WriteLine(
                $"Move Tasks: {number_moves}, Arrival Tasks: {this.ArrivalTasks.Length}, Departure Tasks: {this.DepartureTasks.Length}"
            );
        }

        public void UpdateRoutingOrder()
        {
            MoveTask? move = this.First;
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
                this.TrackOccupations[i]?.Reset();
            this.unverifiedSplitPlacements.Clear();

            this.ComputeLocation(this.First, recomputestart, recomputeend);
            ComputeTime(recomputestart, recomputestart?.PreviousMove?.End ?? 0);
            return this.ComputeCost();
        }

        public SolutionCost ComputeModel()
        {
            foreach (var departure in this.DepartureTasks)
                departure.GetDepartureRoutingTask().UpdatePreviousTaskOrder();

            return this.ComputeModel(this.First, this.Last);
        }

        public void ComputeLocation(MoveTask? start, MoveTask recomputestart, MoveTask recomputeend)
        {
            MoveTask? move = start;
            while (move != null)
            {
                if (move.TaskType == MoveTaskType.Standard)
                {
                    var routing = (RoutingTask)move;

                    // Arrive previously if necessary
                    if (routing.Previous.TaskType is TrackTaskType.Arrival or TrackTaskType.StandIn)
                        routing.Previous.Arrive(
                            this.TrackOccupations[routing.Previous.Track.Index]
                        );

                    // Departure crossings
                    int departurecrossingsA = 0,
                        departurecrossingsB = 0;

                    // Depart from the previous track. A routing that ends on the
                    // track it started from moves nothing, so the train neither
                    // leaves the occupation nor crosses anything to get out.
                    bool staysOnTrack = routing.FromTrack == routing.ToTrack;

                    if (!staysOnTrack)
                    {
                        if (routing.FromTrack.Access.HasFlag(Side.A))
                            departurecrossingsA = routing.Previous.State.GetCrossings(Side.A);
                        if (routing.FromTrack.Access.HasFlag(Side.B))
                            departurecrossingsB = routing.Previous.State.GetCrossings(Side.B);

                        routing.Previous.Depart(this.TrackOccupations[routing.FromTrack.Index]);
                    }

                    if (
                        move.MoveOrder >= recomputestart.MoveOrder
                        && move.MoveOrder <= recomputeend.MoveOrder
                    )
                        this.ComputeRouting(routing, departurecrossingsA, departurecrossingsB);

                    if (staysOnTrack)
                    {
                        // The train stays exactly where it is. With one Next task
                        // that task simply takes over the same State, keeping the
                        // train's place in the occupation.
                        //
                        // A split ends with several tasks, and one State cannot
                        // stand for all of them: each would later remove the very
                        // same deque node, and the second removal would find the
                        // node already gone (#11). The parts instead take over the
                        // stretch the whole train held, keeping its place and order
                        // -- which is what decoupling a train where it stands does.
                        if (routing.IsSplit)
                            this.TrackOccupations[routing.ToTrack.Index]
                                .SplitInPlace(routing.Previous, routing.Next);
                        else
                            foreach (TrackTask to in routing.Next)
                                to.Replace(routing.Previous);
                    }
                    else if (routing.IsSplit)
                    {
                        // Deque.Add() puts whichever child is Arrive()-d last
                        // nearest the arrival side (ToSide), so the loop
                        // direction decides which physical end unit 0 lands
                        // on -- it has to match which unit actually led the
                        // train in.
                        bool? leads = UnitZeroLeads(Units0Side(routing.Previous), routing);
                        bool reversed;
                        if (leads.HasValue)
                        {
                            reversed = !leads.Value;
                        }
                        else
                        {
                            // Units0Side gave up: the history includes
                            // something it isn't taught to reason about yet
                            // (see unverifiedSplitPlacements). Fall back to
                            // the old ToSide-only heuristic so this
                            // candidate can still be built and costed during
                            // search, but flag it as unverified -- it must
                            // not ship as the delivered plan. See
                            // WriteJSONFile's validateFinal.
                            reversed = routing.ToSide == Side.A;
                            this.unverifiedSplitPlacements.Add(
                                $"split of {routing.Train} onto track {routing.ToTrack} at {routing.Start}"
                            );
                        }

                        if (reversed)
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
                    else
                    {
                        // A plain move has exactly one Next task, so there is
                        // nothing to order between children: it always ends
                        // up as the sole occupant of whichever side it
                        // arrives through.
                        var to = routing.Next[0];
                        to.Arrive(this.TrackOccupations[to.Track.Index]);
                        if (to is DepartureTask)
                            to.Depart(this.TrackOccupations[to.Track.Index]);
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
        }

        /// <summary>
        /// Whether the train's first (index-0) unit is the one leading as
        /// <paramref name="routing"/> arrives at its destination, given which
        /// side of the origin track it occupied beforehand
        /// (<paramref name="fromTrackSide"/>, from <see cref="Units0Side"/>).
        /// A route with an even number of reversals preserves whichever unit
        /// was already nearest the departure side; an odd number flips it.
        /// Null (unknown) propagates from a missing route or side.
        /// </summary>
        private static bool? UnitZeroLeads(Side? fromTrackSide, RoutingTask routing)
        {
            if (
                fromTrackSide == null
                || routing.Route == null
                || routing.FromSide == null
                || routing.ToSide == null
            )
                return null;

            return (fromTrackSide == routing.FromSide) ^ (routing.Route.TotalReversals % 2 == 1);
        }

        /// <summary>
        /// Which side of <paramref name="task"/>'s track the train's first
        /// (index-0) unit currently occupies, found by walking back through
        /// its arrival/movement history. At the very first arrival (or
        /// stand-in), unit 0 is taken to be the one that led the train in, so
        /// it drives deepest into the track and ends up on the side opposite
        /// the one the train entered through. Each later plain move (not a
        /// split) either preserves or flips that, via
        /// <see cref="UnitZeroLeads"/>. Returns null when the history isn't
        /// one this can reason about yet -- most notably when this move's
        /// own previous track was itself reached via an earlier split
        /// (<c>routing.Next.Count != 1</c>), since position within a
        /// multi-way split's several simultaneously-placed parts isn't
        /// reconstructed here -- so the caller can fall back.
        /// </summary>
        private static Side? Units0Side(TrackTask task)
        {
            if (task.Previous == null)
                return task.ArrivalSide?.Flip;

            if (task.Previous is not RoutingTask routing || routing.Next.Count != 1)
                return null;

            bool? leads = UnitZeroLeads(Units0Side(routing.Previous), routing);
            if (leads == null)
                return null;

            return leads.Value ? routing.ToSide!.Flip : routing.ToSide;
        }

        public static void ComputeTime(MoveTask? start, Time time)
        {
            MoveTask? move = start;
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
                        if (routing.Start < time && !arrival.Track.CanPark)
                        {
                            logger.LogDebug(
                                ""
                                    + "Forced shuntingunit {routing.Train} to wait after arriving at {routing.Start}, "
                                    + "because previous routing task {routing.Previous} ends at time {time}, but arrival "
                                    + "track {arrival.Track} cannot be used for parking.",
                                routing.Train,
                                routing.Start,
                                routing.Previous,
                                time,
                                arrival.Track
                            );
                            //throw new InvalidOperationException(txt);
                        }
                    }
                    else if (routing.Previous.TaskType == TrackTaskType.Service)
                        routing.Start =
                            routing.Previous.Start
                            + ((ServiceTask)routing.Previous).MinimumDuration;
                    else if (routing.Previous.TaskType == TrackTaskType.StandIn)
                        routing.Start = routing.Previous.Start;
                    else
                        routing.Start = time;

                    routing.Start = Math.Max(routing.Start, time);

                    if (
                        routing.Previous.Previous?.ToSide == routing.FromSide
                        && routing.Start
                            < routing.Previous.Previous.End + routing.Train.ReversalDuration
                    )
                        routing.Start =
                            routing.Previous.Previous.End + routing.Train.ReversalDuration;

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
                                service.Start = Math.Max(
                                    routing.End,
                                    service.PreviousServiceTask.Start
                                        + service.PreviousServiceTask.MinimumDuration
                                );
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
                    Time reversalduration;
                    if (departurerouting.Next is DepartureTask departureTask)
                    {
                        reversalduration =
                            departureTask.DepartureSide == departurerouting.ToSide
                                ? departurerouting.Train.ReversalDuration
                                : (Time)0;
                        departurerouting.Start = Math.Max(
                            time,
                            departureTask.ScheduledTime
                                - departurerouting.Duration
                                - reversalduration
                        );
                    }
                    else
                    {
                        // Outstanding train: no fixed deadline, schedule forward
                        reversalduration = (Time)0;
                        departurerouting.Start = time;
                    }
                    foreach (var task in departurerouting.Previous)
                    {
                        if (task.TaskType == TrackTaskType.Service)
                            departurerouting.Start = Math.Max(
                                departurerouting.Start,
                                task.Start + ((ServiceTask)task).MinimumDuration
                            );
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
            MoveTask? move = this.First;
            while (move != null)
            {
                if (move is RoutingTask routing)
                {
                    Console.WriteLine("--> Routing Task");
                    Console.WriteLine($" ===> {routing}");
                    string arrivalmessage = string.Empty;
                    if (routing.Previous.TaskType == TrackTaskType.Arrival)
                    {
                        var arrival = (ArrivalTask)routing.Previous;
                        if (
                            arrival.End
                            < arrival.ScheduledTime
                                + (
                                    arrival.ArrivalSide == arrival.Next.FromSide
                                        ? arrival.Train.ReversalDuration
                                        : (Time)0
                                )
                        )
                            arrivalmessage =
                                " <--- "
                                + (
                                    arrival.ScheduledTime
                                    + (
                                        arrival.ArrivalSide == arrival.Next.FromSide
                                            ? arrival.Train.ReversalDuration
                                            : (Time)0
                                    )
                                ).ToString();
                    }
                    Console.WriteLine(
                        $"{move.Start} | {move.Train} from {routing.FromTrack.PrettyName}{routing.FromSide} to {move.ToTrack.PrettyName}{move.ToSide} | {move.End} {arrivalmessage}"
                    );
                    Console.WriteLine($"    {routing.ToRouteString()}");
                }
                else
                {
                    Console.WriteLine("--> Departure Task");
                    if (move is DepartureRoutingTask departure)
                    {
                        string departuremessage = string.Empty;
                        if (
                            departure.Next is DepartureTask dt2
                            && dt2.Start
                                + (
                                    dt2.DepartureSide == departure.ToSide
                                        ? departure.Train.ReversalDuration
                                        : (Time)0
                                )
                                > dt2.ScheduledTime
                        )
                            departuremessage = " <--- " + dt2.ScheduledTime.ToString();
                        Console.WriteLine(
                            $"{move.Start} | {move.Train} from ({string.Join(",", departure.Previous.Select(task => task.Track.PrettyName))}) to {move.ToTrack.PrettyName}{move.ToSide} {move.End} {departuremessage}"
                        );
                        foreach (var route in departure.GetRoutes())
                            Console.WriteLine($"    {route.Train} : {route}");
                    }
                }
                move = move.NextMove;
            }
        }

        public string OutputTrainUnitSchedule(int debugLevel = 0)
        {
            string return_value = "";
            if (debugLevel > 0)
                Console.WriteLine($"Plan per shunt unit ({this.ShuntUnits.Length}) in total");
            foreach (ShuntTrainUnit unit in this.ShuntUnits)
            {
                string line =
                    $"{unit.Name} : {unit.Arrival.Track.PrettyName} (Arrival {unit.Arrival.Start.ToMinuteString()} - {unit.Arrival.End.ToMinuteString()})";
                var move = unit.Arrival.Next;
                while (move != null)
                {
                    var task = move.GetNext(t => t.Train.UnitBits[unit.Index]).First();
                    line +=
                        $", {task.Track.PrettyName} ({(task as ServiceTask)?.Type.Name ?? (task is DepartureTask ? "departure" : "parking")} {task.Start.ToMinuteString()} - {task.End.ToMinuteString()})";
                    move = task.Next;
                }
                return_value += line + "\n";
                if (debugLevel > 0)
                    Console.WriteLine(line);
            }
            return return_value;
        }

        public void OutputConstraintViolations()
        {
            foreach (TrackOccupation occupation in this.TrackOccupations)
                if (
                    occupation != null
                    && occupation.ViolatingStates.Count > 0
                    && !outsidetrack[occupation.Track.Index]
                )
                    Console.WriteLine(
                        $"{occupation.Track.ID.ToString()}: {string.Join(", ", occupation.ViolatingStates.Select(state => state.Task.Train.ToString()))}"
                    );
        }

        public void ComputeRouting(
            RoutingTask routing,
            int departurecrossingsA,
            int departurecrossingsB
        )
        {
            Track fromtrack = routing.FromTrack,
                totrack = routing.ToTrack;
            Side? toside = routing.ToSide;

            if (fromtrack.Access == Side.Both)
            {
                var routeA = this.RoutingGraph.ComputeRoute(
                    this.TrackOccupations,
                    routing.Train,
                    fromtrack,
                    Side.A,
                    totrack,
                    toside
                );
                routeA.DepartureCrossings = departurecrossingsA;

                var routeB = this.RoutingGraph.ComputeRoute(
                    this.TrackOccupations,
                    routing.Train,
                    fromtrack,
                    Side.B,
                    totrack,
                    toside,
                    routeA.TrackState
                );
                routeB.DepartureCrossings = departurecrossingsB;

                if (
                    routeA.Crossings + routeA.DepartureCrossings
                    < routeB.Crossings + routeB.DepartureCrossings
                )
                    routing.AddRoute(routeA);
                else if (
                    routeA.Crossings + routeA.DepartureCrossings
                    > routeB.Crossings + routeB.DepartureCrossings
                )
                    routing.AddRoute(routeB);
                else if (routeA.Duration < routeB.Duration)
                    routing.AddRoute(routeA);
                else
                    routing.AddRoute(routeB);
            }
            else
            {
                var route = this.RoutingGraph.ComputeRoute(
                    this.TrackOccupations,
                    routing.Train,
                    fromtrack,
                    fromtrack.Access,
                    totrack,
                    toside
                );
                route.DepartureCrossings =
                    fromtrack.Access == Side.A ? departurecrossingsA : departurecrossingsB;
                routing.AddRoute(route);
            }
        }

        public Route ComputeRouting(
            ShuntTrain train,
            Track fromtrack,
            Track totrack,
            Side? toside,
            int departurecrossingsA,
            int departurecrossingsB
        )
        {
            if (fromtrack.Access == Side.Both)
            {
                var routeA = this.RoutingGraph.ComputeRoute(
                    this.TrackOccupations,
                    train,
                    fromtrack,
                    Side.A,
                    totrack,
                    toside
                );
                routeA.DepartureCrossings = departurecrossingsA;

                var routeB = this.RoutingGraph.ComputeRoute(
                    this.TrackOccupations,
                    train,
                    fromtrack,
                    Side.B,
                    totrack,
                    toside,
                    routeA.TrackState
                );
                routeB.DepartureCrossings = departurecrossingsB;

                if (
                    routeA.Crossings + routeA.DepartureCrossings
                    < routeB.Crossings + routeB.DepartureCrossings
                )
                    return routeA;
                if (
                    routeA.Crossings + routeA.DepartureCrossings
                    > routeB.Crossings + routeB.DepartureCrossings
                )
                    return routeB;
                if (routeA.Duration < routeB.Duration)
                    return routeA;
                return routeB;
            }
            else
            {
                var route = this.RoutingGraph.ComputeRoute(
                    this.TrackOccupations,
                    train,
                    fromtrack,
                    fromtrack.Access,
                    totrack,
                    toside
                );
                route.DepartureCrossings =
                    fromtrack.Access == Side.A ? departurecrossingsA : departurecrossingsB;
                return route;
            }
        }

        public string RoutingOrdering()
        {
            MoveTask? move = this.First;
            string result = string.Empty;
            while (move != null)
            {
                result +=
                    $"{move.Start} - {move.End}: {move.ToString()} {move.DepartureCrossings}+{move.Crossings}, ";
                move = move.NextMove;
            }
            return result;
        }

        public SolutionCost ComputeCost()
        {
            SolutionCost cost = new();

            foreach (bool[] finished in this.FreeServiceTaskFinished)
                for (int i = 0; i < finished.Length; i++)
                    finished[i] = true;

            bool checkmaintenance = true;
            BitSet done = new(ProblemInstance.Current.TrainUnits.Length);

            foreach (ArrivalTask arrival in this.ArrivalTasks)
                if (
                    arrival.End
                    > arrival.ScheduledTime
                        + (
                            arrival.ArrivalSide == arrival.Next.FromSide
                                ? arrival.Train.ReversalDuration
                                : (Time)0
                        )
                )
                {
                    logger.LogInformation(
                        "Arrival delay: {end} > {schedule} for train {train}",
                        arrival.End,
                        arrival.ScheduledTime,
                        arrival.Train
                    );
                    cost.ArrivalDelays++;
                    cost.ArrivalDelaySum += arrival.End - arrival.ScheduledTime;
                    cost.ProblemTrains |= arrival.Train.UnitBits;
                }

            foreach (DepartureTask departure in this.DepartureTasks)
                if (
                    departure.Start
                        + (
                            departure.DepartureSide == departure.Previous.ToSide
                                ? departure.Train.ReversalDuration
                                : (Time)0
                        )
                    > departure.ScheduledTime
                )
                {
                    cost.DepartureDelays++;
                    cost.DepartureDelaySum += departure.Start - departure.ScheduledTime;
                    cost.ProblemTrains |= departure.Train.UnitBits;
                }

            MoveTask? move = this.First;
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
                        if (!task.IsParkingLike || task.Train.UnitBits.IsSubsetOf(done))
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
                                if (
                                    !finished[i]
                                    && tasks[i].Type.Tracks.Contains(task.Track)
                                    && time + tasks[i].Duration <= task.End - task.Start
                                )
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

            var tracklengthviolations = this.TrackOccupations.Where(
                (graph, i) => !outsidetrack[i] && graph != null && graph.TrackLengthViolations > 0
            );
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

            TrackTask? previous = null;
            ShuntTrain? train = null;
            TrackTask? first = null,
                last = null;
            State? next = null;
            bool newShuntTrainConstructed = false;
            for (int i = 0; i < task.Previous.Count; i++)
            {
                TrackTask tracktask =
                    task.ToSide == Side.A
                        ? task.Previous[task.Previous.Count - i - 1]
                        : task.Previous[i];

                // get the adjacent state
                var currentnext = task.ToSide == Side.A ? tracktask.State.A : tracktask.State.B;
                tracktask.State.ComputeCrossings();

                // depart the current task
                tracktask.Depart(this.TrackOccupations[tracktask.Track.Index]);

                // compute route for previous train if necessary
                if (tracktask.Track != previous?.Track || (next != tracktask.State))
                {
                    if (train != null)
                    {
                        Debug.Assert(previous != null && first != null && last != null);
                        this.computeDepartureRoute(task, train, previous.Track, first, last);
                    }
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
            {
                Debug.Assert(previous != null && first != null && last != null);
                this.computeDepartureRoute(task, train, previous.Track, first, last);
            }

            if (task.Next is ParkingTask finalParking)
                finalParking.Arrive(this.TrackOccupations[finalParking.Track.Index]);
        }

        protected void computeDepartureRoute(
            DepartureRoutingTask move,
            ShuntTrain train,
            Track track,
            TrackTask first,
            TrackTask last
        )
        {
            int a = 0,
                b = 0;
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
            MoveTask? move = this.First;
            List<TrackTask> tasks = [];
            while (move != null)
            {
                if (
                    !move.SkipsParking
                    && !move.AllPreviousSatisfy(t => t is ParkingTask)
                    && !move.AllNextSatisfy(t => t is ParkingTask)
                )
                    throw new InvalidOperationException("move failed to mention parking skipping");

                foreach (TrackTask task in move.AllPrevious)
                {
                    if (task.Next != move)
                        throw new InvalidOperationException("track-route linkage failure");
                    // FIXME: in case of a circular reference, the following will never return but recurse infinitely:
                    task.Next.FindAllNext(t => t == task, tasks);
                    if (tasks.Count > 0)
                        throw new InvalidOperationException("track-route circular reference");
                    if (task is not ArrivalTask && task.Previous != null)
                    {
                        task.Previous.FindAllPrevious(t => t == task, tasks);
                        if (tasks.Count > 0)
                            throw new InvalidOperationException("track-route circular reference");
                    }

                    if (task is ServiceTask service)
                    {
                        for (
                            ServiceTask? s = service.NextServiceTask;
                            s != null;
                            s = s.NextServiceTask
                        )
                        {
                            if (service == s)
                                throw new InvalidOperationException("circular service references");
                            if (service.Resource != s.Resource)
                                throw new InvalidOperationException("invalid service references");
                            if (
                                service.Next.MoveOrder > s.Previous.MoveOrder
                                || service.Next.MoveOrder > s.Next.MoveOrder
                            )
                                throw new InvalidOperationException("resource conflict");
                        }
                        for (
                            ServiceTask? s = service.PreviousServiceTask;
                            s != null;
                            s = s.PreviousServiceTask
                        )
                        {
                            if (service == s)
                                throw new InvalidOperationException("circular service references");
                            if (service.Resource != s.Resource)
                                throw new InvalidOperationException("invalid service references");
                            if (
                                service.Previous.MoveOrder < s.Previous.MoveOrder
                                || service.Previous.MoveOrder < s.Next.MoveOrder
                            )
                                throw new InvalidOperationException("resource conflict");
                        }
                    }
                }

                foreach (TrackTask task in move.AllNext)
                {
                    if (task.Previous != move)
                        throw new InvalidOperationException("track-route linkage failure");
                    if (task is not DepartureTask && task.Next != null)
                    {
                        task.Next.FindAllNext(t => t == task, tasks);
                        if (tasks.Count > 0)
                            throw new InvalidOperationException("track-route circular reference");
                    }
                    task.Previous.FindAllPrevious(t => t == task, tasks);
                    if (tasks.Count > 0)
                        throw new InvalidOperationException("track-route circular reference");
                }

                if (
                    move.AllNext.Count > 1
                    && move.AllNext.Any(task =>
                        task.Track != move.AllNext.First().Track
                        || task.ArrivalSide != move.AllNext.First().ArrivalSide
                    )
                )
                    throw new InvalidOperationException("split not on same track");

                if (move.PreviousMove != null && move.PreviousMove.NextMove != move)
                    throw new InvalidOperationException("move-move linkage failure");

                for (MoveTask? other = move.NextMove; other != null; other = other.NextMove)
                    if (other == move)
                        throw new InvalidOperationException("circular move-move references");
                for (MoveTask? other = move.PreviousMove; other != null; other = other.PreviousMove)
                    if (other == move)
                        throw new InvalidOperationException("circular move-move references");

                move = move.NextMove;
            }
        }

        public static void Clear()
        {
            foreach (var location in ProblemInstance.Current.ServiceLocations)
            {
                if (location != null)
                {
                    location.First = location.Last = null;
                }
            }
        }

        public void OutputForDemian()
        {
            using (StreamWriter sw = new("demian.txt"))
            {
                MoveTask? move = this.First;
                while (move != null)
                {
                    if (move is RoutingTask routing)
                    {
                        string arrivalmessage = string.Empty;
                        if (routing.Previous.TaskType == TrackTaskType.Arrival)
                        {
                            var arrival = (ArrivalTask)routing.Previous;
                            if (
                                arrival.End
                                < arrival.ScheduledTime
                                    + (
                                        arrival.ArrivalSide == arrival.Next.FromSide
                                            ? arrival.Train.ReversalDuration
                                            : (Time)0
                                    )
                            )
                                arrivalmessage =
                                    " <--- "
                                    + (
                                        arrival.ScheduledTime
                                        + (
                                            arrival.ArrivalSide == arrival.Next.FromSide
                                                ? arrival.Train.ReversalDuration
                                                : (Time)0
                                        )
                                    ).ToString();
                        }
                        sw.WriteLine(
                            $"{move.Start} | {move.Train} from {routing.FromTrack.PrettyName}{routing.FromSide} to {move.ToTrack.PrettyName}{move.ToSide} | {move.End} {arrivalmessage}"
                        );
                        sw.WriteLine($"    {routing.ToRouteString()}");
                    }
                    else
                    {
                        if (move is DepartureRoutingTask departure)
                        {
                            string departuremessage = string.Empty;
                            if (
                                departure.Next is DepartureTask dt3
                                && dt3.Start
                                    + (
                                        dt3.DepartureSide == departure.ToSide
                                            ? departure.Train.ReversalDuration
                                            : (Time)0
                                    )
                                    > dt3.ScheduledTime
                            )
                                departuremessage = " <--- " + dt3.ScheduledTime.ToString();
                            sw.WriteLine(
                                $"{move.Start} | {move.Train} from ({string.Join(",", departure.Previous.Select(task => task.Track.PrettyName))}) to {move.ToTrack.PrettyName}{move.ToSide} {move.End} {departuremessage}"
                            );
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
                    string line =
                        $"{unit.Name} : {unit.Arrival.Track.PrettyName} (Arrival {unit.Arrival.Start.ToMinuteString()})";
                    move = unit.Arrival.Next;
                    while (move != null)
                    {
                        var task = move.GetNext(t => t.Train.UnitBits[unit.Index]).First();
                        line +=
                            $", {task.Track.PrettyName} ({(task as ServiceTask)?.Type.Name ?? (task is DepartureTask ? "departure" : "parking")} {task.Start.ToMinuteString()})";
                        move = task.Next;
                    }
                    sw.WriteLine(line);
                }
            }
        }

        // @validateFinal: when true (only appropriate for the actual
        // delivered plan, not the tmp_plans/ debug snapshots TabuSearch and
        // SimulatedAnnealing write on every improving move), refuse to
        // silently ship a plan that contains a split whose physical
        // placement couldn't be determined and fell back to a guess (see
        // unverifiedSplitPlacements). The file is still written first so the
        // unverified plan remains available for inspection.
        public void WriteJSONFile(string filePath, bool validateFinal = false)
        {
            Plan? plan = this.ToPlan();
            string jsonPlan = plan.SerializeJson();
            File.WriteAllText(filePath, jsonPlan);

            if (validateFinal && this.unverifiedSplitPlacements.Count > 0)
            {
                throw new InvalidOperationException(
                    "Refusing to deliver a plan containing a split whose physical placement "
                        + "could not be verified (most likely a split immediately following an "
                        + "earlier split of the same train, which isn't yet reasoned about): "
                        + string.Join("; ", this.unverifiedSplitPlacements)
                );
            }
        }

        public Plan? ToPlan()
        {
            if (
                ProblemInstance.Current.InterfaceLocation == null
                || ProblemInstance.Current.InterfaceScenario == null
            )
                return null;

            List<Interchange.Action> actions = [];

            Dictionary<ShuntTrain, ShuntingUnit> trainconversion = [];

            MoveTask? move = this.First;
            while (move != null)
            {
                // Console.WriteLine($"Now processing move {move.TaskType} of train {move.Train} at {(int)move.Start}--{(int)move.End} from {move.FromTrack} to {move.ToTrack}");
                if (move.TaskType == MoveTaskType.Standard)
                {
                    var routing = (RoutingTask)move;
                    var endtime = (ulong)routing.End;

                    // Add split
                    if (routing.IsSplit)
                    {
                        var splitaction = new Interchange.Action
                        {
                            Location = routing.ToTrack.ID,
                            TaskType = TaskType.FromPredefined(Split),
                            EndTime = endtime,
                            StartTime = endtime =
                                (ulong)(
                                    routing.End
                                    - routing.Train.Units[0].Type.SplitDuration
                                        * (routing.Next.Count - 1)
                                ),
                            ShuntingUnit = GetShuntUnit(move.Train, trainconversion),
                        };
                        actions.Add(splitaction);

                        // add parent-child relation
                        foreach (var task in routing.Next)
                        {
                            var shuntingunit = GetShuntUnit(task.Train, trainconversion);
                            splitaction.ShuntingUnit.ChildIDs.Add(shuntingunit.Id);
                            shuntingunit.ParentIDs.Add(splitaction.ShuntingUnit.Id);
                        }
                    }

                    // Add move.
                    //
                    // Only when the train actually travels. A routing task's
                    // duration also covers the decoupling of a split, so a split
                    // that stays on its own track has a non-zero duration without
                    // a route: reading the duration alone would emit a Move action
                    // whose path is empty, and the RemoveAt below would then run
                    // off the end of the list. NumberOfRoutes is the model's own
                    // test for "this routing traverses a route".
                    if (routing.NumberOfRoutes > 0)
                    {
                        var moveaction = new Interchange.Action
                        {
                            Location = routing.FromTrack.ID,
                            TaskType = TaskType.FromPredefined(Move),
                            StartTime = (ulong)routing.Start,
                            EndTime = endtime,
                            ShuntingUnit = GetShuntUnit(move.Train, trainconversion),
                        };

                        Infrastructure? previous = null;
                        foreach (var arc in routing.Route.Arcs)
                        {
                            foreach (var infra in arc.Path.Path)
                            {
                                if (infra != previous)
                                {
                                    var resource = Resource.FromInfra(infra);
                                    moveaction.Resources.Add(resource);

                                    previous = infra;
                                }
                            }
                        }
                        // remove first
                        //
                        // NumberOfRoutes > 0 is meant to guarantee at least one
                        // resource was added above, but doesn't always hold - a
                        // route whose arcs all resolve to the same infrastructure
                        // as `previous` (observed on short/single-hop routes, e.g.
                        // solver known_problems/invalid_endmove, seed 5) collapses
                        // to zero resources. Skip rather than crash so the rest of
                        // the plan still gets written; the resulting action having
                        // no resources is a real gap, not fixed here - see #24.
                        if (moveaction.Resources.Count > 0)
                        {
                            moveaction.Resources.RemoveAt(0);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Move action for shunting unit {ShuntingUnitId} at {Location} from {StartTime} to {EndTime} has a route but does not specify it. The delivered plan does not correctly represent this move. See issue #24.",
                                moveaction.ShuntingUnit.Id,
                                moveaction.Location,
                                moveaction.StartTime,
                                moveaction.EndTime
                            );
                        }
                        // add to plan
                        actions.Add(moveaction);
                    }

                    // Add task
                    AddTrackAction(routing.Previous, trainconversion, actions);
                }
                else
                {
                    var departurerouting = (DepartureRoutingTask)move;
                    var starttime = departurerouting.Start;

                    foreach (var route in departurerouting.GetRoutes())
                    {
                        var tasks = departurerouting.GetPrevious(task =>
                            task.Train.UnitBits.Intersects(route.Train.UnitBits)
                        );
                        var shuntingunit = GetShuntUnit(route.Train, trainconversion);

                        // Add tasks
                        foreach (var task in tasks)
                            AddTrackAction(task, starttime, trainconversion, actions);

                        // Add merge
                        if (tasks.Count() > 1)
                        {
                            foreach (var task in tasks)
                            {
                                var mergeaction = new Interchange.Action
                                {
                                    Location = task.Track.ID,
                                    TaskType = TaskType.FromPredefined(Combine),
                                    StartTime = (ulong)starttime,
                                    EndTime = (ulong)(
                                        starttime
                                        + departurerouting.Train.Units[0].Type.CombineDuration
                                            * (tasks.Count() - 1)
                                    ),
                                    ShuntingUnit = GetShuntUnit(task.Train, trainconversion),
                                };
                                actions.Add(mergeaction);

                                // add parent-child-relation
                                mergeaction.ShuntingUnit.ChildIDs.Add(shuntingunit.Id);
                                shuntingunit.ParentIDs.Add(mergeaction.ShuntingUnit.Id);
                            }
                            starttime +=
                                departurerouting.Train.Units[0].Type.CombineDuration
                                * (tasks.Count() - 1);
                        }

                        // Add move
                        var moveaction = new Interchange.Action
                        {
                            Location = route.Tracks[0].ID,
                            TaskType = TaskType.FromPredefined(Move),
                            StartTime = (ulong)starttime,
                            EndTime = (ulong)(starttime + route.Duration),
                            ShuntingUnit = shuntingunit,
                        };
                        // add path
                        Infrastructure? previous = null;
                        foreach (var arc in route.Arcs)
                        {
                            foreach (var infra in arc.Path.Path)
                            {
                                if (infra != previous)
                                {
                                    var resource = Resource.FromInfra(infra);
                                    moveaction.Resources.Add(resource);

                                    previous = infra;
                                }
                            }
                        }
                        // remove first - same gap as the arrival/general routing case
                        // above (see #24), just already guarded here; add the
                        // matching diagnostic so a departure-side occurrence is
                        // traceable the same way. This is in fact the common case -
                        // see #24's frequency findings.
                        if (moveaction.Resources.Count > 0)
                        {
                            moveaction.Resources.RemoveAt(0);
                        }
                        else
                        {
                            logger.LogWarning(
                                "Departure move action for shunting unit {ShuntingUnitId} at {Location} from {StartTime} to {EndTime} has a route but does not specify it. The delivered plan does not correctly represent this move. See issue #24.",
                                moveaction.ShuntingUnit.Id,
                                moveaction.Location,
                                moveaction.StartTime,
                                moveaction.EndTime
                            );
                        }
                        // add to plan
                        actions.Add(moveaction);
                        starttime += route.Duration;
                    }
                    var departureshuntunit = GetShuntUnit(departurerouting.Train, trainconversion);
                    // Add merge
                    if (departurerouting.GetRoutes().Count > 1)
                    {
                        foreach (var route in departurerouting.GetRoutes())
                        {
                            var mergeaction = new Interchange.Action
                            {
                                Location = departurerouting.Next.Track.ID,
                                TaskType = TaskType.FromPredefined(Combine),
                                StartTime = (ulong)starttime,
                                EndTime = (ulong)departurerouting.End,
                                ShuntingUnit = GetShuntUnit(route.Train, trainconversion),
                            };
                            actions.Add(mergeaction);

                            // add parent-child-relation
                            mergeaction.ShuntingUnit.ChildIDs.Add(departureshuntunit.Id);
                            departureshuntunit.ParentIDs.Add(mergeaction.ShuntingUnit.Id);
                        }
                    }
                    // Add departure
                    AddTrackAction(departurerouting.Next, trainconversion, actions);
                }
                move = move.NextMove;
            }

            return ActionsToPlan(actions);
        }

        /// <summary>
        /// Clean up and sort actions, then return a Plan.
        /// </summary>
        static Plan ActionsToPlan(IList<Interchange.Action> actions)
        {
            ShuntingUnit? lastShuntingUnit = null;
            Interchange.Action? waitAction = null;
            ulong lastEndTime = ulong.MinValue;
            HashSet<Interchange.Action> toDelete = [];

            foreach (
                Interchange.Action a in actions
                    .Where(a => a.TaskType?.Predefined == Wait)
                    .OrderBy(a => a.ShuntingUnit.Id)
                    .ThenBy(a => a.StartTime)
                    .ThenBy(a => a.EndTime)
            )
            {
                if (a.ShuntingUnit.Equals(lastShuntingUnit) && a.StartTime == lastEndTime)
                {
                    logger.LogWarning(
                        "ShuntingUnit {ShuntingUnit}: start of wait {StartTime}-{EndTime} coincides with end of previous wait. Merging.",
                        a.ShuntingUnit,
                        a.StartTime,
                        a.EndTime
                    );
                    Debug.Assert(waitAction != null);
                    waitAction.EndTime = a.EndTime;
                    toDelete.Add(a);
                }
                else
                {
                    waitAction = a;
                    lastShuntingUnit = a.ShuntingUnit;
                    lastEndTime = a.EndTime ?? ulong.MaxValue;
                }
            }

            Plan plan_pb = new() { Actions = [] };
            foreach (
                Interchange.Action a in actions
                    .OrderBy(a => a.StartTime)
                    .ThenBy(a => a.EndTime)
                    .ThenBy(a => TaskTypeOrder(a.TaskType.Predefined))
                    .ThenBy(a => a.TaskType.Other)
            )
            {
                if (!toDelete.Contains(a))
                {
                    plan_pb.Actions.Add(a);
                }
            }

            return plan_pb;
        }

        public void DisplayMovements()
        {
            MoveTask? move = this.First;
            int i = 0;
            while (move != null)
            {
                Console.WriteLine($"============Move: {i} --- {move.TaskType}===============");
                Console.WriteLine($"Start time: {(int)move.Start} - End time: {(int)move.End}");
                Console.WriteLine($"From : {move.FromTrack} -> To : {move.ToTrack} ({move.Train})");

                if (move is RoutingTask routing)
                {
                    Console.WriteLine("Infrastructure used (tracks):");
                    var tracks = routing.Route.Tracks;
                    var lastTrack = tracks.Last();
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
                        Console.Write("\n");
                    }
                    Console.WriteLine("All Previous tasks:");
                    foreach (TrackTask task in routing.AllPrevious)
                    {
                        Console.WriteLine(
                            $"---{task.GetType().Name}: {task} - Start Time: {(int)task.Start} - End Time: {(int)task.End}----"
                        );
                    }
                    Console.WriteLine("All Next tasks:");
                    foreach (TrackTask task in routing.AllNext)
                    {
                        Console.WriteLine(
                            $"---{task.GetType().Name} {task} - Start Time: {(int)task.Start} - End Time: {(int)task.End}----"
                        );
                    }
                }
                else if (move.TaskType is MoveTaskType.Departure)
                {
                    Console.WriteLine("All Previous tasks:");
                    foreach (TrackTask task in move.AllPrevious)
                    {
                        Console.WriteLine(
                            $"---{task.GetType().Name} {task} - Start Time: {(int)task.Start} - End Time: {(int)task.End}{(task.Train.InStanding ? " (Instanding Train)" : "")}----"
                        );
                    }
                    Console.WriteLine("All Next tasks:");
                    foreach (TrackTask task in move.AllNext)
                    {
                        Console.WriteLine(
                            $"---{task.GetType().Name} {task} - Start Time: {(int)task.Start} - End Time: {(int)task.End}{(task.Train.InStanding ? " (Outstanding Train)" : "")}----"
                        );
                    }
                    if (move is DepartureRoutingTask routingDeparture)
                    {
                        var listOfRoutes = routingDeparture.GetRoutes();
                        Console.WriteLine(
                            $"Infrastructure used (tracks) number of routes {listOfRoutes.Count}:"
                        );
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
                            Console.Write("\n");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"WARNING: did not recognize move type: {move.TaskType}");
                }
                i++;
                move = move.NextMove;
            }
        }

        /// <summary>
        /// Tie-break order for actions that share a start and end time.
        /// </summary>
        /// <remarks>
        /// A Dictionary can't be used here: its keys can't be null, so a custom
        /// (Other) task type's null Predefined value has no representable fallback
        /// key. An earlier version used <c>default</c> as that fallback, but
        /// <c>default(PredefinedTaskType)</c> equals Move — its first, zero-valued
        /// member — which silently overwrote Move's order and sorted it after Exit.
        /// Any PredefinedTaskType not listed falls into the trailing catch-all.
        ///
        /// StandIn and StandOut share a bucket with Arrive and Exit. They are the
        /// chain head and tail of an inStanding/outStanding train and are emitted
        /// with zero duration at the scenario start and end, where their timestamps
        /// necessarily tie with the Wait that follows or precedes them, so this
        /// comparator is the only thing keeping them on the correct side of it.
        /// </remarks>
        internal static int TaskTypeOrder(PredefinedTaskType? t) =>
            t switch
            {
                Arrive => 0,
                StandIn => 0,
                Move => 1,
                Wait => 2,
                Split => 3,
                Combine => 4,
                Exit => 5,
                StandOut => 5,
                _ => 6,
            };

        private static void AddTrackAction(
            TrackTask task,
            Dictionary<ShuntTrain, ShuntingUnit> trainconversion,
            List<Interchange.Action> actions
        )
        {
            AddTrackAction(task, task.End, trainconversion, actions);
        }

        private static void AddTrackAction(
            TrackTask task,
            Time endtime,
            Dictionary<ShuntTrain, ShuntingUnit> trainconversion,
            List<Interchange.Action> actions
        )
        {
            var trackaction = new Interchange.Action
            {
                Location = task.Track.ID,
                ShuntingUnit = GetShuntUnit(task.Train, trainconversion),
                TaskType = null!, // set in the switch statement below, verified non-null before exiting the method
            };
            switch (task.TaskType)
            {
                case TrackTaskType.Arrival:
                {
                    var arrival = (ArrivalTask)task;
                    trackaction.TaskType = TaskType.FromPredefined(Arrive);
                    trackaction.StartTime = trackaction.EndTime = (ulong)arrival.ScheduledTime;

                    var gatewayconnection = ProblemInstance.Current.GatewayConversion[
                        task.Track.ID
                    ];
                    trackaction.Location = gatewayconnection.Path[0].ID;
                    Infrastructure? previous = null;
                    foreach (var infra in gatewayconnection.Path)
                        if (infra != previous)
                        {
                            var resource = Resource.FromInfra(infra);
                            trackaction.Resources.Add(resource);

                            previous = infra;
                        }
                    trackaction.Resources.RemoveAt(0);

                    if (endtime > arrival.ScheduledTime)
                    {
                        var nextparking = new Interchange.Action
                        {
                            Location = task.Track.ID,
                            ShuntingUnit = GetShuntUnit(task.Train, trainconversion),
                            TaskType = TaskType.FromPredefined(Wait),
                            StartTime = trackaction.EndTime,
                            EndTime = (ulong)endtime,
                        };
                        actions.Add(nextparking);
                    }
                    break;
                }
                case TrackTaskType.Parking:
                    trackaction.TaskType = TaskType.FromPredefined(Wait);
                    trackaction.StartTime = (ulong)task.Start;
                    trackaction.EndTime = (ulong)endtime;
                    break;
                case TrackTaskType.StandIn:
                {
                    // The train is already on its track when the scenario starts, so
                    // there is no gateway route to reserve: a StandIn is an instantaneous
                    // marker that puts the shunting unit on the yard, followed by a Wait
                    // for however long it stands there. This mirrors the Arrival case.
                    trackaction.TaskType = TaskType.FromPredefined(StandIn);
                    trackaction.StartTime = trackaction.EndTime = (ulong)task.Start;
                    if (endtime > task.Start)
                    {
                        var nextparking = new Interchange.Action
                        {
                            Location = task.Track.ID,
                            ShuntingUnit = GetShuntUnit(task.Train, trainconversion),
                            TaskType = TaskType.FromPredefined(Wait),
                            StartTime = trackaction.EndTime,
                            EndTime = (ulong)endtime,
                        };
                        actions.Add(nextparking);
                    }
                    break;
                }
                case TrackTaskType.StandOut:
                {
                    // Mirror of StandIn: Wait until the scenario ends, then an
                    // instantaneous StandOut marker. No gateway route either.
                    //
                    // The marker belongs at the scenario horizon, not at task.End.
                    // An outStanding train has no departure of its own; what the
                    // request asserts is where it stands when the scenario ends,
                    // and TORS only accepts its exit once the clock has reached
                    // that point. ProblemInstance builds these DepartureTrains
                    // with ScenarioEndTime for the same reason, but the scheduler
                    // then moves task.End to whenever the train actually settles.
                    var horizon = (ulong)ProblemInstance.Current.ScenarioEndTime;
                    // No trailing Wait: TORS derives a wait's duration from the next
                    // event rather than from the plan, and rejects one outright when
                    // there is no next event to wait for — which is precisely the
                    // situation of a train that simply stays put until the scenario
                    // ends. The marker alone says where it stands.
                    trackaction.TaskType = TaskType.FromPredefined(StandOut);
                    // If the train is still busy past the horizon the plan does not
                    // in fact leave it standing at the end, and emitting the marker
                    // here lets the evaluator say so rather than hiding it.
                    trackaction.StartTime = trackaction.EndTime = horizon;
                    break;
                }
                case TrackTaskType.Service:
                    var service = (ServiceTask)task;
                    if (service.Start > service.Previous.End)
                    {
                        var previousparking = new Interchange.Action
                        {
                            Location = task.Track.ID,
                            ShuntingUnit = GetShuntUnit(task.Train, trainconversion),
                            TaskType = TaskType.FromPredefined(Wait),
                            StartTime = (ulong)service.Previous.End,
                            EndTime = (ulong)service.Start,
                        };
                        actions.Add(previousparking);
                    }
                    trackaction.TaskType = new TaskType(null, service.Type.Name);
                    trackaction.StartTime = (ulong)service.Start;
                    trackaction.EndTime = trackaction.StartTime + (ulong)service.MinimumDuration;
                    if (endtime - service.Start > service.MinimumDuration)
                    {
                        var nextparking = new Interchange.Action
                        {
                            Location = task.Track.ID,
                            ShuntingUnit = GetShuntUnit(task.Train, trainconversion),
                            TaskType = TaskType.FromPredefined(Wait),
                            StartTime = trackaction.EndTime,
                            EndTime = (ulong)endtime,
                        };
                        actions.Add(nextparking);
                    }
                    var facilityresource = Resource.FromFacility(
                        ProblemInstance.Current.FacilityConversion[service.Type]
                    );
                    trackaction.Resources.Add(facilityresource);
                    break;
                case TrackTaskType.Departure:
                {
                    trackaction.TaskType = TaskType.FromPredefined(Exit);
                    trackaction.StartTime = trackaction.EndTime = (ulong)task.End;
                    var gatewayconnection2 = ProblemInstance.Current.GatewayConversion[
                        task.Track.ID
                    ];
                    Infrastructure? previous2 = null;
                    for (int i = gatewayconnection2.Path.Length - 1; i >= 0; i--)
                    {
                        var infra = gatewayconnection2.Path[i];
                        if (infra != previous2)
                        {
                            var resource = Resource.FromInfra(infra);
                            trackaction.Resources.Add(resource);
                        }
                        previous2 = infra;
                    }
                    trackaction.Resources.RemoveAt(0);
                    break;
                }
            }
            Debug.Assert(trackaction.TaskType != null);
            actions.Add(trackaction);
        }

        private static ShuntingUnit GetShuntUnit(
            ShuntTrain train,
            Dictionary<ShuntTrain, Interchange.ShuntingUnit> trainconversion
        )
        {
            if (!trainconversion.TryGetValue(train, out ShuntingUnit? shuntingunit))
            {
                ulong id =
                    trainconversion.Count > 0 ? trainconversion.Max(kvp => kvp.Value.Id) + 1 : 0;
                shuntingunit = new ShuntingUnit(id);
                foreach (var unit in train.Units)
                    shuntingunit.MemberIDs.Add(
                        ProblemInstance.Current.TrainUnitConversion[unit.Base].Id
                    );
                trainconversion[train] = shuntingunit;
            }
            else
            {
                var _shuntingunit = new ShuntingUnit(shuntingunit);

                trainconversion[train] = _shuntingunit;
                return _shuntingunit;
            }
            return shuntingunit;
        }

        /// <summary>
        /// Run assertions for the well-formedness of the data structures. Meant to be called as <code>Debug.Assert(IsWellFormed())</code>.
        /// </summary>
        /// <returns>True, unless an assertion fails.</returns>
        internal bool IsWellFormed()
        {
            HashSet<TrackTask> seen_tt = [];
            Dictionary<MoveTask, int> seen_mt = [];

            Debug.Assert(CheckGraphStructure(seen_mt, seen_tt));

            // Check other well-formedness criteria
            foreach (var tt in seen_tt)
            {
                Debug.Assert(tt.Track != null);
            }

            // Check the linked list of MoveTask_s
            Debug.Assert(this.First != null && this.Last != null, "First and Last must be set");

            int count = 0;
            for (MoveTask? mt = this.First; mt != null; mt = mt.NextMove)
            {
                // Debug.Assert(seen_mt.ContainsKey(mt), "MoveTask not in task graph");
                Debug.Assert(mt.Graph == this, "MoveTask.Graph not correctly set");
                Debug.Assert(
                    mt.FromTrack != null && mt.ToTrack != null,
                    "MoveTask.{FromTrack,ToTrack} must be non-null"
                );
                count++;
            }

            // Debug.Assert(
            //     count == seen_mt.Count,
            //     "Mismatch between task graph and linked list of MoveTasks"
            // );

            return true;
        }

        /// <summary>
        /// Verify graph data structure: start with arrival tasks, then alternating MoveTask_s and TrackTask_s, to end up in a Departure (preceded by a DepartureMove).
        /// Check that all links are correct and that there are no cycles.
        /// </summary>
        /// <returns>True, unless an assertion fails.</returns>
        private bool CheckGraphStructure(
            Dictionary<MoveTask, int> seen_mt,
            HashSet<TrackTask> seen_tt
        )
        {
            Queue<MoveTask> queue_mt = [];
            Queue<TrackTask> queue_tt = [];
            // Seed from every chain head. An inStanding train has no ArrivalTask —
            // it is already parked when the scenario starts — so its StandInTask is
            // the head instead. Omitting these leaves their whole chain unvisited,
            // which the traversal below then reports as a broken graph.
            foreach (TrackTask at in this.ArrivalTasks.Concat<TrackTask>(this.StandInTasks))
            {
                Debug.Assert(at != null, "Chain head must not be null");
                Debug.Assert(!seen_tt.Contains(at), "Duplicate chain head");
                seen_tt.Add(at);
                Debug.Assert(at.Track != null, "Track must be set");
                Debug.Assert(at.Previous == null, "Chain head must not have Previous task");
                Debug.Assert(at.Next != null, "Chain head must have a Next task");
                queue_mt.Enqueue(at.Next);
            }
            while (queue_mt.Count != 0 || queue_tt.Count != 0)
            {
                while (queue_mt.Count != 0)
                {
                    MoveTask mt = queue_mt.Dequeue();
                    Debug.Assert(mt != null);
                    if (seen_mt.TryGetValue(mt, out int value))
                    {
                        value++;
                    }
                    else
                    {
                        value = 1;
                        Debug.Assert(mt.AllNext.Count > 0 && mt.AllPrevious.Count > 0);
                        foreach (TrackTask tt in mt.AllPrevious)
                        {
                            Debug.Assert(tt != null);
                            Debug.Assert(seen_tt.Contains(tt));
                            Debug.Assert(tt.Next == mt);
                        }
                        foreach (TrackTask tt in mt.AllNext)
                        {
                            Debug.Assert(tt != null && tt.Previous == mt);
                            queue_tt.Enqueue(tt);
                        }
                    }
                    seen_mt[mt] = value;
                }
                while (queue_tt.Count != 0)
                {
                    TrackTask tt = queue_tt.Dequeue();
                    Debug.Assert(!seen_tt.Contains(tt));
                    seen_tt.Add(tt);
                    Debug.Assert(tt.Previous != null); // this was actually already asserted when enqueueing `tt`
                    if (tt.Next == null)
                    {
                        Debug.Assert(
                            tt.TaskType == TrackTaskType.Departure || tt.IsParkingLike,
                            "Only DepartureTask, ParkingTask or StandOutTask may have Next unset"
                        );
                        Debug.Assert(
                            tt.Previous.TaskType == MoveTaskType.Departure,
                            "Departure preceded by regular MoveTask"
                        );
                    }
                    else
                    {
                        Debug.Assert(
                            tt.TaskType != TrackTaskType.Departure,
                            "DepartureTask must not have Next set"
                        );
                        Debug.Assert(
                            tt.Previous.TaskType != MoveTaskType.Departure,
                            "DepartureMove followed by non-Departure"
                        );
                        queue_mt.Enqueue(tt.Next);
                    }
                }
            }

            // Check that the counts are correct
            foreach (var kvp in seen_mt)
            {
                Debug.Assert(kvp.Key.AllPrevious.Count == kvp.Value);
            }

            return true;
        }
    }
}
