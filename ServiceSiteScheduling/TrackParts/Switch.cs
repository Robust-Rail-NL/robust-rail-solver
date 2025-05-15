
namespace ServiceSiteScheduling.TrackParts
{
    class Switch : Connection
    {
        public Switch(ulong id, string name) : base(id, name) { }

        public Switch(ulong id, string name, Infrastructure permanent, IList<Infrastructure> variable) : base(id, name)
        {
            this.Connect(permanent, variable);
        }

        public void Connect(Infrastructure permanent, IList<Infrastructure> variable)
        {
            this.Connections[permanent] = variable;
            var connection = new Infrastructure[1] { permanent };
            foreach (var infra in variable)
                this.Connections[infra] = connection;
        }
    }
}
