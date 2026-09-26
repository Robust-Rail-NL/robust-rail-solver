using System.Collections.Immutable;
using System.Diagnostics;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Routing
{
    class Storage
    {
        private readonly int bitsize;
        private Dictionary<BitSet, Entry> AA;
        private Dictionary<BitSet, Entry> AB;
        private Dictionary<BitSet, Entry> BA;
        private Dictionary<BitSet, Entry> BB;

        // Rest and ReadyToDepart (#51) target different vertices (AA/BB vs
        // AB/BA) for the same (from, to) side pair, so they need their own
        // cache buckets -- otherwise a Rest-mode lookup could hand back a
        // ReadyToDepart route, or vice versa.
        private Dictionary<BitSet, Entry> ReadyAA;
        private Dictionary<BitSet, Entry> ReadyAB;
        private Dictionary<BitSet, Entry> ReadyBA;
        private Dictionary<BitSet, Entry> ReadyBB;
        private readonly ImmutableArray<int> indices;
        private const int maxsize = 10000;
        private LinkedList<BitSet> AAhistory,
            ABhistory,
            BAhistory,
            BBhistory;
        private LinkedList<BitSet> ReadyAAhistory,
            ReadyABhistory,
            ReadyBAhistory,
            ReadyBBhistory;

        public TrackParts.Track From { get; private set; }
        public TrackParts.Track To { get; private set; }
        public BitSet EmptyState { get; private set; }

        // Computing this once per RoutingGraph (not once per Storage) turns an O(N)
        // allocation duplicated across all O(N^2) Storages -- O(N^3) total -- into a
        // single O(N) array shared by all of them. Depends only on
        // ProblemInstance.Current.Tracks, so every Storage built from the same
        // ProblemInstance gets an identical result.
        public static (ImmutableArray<int> Indices, int BitSize) ComputeIndices()
        {
            var indices = new int[ProblemInstance.Current.Tracks.Length];
            var activetracks = ProblemInstance.Current.Tracks.Where(track => track.IsActive);
            int index = 0;
            foreach (var track in activetracks)
            {
                indices[track.Index] = index;
                if (track.Access == Side.Both)
                    index += 2;
                else
                    index++;
            }

            return (ImmutableArray.Create(indices), index);
        }

        public Storage(
            TrackParts.Track from,
            TrackParts.Track to,
            ImmutableArray<int> indices,
            int bitsize
        )
        {
            this.From = from;
            this.To = to;

            this.indices = indices;
            this.bitsize = bitsize;
            this.AA = [];
            this.AAhistory = new LinkedList<BitSet>();
            this.AB = [];
            this.ABhistory = new LinkedList<BitSet>();
            this.BA = [];
            this.BAhistory = new LinkedList<BitSet>();
            this.BB = [];
            this.BBhistory = new LinkedList<BitSet>();

            this.ReadyAA = [];
            this.ReadyAAhistory = new LinkedList<BitSet>();
            this.ReadyAB = [];
            this.ReadyABhistory = new LinkedList<BitSet>();
            this.ReadyBA = [];
            this.ReadyBAhistory = new LinkedList<BitSet>();
            this.ReadyBB = [];
            this.ReadyBBhistory = new LinkedList<BitSet>();

            this.EmptyState = new BitSet(this.bitsize);
        }

        public BitSet ConstructState(
            IEnumerable<Parking.TrackOccupation> trackstates,
            Trains.ShuntTrain train
        )
        {
            BitSet result = new(bitsize);

            foreach (var state in trackstates)
            {
                if (state == null)
                    continue;

                int index = this.indices[state.Track.Index];
                if (state.CountCrossingsIfTurning(train, Side.A) > 0)
                    result[index] = true;

                if (state.Track.Access == Side.Both && state.StateDeque.Count > 0)
                    result[index + 1] = true;
            }

            return result;
        }

        public bool TryGet(
            Side from,
            Side to,
            BitSet state,
            RouteDestination destinationMode,
            out Route route
        )
        {
            var (hashmap, history) = SelectDictAndList(
                from,
                to,
                destinationMode == RouteDestination.ReadyToDepart
            );
            return tryGetValue(hashmap, history, state, out route);
        }

        public void Add(
            Side from,
            Side to,
            BitSet state,
            RouteDestination destinationMode,
            Route route
        )
        {
            var (hashmap, history) = SelectDictAndList(
                from,
                to,
                destinationMode == RouteDestination.ReadyToDepart
            );
            add(hashmap, history, state, route);
        }

        // Picks which of the 8 (from, to, ready) cache buckets TryGet/Add
        // should use. Only one place, not duplicated across both callers, so
        // a future bug fix or added mode can't be applied to one and
        // forgotten in the other.
        private (Dictionary<BitSet, Entry> Dict, LinkedList<BitSet> History) SelectDictAndList(
            Side from,
            Side to,
            bool ready
        )
        {
            if (from == Side.A)
            {
                if (to == Side.A)
                    return ready ? (this.ReadyAA, this.ReadyAAhistory) : (this.AA, this.AAhistory);

                Debug.Assert(to == Side.B, $"Storage's `to` must be A or B, was {to}");
                return ready ? (this.ReadyAB, this.ReadyABhistory) : (this.AB, this.ABhistory);
            }
            else
            {
                // Not asserted here: unlike `to`, `from` legitimately carries
                // Side.None for an InStanding train's true resting side (see
                // ResolveEndpoints' matching comment) -- a real value, not a bug.
                if (to == Side.A)
                    return ready ? (this.ReadyBA, this.ReadyBAhistory) : (this.BA, this.BAhistory);

                Debug.Assert(to == Side.B, $"Storage's `to` must be A or B, was {to}");
                return ready ? (this.ReadyBB, this.ReadyBBhistory) : (this.BB, this.BBhistory);
            }
        }

        private static bool tryGetValue(
            Dictionary<BitSet, Entry> hashmap,
            LinkedList<BitSet> history,
            BitSet key,
            out Route value
        )
        {
            Entry entry = default(Entry);
            bool success = hashmap.TryGetValue(key, out entry);

            if (success)
            {
                value = entry.Route;
                history.Remove(entry.Node);
                history.AddLast(entry.Node);
            }
            else
                value = null;
            return success;
        }

        private static void add(
            Dictionary<BitSet, Entry> hashmap,
            LinkedList<BitSet> history,
            BitSet key,
            Route value
        )
        {
            hashmap[key] = new Entry(value, history.AddLast(key));
            if (history.Count > maxsize)
            {
                hashmap.Remove(history.First.Value);
                history.RemoveFirst();
            }
        }

        public override string ToString()
        {
            return $"{this.From.ID}->{this.To.ID} :  A->A={this.AA.Count}, A->B={this.AB.Count}, B->A={this.BA.Count}, B->B={this.BB.Count}";
        }

        public string BitStateToString(BitSet state)
        {
            string result = string.Empty;
            foreach (var track in ProblemInstance.Current.Tracks)
                if (track.IsActive)
                    result +=
                        $"{track.PrettyName} = {state[this.indices[track.Index]]} {(track.Access == Side.Both ? state[this.indices[track.Index] + 1].ToString() : "?")} \r\n";
            return result;
        }

        private struct Entry
        {
            public Route Route { get; }
            public LinkedListNode<BitSet> Node { get; }

            public Entry(Route route, LinkedListNode<BitSet> node)
            {
                this.Route = route;
                this.Node = node;
            }

            public override string ToString()
            {
                return this.Route.ToString();
            }
        }
    }
}
