using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Routing
{
    class Storage
    {
        private int bitsize;
        private Dictionary<BitSet, Entry> AA;
        private Dictionary<BitSet, Entry> AB;
        private Dictionary<BitSet, Entry> BA;
        private Dictionary<BitSet, Entry> BB;
        private int[] indices;
        private const int maxsize = 10000;
        private LinkedList<BitSet> AAhistory, ABhistory, BAhistory, BBhistory;

        public TrackParts.Track From { get; private set; }
        public TrackParts.Track To { get; private set; }
        public BitSet EmptyState { get; private set; }

        public Storage(TrackParts.Track from, TrackParts.Track to)
        {
            this.From = from;
            this.To = to;

            this.indices = new int[ProblemInstance.Current.Tracks.Length];
            var activetracks = ProblemInstance.Current.Tracks.Where(track => track.IsActive);
            int index = 0;
            foreach (var track in activetracks)
            {
                this.indices[track.Index] = index;
                if (track.Access == Side.Both)
                    index += 2;
                else
                    index++;
            }

            this.bitsize = index;
            this.AA = new Dictionary<BitSet, Entry>();
            this.AAhistory = new LinkedList<BitSet>();
            this.AB = new Dictionary<BitSet, Entry>();
            this.ABhistory = new LinkedList<BitSet>();
            this.BA = new Dictionary<BitSet, Entry>();
            this.BAhistory = new LinkedList<BitSet>();
            this.BB = new Dictionary<BitSet, Entry>();
            this.BBhistory = new LinkedList<BitSet>();

            this.EmptyState = new BitSet(this.bitsize);
        }

        public BitSet ConstructState(Parking.TrackOccupation[] trackstates, Trains.ShuntTrain train)
        {
            BitSet result = new BitSet(bitsize);

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

        public bool TryGet(Side from, Side to, BitSet state, out Route route)
        {
            if (from == Side.A)
            {
                if (to == Side.A)
                    return this.tryGetValue(this.AA, this.AAhistory, state, out route);
                else
                    return this.tryGetValue(this.AB, this.ABhistory, state, out route);
            }
            else
            {
                if (to == Side.A)
                    return this.tryGetValue(this.BA, this.BAhistory, state, out route);
                else
                    return this.tryGetValue(this.BB, this.BBhistory, state, out route);
            }
        }

        public void Add(Side from, Side to, BitSet state, Route route)
        {
            if (from == Side.A)
            {
                if (to == Side.A)
                    add(this.AA, this.AAhistory, state, route);
                else
                    add(this.AB, this.ABhistory, state, route);
            }
            else
            {
                if (to == Side.A)
                    add(this.BA, this.BAhistory, state, route);
                else
                    add(this.BB, this.BBhistory, state, route);
            }
        }

        private bool tryGetValue(Dictionary<BitSet, Entry> hashmap, LinkedList<BitSet> history, BitSet key, out Route value)
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

        private void add(Dictionary<BitSet, Entry> hashmap, LinkedList<BitSet> history, BitSet key, Route value)
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
                    result += $"{track.PrettyName} = {state[this.indices[track.Index]]} {(track.Access == Side.Both ? state[this.indices[track.Index] + 1].ToString() : "?")} \r\n";
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
