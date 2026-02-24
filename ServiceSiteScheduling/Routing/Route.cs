#nullable enable

using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Routing
{
    class Route : IEquatable<Route>
    {
        public ShuntTrain Train { get; private set; }
        public Track[] Tracks { get; private set; }
        public Arc[] Arcs { get; private set; }
        public BitSet TrackBits { get; private set; }
        public Time Duration { get; private set; }
        public int Crossings { get; private set; }
        public int DepartureCrossings { get; set; }
        public Side DepartureSide { get; private set; }
        public RoutingGraph Graph { get; private set; }
        public BitSet CrossingTracks { get; private set; }
        public BitSet? TrackState { get; set; }

        public static readonly Route Invalid = new(
            null!,
            [],
            [],
            Settings.CrossingsIfInvalidRoute,
            Side.None,
            Settings.SwitchesIfInvalidRoute,
            0
        );

        public int TotalSwitches { get; }
        public int TotalReversals { get; }

        private Route(
            RoutingGraph graph,
            Track[] tracks,
            Arc[] arcs,
            int crossings,
            Side side,
            int switches,
            int reversals
        )
        {
            this.Train = null!;
            this.Graph = graph;
            this.Tracks = tracks;
            this.Arcs = arcs;
            this.Crossings = crossings;
            this.DepartureSide = side;
            this.TotalSwitches = switches;
            this.TotalReversals = reversals;
            this.CrossingTracks = new BitSet(ProblemInstance.Current.Tracks.Length);

            this.TrackBits = new BitSet(ProblemInstance.Current.Tracks.Length);
            foreach (Track track in tracks)
                this.TrackBits[track.Index] = true;

            this.Duration = Time.Hour;
        }

        private Route(
            ShuntTrain train,
            RoutingGraph graph,
            Track[] tracks,
            Arc[] arcs,
            int crossings,
            Side side,
            int switches,
            int reversals
        )
            : this(graph, tracks, arcs, crossings, side, switches, reversals)
        {
            this.Train = train;
            this.ComputeDuration();
        }

        public Route(
            ShuntTrain train,
            RoutingGraph graph,
            Track[] tracks,
            Arc[] arcs,
            int crossings,
            BitSet crossingtracks,
            Side side,
            int switches,
            int reversals
        )
            : this(train, graph, tracks, arcs, crossings, side, switches, reversals)
        {
            this.CrossingTracks = crossingtracks;
        }

        public Route(ShuntTrain train, Route route)
        {
            this.Train = train;
            this.Graph = route.Graph;
            this.Tracks = route.Tracks;
            this.Arcs = route.Arcs;
            this.TrackBits = route.TrackBits;
            this.Duration = route.Duration;
            this.Crossings = route.Crossings;
            this.DepartureSide = route.DepartureSide;
            this.DepartureCrossings = route.DepartureCrossings;
            this.CrossingTracks = route.CrossingTracks;
            this.TotalSwitches = route.TotalSwitches;
            this.TotalReversals = route.TotalReversals;
        }

        public override string ToString()
        {
            return $"({this.Duration}|{this.DepartureCrossings}+{this.Crossings}) "
                + $"{string.Join("->", this.Tracks?.Select(track => track.PrettyName) ?? ["?"])}";
        }

        public Time ComputeDuration()
        {
            this.Duration =
                (this.Tracks.Length + this.TotalReversals) * Settings.TrackCrossingTime
                + this.TotalSwitches * Settings.SwitchCrossingTime
                + this.TotalReversals * this.Train.ReversalDuration;
            return this.Duration;
        }

        public static Route EmptyRoute(ShuntTrain train, RoutingGraph graph, Track track, Side side)
        {
            return new Route(train, graph, [track], [], 0, side, 0, 0) { Duration = 0 };
        }

        public bool Equals(Route? other)
        {
            return this.TrackBits.Equals(other?.TrackBits);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Route);
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
