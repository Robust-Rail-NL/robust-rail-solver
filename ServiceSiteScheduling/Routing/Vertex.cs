using Priority_Queue;
using ServiceSiteScheduling.TrackParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Routing
{
    class Vertex : FastPriorityQueueNode
    {
        public SuperVertex SuperVertex { get; set; }
        public Side TrackSide { get; private set; }
        public Side ArrivalSide { get; private set; }
        public List<Arc> Arcs { get; private set; }

        public Arc Previous;
        public int Index, Distance;
        public bool Discovered, Explored;

        public Vertex(Side trackside, Side arrivalside)
        {
            this.TrackSide = trackside;
            this.ArrivalSide = arrivalside;
            this.Arcs = new List<Arc>();
        }

        public override string ToString()
        {
            return $"{this.SuperVertex.Track.PrettyName}{this.TrackSide}{this.ArrivalSide}";
        }
    }
}
