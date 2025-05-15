using ServiceSiteScheduling.TrackParts;

namespace ServiceSiteScheduling.Routing
{
    class SuperVertex
    {
        public Track Track;
        public Vertex AA, AB, BA, BB;
        public Vertex[] SubVertices;
        public Parking.TrackOccupation TrackOccupation;
        public int Index;

        public SuperVertex(Track track, Vertex aa, Vertex ab, Vertex ba, Vertex bb, int index)
        {
            this.Track = track;
            this.AA = aa;
            this.AB = ab;
            this.BA = ba;
            this.BB = bb;
            this.Index = index;

            this.SubVertices = new Vertex[4] { aa, ab, ba, bb };
            aa.Index = 4 * index;
            ab.Index = 4 * index + 1;
            ba.Index = 4 * index + 2;
            bb.Index = 4 * index + 3;
        }

        public Vertex GetVertex(Side trackside, Side arrivalside)
        {
            if (trackside == Side.A)
            {
                if (arrivalside == Side.A)
                    return this.AA;
                else
                    return this.AB;
            }
            else
            {
                if (arrivalside == Side.A)
                    return this.BA;
                else
                    return this.BB;
            }
        }

        public override string ToString()
        {
            return this.Track.ID.ToString();
        }
    }
}
