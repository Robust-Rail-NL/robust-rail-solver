
namespace ServiceSiteScheduling.TrackParts
{
    class Intersection : Connection
    {
        public Intersection(ulong id, string name) : base(id, name) { this.cost = 0; }
        

        public Intersection(ulong id, string name, Infrastructure A1, Infrastructure A2, Infrastructure B1, Infrastructure B2) : base(id, name)
        {
            this.Connect(A1, A2, B1, B2);
        }

        public void Connect(Infrastructure A1, Infrastructure A2, Infrastructure B1, Infrastructure B2)
        {
            this.Connections[A1] = new Infrastructure[1] { A2 };
            this.Connections[A2] = new Infrastructure[1] { A1 };
            this.Connections[B1] = new Infrastructure[1] { B2 };
            this.Connections[B2] = new Infrastructure[1] { B1 };
        }
    }
}
