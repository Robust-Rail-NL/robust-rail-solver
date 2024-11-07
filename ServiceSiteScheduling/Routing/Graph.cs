using Priority_Queue;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ServiceSiteScheduling.Routing
{
    class RoutingGraph
    {
        public SuperVertex[] SuperVertices;
        public Vertex[] Vertices;
        public int[][] TrackCount;
        public int[][] ReversalCount;
        public int[][] SwitchCount;
        public Arc[,] ArcMatrix;

        protected FastPriorityQueue<Vertex> priorityqueue;
        protected Storage[,] storages;

        public RoutingGraph(SuperVertex[] supervertices)
        {
            this.SuperVertices = supervertices;
            this.storages = new Storage[ProblemInstance.Current.Tracks.Length, ProblemInstance.Current.Tracks.Length];
            for (int i = 0; i < ProblemInstance.Current.Tracks.Length; i++)
                for (int j = 0; j < ProblemInstance.Current.Tracks.Length; j++)
                    this.storages[i, j] = new Storage(ProblemInstance.Current.Tracks[i], ProblemInstance.Current.Tracks[j]);

            this.Vertices = new Vertex[4 * supervertices.Length];
            this.ArcMatrix = new Arc[this.Vertices.Length, this.Vertices.Length];
            for (int i = 0; i < supervertices.Length; i++)
                for (int j = 0; j < 4; j++)
                    this.Vertices[4 * i + j] = supervertices[i].SubVertices[j];

            foreach (Vertex vertex in this.Vertices)
                foreach (Arc arc in vertex.Arcs)
                    this.ArcMatrix[vertex.Index, arc.Head.Index] = arc;

            this.priorityqueue = new FastPriorityQueue<Vertex>(4 * supervertices.Length);

            ShuntTrain train = new ShuntTrain(new ShuntTrainUnit[] { new ShuntTrainUnit(new TrainUnit(-1, ProblemInstance.Current.TrainTypes[0], new Servicing.Service[0], ProblemInstance.Current.ServiceTypes)) });
            this.SwitchCount = new int[this.Vertices.Length][];
            this.TrackCount = new int[this.Vertices.Length][];
            this.ReversalCount = new int[this.Vertices.Length][];
            for (int i = 0; i < this.Vertices.Length; i++)
            {
                this.SwitchCount[i] = new int[this.Vertices.Length];
                this.TrackCount[i] = new int[this.Vertices.Length];
                this.ReversalCount[i] = new int[this.Vertices.Length];
            }

            for (int i = 0; i < this.Vertices.Length; i++)
            {
                var v = this.Vertices[i];
                for (int j = i + 1; j < this.Vertices.Length; j++)
                {
                    var w = this.Vertices[j];

                    if (v.ToString() == "Spoor906aBA" && w.SuperVertex.Track.CanPark)
                    {
                        int x = 25;
                    }

                    var route = this.Dijkstra(train, w, v, w.TrackSide, false);

                    this.ReversalCount[i][j] = route.TotalReversals;
                    this.SwitchCount[i][j] = route.TotalSwitches;
                    this.TrackCount[i][j] = route.Tracks.Length;

                    if (w.ArrivalSide != w.TrackSide && v.ArrivalSide == v.TrackSide)
                    {
                        var storage = this.storages[w.SuperVertex.Track.Index, v.SuperVertex.Track.Index];
                        storage.Add(w.TrackSide, v.TrackSide, storage.EmptyState, route);
                    }

                    route = this.Dijkstra(train, v, w, v.TrackSide, false);

                    this.ReversalCount[j][i] = route.TotalReversals;
                    this.SwitchCount[j][i] = route.TotalSwitches;
                    this.TrackCount[j][i] = route.Tracks.Length;

                    if (v.ArrivalSide != v.TrackSide && w.ArrivalSide == w.TrackSide)
                    {
                        var storage = this.storages[v.SuperVertex.Track.Index, w.SuperVertex.Track.Index];
                        storage.Add(v.TrackSide, w.TrackSide, storage.EmptyState, route);
                    }
                }
            }

            /*List<Route>[,] routes = new List<Route>[this.Vertices.Length, this.Vertices.Length];
            for (int i = 0; i < this.Vertices.Length; i++)
            {
                Vertex v = this.Vertices[i];
                if (!v.SuperVertex.Track.IsActive)
                    continue;

                for (int j = 0; j < this.Vertices.Length; j++)
                {
                    var list = new List<Route>();
                    Vertex w = this.Vertices[j];
                    if (!w.SuperVertex.Track.IsActive)
                        continue;

                    foreach (Vertex u in this.Vertices)
                        u.Discovered = false;
                    ComputeRoutes(v, w, (new Vertex[1] { v }).ToList(), list, v.TrackSide);
                    routes[i, j] = list;
                }
            }
            int count = 0;
            for (int i = 0; i < this.Vertices.Length; i++)
                for (int j = 0; j < this.Vertices.Length; j++)
                    count += routes[i, j]?.Count ?? 0;*/
        }

        public static RoutingGraph Construct()
        {
            SuperVertex[] supervertices = new SuperVertex[ProblemInstance.Current.Tracks.Length];

            foreach (Track track in ProblemInstance.Current.Tracks)
            {
                Vertex aa = new Vertex(Side.A, Side.A);
                Vertex ab = new Vertex(Side.A, Side.B);
                Vertex ba = new Vertex(Side.B, Side.A);
                Vertex bb = new Vertex(Side.B, Side.B);
                SuperVertex v = new SuperVertex(track, aa, ab, ba, bb, track.Index);
                supervertices[v.Index] = v;
                aa.SuperVertex = ab.SuperVertex = ba.SuperVertex = bb.SuperVertex = v;

                // Add track arcs
                aa.Arcs.Add(new Arc(aa, ba, ArcType.Track, new TrackSwitchContainer(track, 0, Side.None, new Infrastructure[1] { track })));
                bb.Arcs.Add(new Arc(bb, ab, ArcType.Track, new TrackSwitchContainer(track, 0, Side.None, new Infrastructure[1] { track })));

                // Add reversal arcs
                if (track.CanReverse)
                {
                    aa.Arcs.Add(new Arc(aa, ab, ArcType.Reverse, new TrackSwitchContainer(track, 0, Side.None, new Infrastructure[1] { track })));
                    ab.Arcs.Add(new Arc(ab, aa, ArcType.Reverse, new TrackSwitchContainer(track, 0, Side.None, new Infrastructure[1] { track })));
                    bb.Arcs.Add(new Arc(bb, ba, ArcType.Reverse, new TrackSwitchContainer(track, 0, Side.None, new Infrastructure[1] { track })));
                    ba.Arcs.Add(new Arc(ba, bb, ArcType.Reverse, new TrackSwitchContainer(track, 0, Side.None, new Infrastructure[1] { track })));
                }
            }

            List<Arc> arcs = new List<Arc>();
            foreach (Track track in ProblemInstance.Current.Tracks)
            {
                if (!track.IsActive)
                    continue;

                SuperVertex v = supervertices[track.Index];

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

        public Route ComputeRoute(Parking.TrackOccupation[] occupations, ShuntTrain train, Track departureTrack, Side departureSide, Track arrivalTrack, Side arrivalSide)
        {
            if (departureTrack == arrivalTrack && departureSide == arrivalSide)
                return Route.EmptyRoute(train, this, departureTrack, departureSide);

            Route route = null;
            var storage = this.storages[departureTrack.Index, arrivalTrack.Index];
            BitSet bitstate = storage.ConstructState(occupations, train);
            if (!storage.TryGet(departureSide, arrivalSide, bitstate, out route))
            {
                SuperVertex start = this.SuperVertices[departureTrack.Index];
                Vertex origin = departureSide == Side.A ? start.AB : start.BA;
                SuperVertex end = this.SuperVertices[arrivalTrack.Index];
                Vertex destination = arrivalSide == Side.A ? end.AA : end.BB;

                route = this.Dijkstra(train, origin, destination, departureSide);
                storage.Add(departureSide, arrivalSide, bitstate, route);
                return route;
            }

            route = new Route(train, route);
            route.TrackState = bitstate;
            route.computeDuration();
            return route;
        }

        public Route ComputeRoute(Parking.TrackOccupation[] occupations, ShuntTrain train, Track departureTrack, Side departureSide, Track arrivalTrack, Side arrivalSide, BitSet bitstate)
        {
            if (bitstate == null)
                return this.ComputeRoute(occupations, train, departureTrack, departureSide, arrivalTrack, arrivalSide);

            if (departureTrack == arrivalTrack && departureSide == arrivalSide)
                return Route.EmptyRoute(train, this, departureTrack, departureSide);

            Route route = null;
            var storage = this.storages[departureTrack.Index, arrivalTrack.Index];
            if (!storage.TryGet(departureSide, arrivalSide, bitstate, out route))
            {
                SuperVertex start = this.SuperVertices[departureTrack.Index];
                Vertex origin = departureSide == Side.A ? start.AB : start.BA;
                SuperVertex end = this.SuperVertices[arrivalTrack.Index];
                Vertex destination = arrivalSide == Side.A ? end.AA : end.BB;

                route = this.Dijkstra(train, origin, destination, departureSide);
                storage.Add(departureSide, arrivalSide, bitstate, route);
                return route;
            }

            route = new Route(train, route);
            route.TrackState = bitstate;
            route.computeDuration();
            return route;
        }

        public bool RoutePossible(ShuntTrain train, Track departureTrack, Side departureSide, Track arrivalTrack, Side arrivalSide)
        {
            if (departureTrack == arrivalTrack && departureSide == arrivalSide)
                return true;

            SuperVertex start = this.SuperVertices[departureTrack.Index];
            Vertex origin = departureSide == Side.A ? start.AB : start.BA;
            SuperVertex end = this.SuperVertices[arrivalTrack.Index];
            Vertex destination = arrivalSide == Side.A ? end.AA : end.BB;

            return this.SwitchCount[destination.Index][origin.Index] < Settings.SwitchesIfInvalidRoute;
        }

        protected Route Dijkstra(ShuntTrain train, Vertex start, Vertex end, Side departureside, bool useEstimate = true)
        {
            foreach (Vertex v in this.Vertices)
                v.Discovered = v.Explored = false;
            this.priorityqueue.Clear();

            int[] switchcount = this.SwitchCount[end.Index], reversalcount = this.ReversalCount[end.Index], trackcount = this.TrackCount[end.Index];

            start.Distance = 0;
            start.Discovered = true;
            priorityqueue.Enqueue(start, (useEstimate ? (int)this.ComputeEstimate(train, start.Index, switchcount, trackcount, reversalcount) : 0));

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
                            priorityqueue.UpdatePriority(neighbor, neighbor.Distance + (useEstimate ? (int)this.ComputeEstimate(train, neighbor.Index, switchcount, trackcount, reversalcount) : 0));
                        else
                        {
                            priorityqueue.Enqueue(neighbor, neighbor.Distance + (useEstimate ? (int)this.ComputeEstimate(train, neighbor.Index, switchcount, trackcount, reversalcount) : 0));
                            neighbor.Discovered = true;
                        }
                    }
                }
            }

            if (!end.Discovered)
                return Route.Invalid;

            // Backtracking
            int crossings = 0;
            Vertex current = end;
            if (current.Previous?.Type == ArcType.Reverse)
                current = current.Previous.Tail;
            Stack<Track> route = new Stack<Track>();
            Stack<Arc> arcs = new Stack<Arc>();
            int switches = 0;
            int reversals = 0;
            BitSet crossingtracks = new BitSet(ProblemInstance.Current.Tracks.Length);
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

            return new Route(train, this, route.ToArray(), arcs.ToArray(), crossings, crossingtracks, departureside, switches, reversals);
        }

        protected Time ComputeEstimate(ShuntTrain train, int index, int[] switchcount, int[] trackcount, int[] reversalcount)
        {
            int reversals = reversalcount[index];
            Time result = switchcount[index] * Settings.SwitchCrossingTime + (reversals + trackcount[index]) * Settings.TrackCrossingTime + reversals * train.ReversalDuration;
            return result;
        }
    }
}
