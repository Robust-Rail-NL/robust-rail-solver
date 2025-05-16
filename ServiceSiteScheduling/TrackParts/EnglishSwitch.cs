
namespace ServiceSiteScheduling.TrackParts
{
    class EnglishSwitch : Connection
    {
        public EnglishSwitch(ulong id, string name) : base(id, name) { this.cost = 2; }

        public EnglishSwitch(ulong id, string name, IList<Infrastructure> A, IList<Infrastructure> B) : base(id, name)
        {
            this.Connect(A, B);
        }

        public void Connect(IList<Infrastructure> A, IList<Infrastructure> B)
        {
            foreach (var infra in A)
                this.Connections[infra] = B;
            foreach (var infra in B)
                this.Connections[infra] = A;
        }
    }
}
