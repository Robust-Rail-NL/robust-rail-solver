namespace ServiceSiteScheduling.TrackParts
{
    class HalfEnglishSwitch : Connection
    {
        public HalfEnglishSwitch(ulong id, string name) : base(id, name) { this.cost = 2; }

        public HalfEnglishSwitch(ulong id, string name, Infrastructure doubleA, Infrastructure doubleB, Infrastructure singleA, Infrastructure singleB) : base(id, name)
        {
            this.Connect(doubleA, doubleB, singleA, singleB);
        }

        public void Connect(Infrastructure doubleA, Infrastructure doubleB, Infrastructure singleA, Infrastructure singleB)
        {
            this.Connections[doubleA] = new Infrastructure[2] { doubleB, singleB };
            this.Connections[singleA] = new Infrastructure[1] { doubleB };
            this.Connections[doubleB] = new Infrastructure[2] { doubleA, singleA };
            this.Connections[singleB] = new Infrastructure[1] { doubleA };
        }
    }
}
