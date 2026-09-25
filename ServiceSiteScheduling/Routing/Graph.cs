using System.Diagnostics;
using Priority_Queue;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Routing
{
    class RoutingGraph
    {
        private SuperVertex[] SuperVertices;
        private Vertex[] Vertices;
        private int[][] TrackCount;
        private int[][] ReversalCount;
        private int[][] SwitchCount;
        private Arc[,] ArcMatrix;

        private FastPriorityQueue<Vertex> priorityqueue;
        private Storage[,] storages;

        // Vertices touched (Discovered) by the most recent `Dijkstra` call, so the next
        // call can reset just those instead of sweeping all Vertices -- see `Dijkstra`.
        private readonly List<Vertex> touchedVertices = [];

        public RoutingGraph(SuperVertex[] supervertices)
        {
            this.SuperVertices = supervertices;
            this.storages = new Storage[
                ProblemInstance.Current.Tracks.Length,
                ProblemInstance.Current.Tracks.Length
            ];
            var (storageIndices, storageBitSize) = Storage.ComputeIndices();
            for (int i = 0; i < ProblemInstance.Current.Tracks.Length; i++)
            for (int j = 0; j < ProblemInstance.Current.Tracks.Length; j++)
                this.storages[i, j] = new Storage(
                    ProblemInstance.Current.Tracks[i],
                    ProblemInstance.Current.Tracks[j],
                    storageIndices,
                    storageBitSize
                );

            this.Vertices = new Vertex[4 * supervertices.Length];
            this.ArcMatrix = new Arc[this.Vertices.Length, this.Vertices.Length];
            for (int i = 0; i < supervertices.Length; i++)
            for (int j = 0; j < 4; j++)
                this.Vertices[4 * i + j] = supervertices[i].SubVertices[j];

            foreach (Vertex vertex in this.Vertices)
            foreach (Arc arc in vertex.Arcs)
                this.ArcMatrix[vertex.Index, arc.Head.Index] = arc;

            this.priorityqueue = new FastPriorityQueue<Vertex>(4 * supervertices.Length);

            ShuntTrain train = new(
                new ShuntTrainUnit[]
                {
                    new(
                        new TrainUnit(
                            -1,
                            ProblemInstance.Current.TrainTypes[0],
                            Array.Empty<Servicing.Service>(),
                            ProblemInstance.Current.ServiceTypes
                        )
                    ),
                }
            );
            this.SwitchCount = new int[this.Vertices.Length][];
            this.TrackCount = new int[this.Vertices.Length][];
            this.ReversalCount = new int[this.Vertices.Length][];
            for (int i = 0; i < this.Vertices.Length; i++)
            {
                this.SwitchCount[i] = new int[this.Vertices.Length];
                this.TrackCount[i] = new int[this.Vertices.Length];
                this.ReversalCount[i] = new int[this.Vertices.Length];
            }

            // TODO: this runs a separate, early-terminating Dijkstra search per *pair*
            // of vertices (twice, once per direction) -- O(V^2) calls. A single
            // non-terminating Dijkstra per *source* vertex already yields shortest
            // paths (and Previous chains) to every other vertex in one pass, which
            // would cut this to O(V) calls (the Dijkstra half of Johnson's algorithm;
            // arc costs here are non-negative, so Johnson's own Bellman-Ford
            // reweighting step isn't needed). Worth revisiting if this constructor's
            // one-time cost is ever actually measured as a problem -- it isn't known
            // to be one today, and it's a one-off per solve, not a hot-loop cost.
            for (int i = 0; i < this.Vertices.Length; i++)
            {
                var v = this.Vertices[i];
                for (int j = i + 1; j < this.Vertices.Length; j++)
                {
                    var w = this.Vertices[j];

                    var route = this.Dijkstra(train, w, v, RouteDestination.Rest, false);
                    RecordCounts(j, i, route);

                    route = this.Dijkstra(train, v, w, RouteDestination.Rest, false);
                    RecordCounts(i, j, route);
                }
            }

            void RecordCounts(int startIndex, int endIndex, Route route)
            {
                // Count matrices are indexed [endIndex][startIndex], matching the
                // assignments above this function replaces.
                this.ReversalCount[endIndex][startIndex] = route.TotalReversals;
                this.SwitchCount[endIndex][startIndex] = route.TotalSwitches;
                this.TrackCount[endIndex][startIndex] = route.Tracks.Length;
            }
        }

        public static RoutingGraph Construct()
        {
            SuperVertex[] supervertices = new SuperVertex[ProblemInstance.Current.Tracks.Length];

            foreach (Track track in ProblemInstance.Current.Tracks)
            {
                Vertex aa = new(Side.A, Side.A);
                Vertex ab = new(Side.A, Side.B);
                Vertex ba = new(Side.B, Side.A);
                Vertex bb = new(Side.B, Side.B);
                Debug.Assert(
                    aa != ab && aa != ba && aa != bb && ab != ba && ab != bb && ba != bb,
                    $"Track {track}'s four sub-vertices (AA/AB/BA/BB) must be pairwise distinct "
                        + "objects -- ComputeRoute's origin==destination check relies on reference "
                        + "equality between them"
                );
                SuperVertex v = new(track, aa, ab, ba, bb, track.Index);
                supervertices[v.Index] = v;
                aa.SuperVertex = ab.SuperVertex = ba.SuperVertex = bb.SuperVertex = v;

                // Add track arcs
                aa.Arcs.Add(
                    new Arc(
                        aa,
                        ba,
                        ArcType.Track,
                        new TrackSwitchContainer(
                            track,
                            0,
                            Side.None,
                            new Infrastructure[1] { track }
                        )
                    )
                );
                bb.Arcs.Add(
                    new Arc(
                        bb,
                        ab,
                        ArcType.Track,
                        new TrackSwitchContainer(
                            track,
                            0,
                            Side.None,
                            new Infrastructure[1] { track }
                        )
                    )
                );

                // Add reversal arcs
                if (track.CanReverse)
                {
                    aa.Arcs.Add(
                        new Arc(
                            aa,
                            ab,
                            ArcType.Reverse,
                            new TrackSwitchContainer(
                                track,
                                0,
                                Side.None,
                                new Infrastructure[1] { track }
                            )
                        )
                    );
                    ab.Arcs.Add(
                        new Arc(
                            ab,
                            aa,
                            ArcType.Reverse,
                            new TrackSwitchContainer(
                                track,
                                0,
                                Side.None,
                                new Infrastructure[1] { track }
                            )
                        )
                    );
                    bb.Arcs.Add(
                        new Arc(
                            bb,
                            ba,
                            ArcType.Reverse,
                            new TrackSwitchContainer(
                                track,
                                0,
                                Side.None,
                                new Infrastructure[1] { track }
                            )
                        )
                    );
                    ba.Arcs.Add(
                        new Arc(
                            ba,
                            bb,
                            ArcType.Reverse,
                            new TrackSwitchContainer(
                                track,
                                0,
                                Side.None,
                                new Infrastructure[1] { track }
                            )
                        )
                    );
                }
            }

            List<Arc> arcs = [];
            foreach (Track track in ProblemInstance.Current.Tracks)
            {
                if (!track.IsActive)
                    continue;

                SuperVertex v = supervertices[track.Index];
                var tmp = track.GetConnectionsAtSide(Side.A).Count;

                if (track.Access.HasFlag(Side.A))
                {
                    foreach (var connection in track.GetConnectionsAtSide(Side.A))
                    {
                        SuperVertex w = supervertices[connection.Track.Index];

                        // Determine which side of the connected track we are connected to.
                        if (connection.Side == Side.A)
                            v.AB.Arcs.Add(new Arc(v.AB, w.AA, ArcType.Switch, connection));
                        else
                            v.AB.Arcs.Add(new Arc(v.AB, w.BB, ArcType.Switch, connection));
                    }

                    arcs.AddRange(v.AB.Arcs);
                }

                if (track.Access.HasFlag(Side.B))
                {
                    foreach (var connection in track.GetConnectionsAtSide(Side.B))
                    {
                        SuperVertex w = supervertices[connection.Track.Index];

                        // Determine which side of the connected track we are connected to.
                        if (connection.Side == Side.A)
                            v.BA.Arcs.Add(new Arc(v.BA, w.AA, ArcType.Switch, connection));
                        else
                            v.BA.Arcs.Add(new Arc(v.BA, w.BB, ArcType.Switch, connection));
                    }
                    arcs.AddRange(v.BA.Arcs);
                }
            }

            return new RoutingGraph(supervertices);
        }

        public void SetTrackOccupation(Track track, Parking.TrackOccupation occupation) =>
            this.SuperVertices[track.Index].TrackOccupation = occupation;

        // The Vertex a Track+Side pair actually refers to for routing purposes.
        // Rest targets the train's resting vertex (AA/BB, ArrivalSide==Track-
        // Side) -- what every ordinary RoutingTask still wants, since any
        // orientation it ends up in gets corrected by the next leg's own
        // route. ReadyToDepart targets the departure-ready vertex instead
        // (AB/BA), for the one leg with no next leg to hand a correction to
        // (#51).
        private (Vertex Origin, Vertex Destination) ResolveEndpoints(
            Track departureTrack,
            Side originSide,
            Track arrivalTrack,
            Side arrivalSide,
            RouteDestination destination
        )
        {
            SuperVertex start = this.SuperVertices[departureTrack.Index];
            SuperVertex end = this.SuperVertices[arrivalTrack.Index];
            Vertex destinationVertex =
                destination == RouteDestination.ReadyToDepart
                    ? (arrivalSide == Side.A ? end.AB : end.BA)
                    : (arrivalSide == Side.A ? end.AA : end.BB);
            return (originSide == Side.A ? start.AA : start.BB, destinationVertex);
        }

        public Route ComputeRoute(
            IEnumerable<Parking.TrackOccupation> occupations,
            ShuntTrain train,
            Track departureTrack,
            Side originSide,
            Track arrivalTrack,
            Side arrivalSide,
            RouteDestination destination = RouteDestination.Rest
        ) =>
            this.ComputeRoute(
                occupations,
                train,
                departureTrack,
                originSide,
                arrivalTrack,
                arrivalSide,
                null,
                destination
            );

        public Route ComputeRoute(
            IEnumerable<Parking.TrackOccupation> occupations,
            ShuntTrain train,
            Track departureTrack,
            Side originSide,
            Track arrivalTrack,
            Side arrivalSide,
            BitSet bitstate,
            RouteDestination destination = RouteDestination.Rest
        )
        {
            var (origin, destinationVertex) = this.ResolveEndpoints(
                departureTrack,
                originSide,
                arrivalTrack,
                arrivalSide,
                destination
            );

            if (origin == destinationVertex)
                return Route.EmptyRoute(train, this, departureTrack, originSide);

            Route route = null;
            var storage = this.storages[departureTrack.Index, arrivalTrack.Index];
            bitstate ??= storage.ConstructState(occupations, train);
            if (!storage.TryGet(originSide, arrivalSide, bitstate, destination, out route))
            {
                route = this.Dijkstra(train, origin, destinationVertex, destination);
                storage.Add(originSide, arrivalSide, bitstate, destination, route);
                return route;
            }

            route = new Route(train, route);
            route.TrackState = bitstate;
            route.RefreshArcsForTrain();
            route.ComputeDuration();
            return route;
        }

        public bool RoutePossible(
            ShuntTrain train,
            Track departureTrack,
            Side originSide,
            Track arrivalTrack,
            Side arrivalSide,
            RouteDestination destinationMode = RouteDestination.Rest
        )
        {
            var (origin, destination) = this.ResolveEndpoints(
                departureTrack,
                originSide,
                arrivalTrack,
                arrivalSide,
                destinationMode
            );

            if (origin == destination)
                return true;

            return this.SwitchCount[destination.Index][origin.Index]
                < Settings.SwitchesIfInvalidRoute;
        }

        private Route Dijkstra(
            ShuntTrain train,
            Vertex start,
            Vertex end,
            RouteDestination destination,
            bool useEstimate = true
        )
        {
            Debug.Assert(
                start != end,
                "Dijkstra must not be called with start == end -- callers (ComputeRoute/"
                    + "RoutePossible) must return Route.EmptyRoute/true directly instead, or "
                    + "the zero-distance case gets charged a spurious TrackCrossingTime"
            );

            // Reset only what the previous call actually touched, not every Vertex --
            // Dijkstra sits on ComputeRoute's cache-miss path, which local search hits
            // often enough (LocalSearchMove.cs's DebugCheckInterval comment measures
            // ~2500 candidate moves/sec on a modest 30-train scenario) that an O(V)
            // sweep per call is worth avoiding. A vertex is only ever discovered,
            // explored, or given a Previous below when it's added to touchedVertices,
            // so resetting exactly that set is equivalent to sweeping all Vertices.
            foreach (Vertex v in this.touchedVertices)
            {
                v.Discovered = v.Explored = false;
                v.Previous = null;
            }
            this.touchedVertices.Clear();
            this.priorityqueue.Clear();

            int[] switchcount = this.SwitchCount[end.Index],
                reversalcount = this.ReversalCount[end.Index],
                trackcount = this.TrackCount[end.Index];

            start.Distance = 0;
            start.Discovered = true;
            this.touchedVertices.Add(start);
            priorityqueue.Enqueue(
                start,
                (
                    useEstimate
                        ? (int)ComputeEstimate(
                            train,
                            start.Index,
                            switchcount,
                            trackcount,
                            reversalcount
                        )
                        : 0
                )
            );

            while (priorityqueue.Count > 0)
            {
                Vertex vertex = priorityqueue.Dequeue();
                vertex.Explored = true;

                if (vertex == end)
                    break;

                foreach (Arc arc in vertex.Arcs)
                {
                    Vertex neighbor = arc.Head;
                    if (neighbor.Explored)
                        continue;

                    arc.ComputeCost(train);
                    if (!neighbor.Discovered || neighbor.Distance > vertex.Distance + arc.Cost)
                    {
                        neighbor.Previous = arc;
                        neighbor.Distance = vertex.Distance + arc.Cost;

                        if (neighbor.Discovered)
                            priorityqueue.UpdatePriority(
                                neighbor,
                                neighbor.Distance
                                    + (
                                        useEstimate
                                            ? (int)ComputeEstimate(
                                                train,
                                                neighbor.Index,
                                                switchcount,
                                                trackcount,
                                                reversalcount
                                            )
                                            : 0
                                    )
                            );
                        else
                        {
                            priorityqueue.Enqueue(
                                neighbor,
                                neighbor.Distance
                                    + (
                                        useEstimate
                                            ? (int)ComputeEstimate(
                                                train,
                                                neighbor.Index,
                                                switchcount,
                                                trackcount,
                                                reversalcount
                                            )
                                            : 0
                                    )
                            );
                            neighbor.Discovered = true;
                            this.touchedVertices.Add(neighbor);
                        }
                    }
                }
            }

            if (!end.Discovered)
                return Route.Invalid;

            // Backtracking
            int crossings = 0;
            Vertex current = end;
            // A trailing Reverse arc landing on `end` is dropped for a Rest
            // destination. AA and AB (same for BB/BA) are the same physical
            // track, so a path that reaches AB first and then reverses into
            // AA (rather than some other, costlier path that reaches AA
            // directly) is only ever cheaper than stopping at AB in the
            // Reverse arc's own cost -- and nothing downstream reads that
            // choice back off this Route to tell AA and AB apart: the next
            // TrackTask's own ArrivalSide is set independently, by whichever
            // local-search move or heuristic step placed it there (see e.g.
            // ParkingSwitchMove/ParkingSwapMove), never derived from this
            // Route's Arcs. So the reversal would cost real time (and, once
            // emitted, a real Reverse action) for a distinction nothing
            // downstream ever queries -- keeping it would be paying for a
            // flip nobody asked for. For ReadyToDepart (#51) that argument
            // doesn't apply: AB/BA there isn't standing in for the same
            // physical stop as AA/BB, it's the actual destination the caller
            // requested (this is the one leg with no next task to read an
            // "ArrivalSide" from at all), so the trailing Reverse is kept.
            if (destination == RouteDestination.Rest && current.Previous?.Type == ArcType.Reverse)
                current = current.Previous.Tail;
            Stack<Track> route = new();
            Stack<Arc> arcs = new();
            int switches = 0;
            int reversals = 0;
            BitSet crossingtracks = new(ProblemInstance.Current.Tracks.Length);
            while (true)
            {
                if (route.Count == 0 || route.Peek() != current.SuperVertex.Track)
                    route.Push(current.SuperVertex.Track);

                if (current == start)
                    break;

                arcs.Push(current.Previous);
                crossings += current.Previous.Crossings;
                switches += current.Previous.Switches;
                if (current.Previous.Type == ArcType.Reverse)
                    reversals++;
                if (current.Previous.Crossings > 0)
                    crossingtracks[current.SuperVertex.Track.Index] = true;

                current = current.Previous.Tail;
            }

            Arc[] arcArray = arcs.ToArray();

            // The route's effective departure side isn't an input any more (the search
            // starts from wherever the train truly rests, AA/BB) -- it's discovered from
            // the path: whichever side's switch arcs the path actually leaves through.
            // A route that never leaves the origin track via a Switch (a same-track
            // reposition) has no such arc to read, so it falls back to the TrackSide of
            // wherever the path ends up -- start and end are then on the same track, so
            // that's the only side there is to report.
            Arc firstSwitch = Array.Find(arcArray, arc => arc.Type == ArcType.Switch);
            Debug.Assert(
                firstSwitch != null || start.SuperVertex == end.SuperVertex,
                "A route with no Switch arc must stay on a single Track -- Track/Reverse arcs "
                    + "never cross SuperVertices, only Switch arcs do -- otherwise the "
                    + "end.TrackSide fallback below reports the wrong side"
            );
            Side departureside = firstSwitch != null ? firstSwitch.Tail.TrackSide : end.TrackSide;

            return new Route(
                train,
                this,
                route.ToArray(),
                arcArray,
                crossings,
                crossingtracks,
                departureside,
                switches,
                reversals
            );
        }

        private static Time ComputeEstimate(
            ShuntTrain train,
            int index,
            int[] switchcount,
            int[] trackcount,
            int[] reversalcount
        )
        {
            int reversals = reversalcount[index];
            Time result =
                switchcount[index] * Settings.SwitchCrossingTime
                + (reversals + trackcount[index]) * Settings.TrackCrossingTime
                + reversals * train.ReversalDuration;
            return result;
        }
    }
}
