
namespace ServiceSiteScheduling.Servicing
{
    class ServiceResource
    {
        public string Name { get; private set; }
        public IEnumerable<ServiceType> Types { get; private set; }
        public Tasks.ServiceTask First { get; set; }
        public Tasks.ServiceTask Last { get; set; }

        public ServiceResource(string name, IEnumerable<ServiceType> types)
        {
            this.Name = name;
            this.Types = types;
        }

        public override string ToString()
        {
            return $"{this.Name}";
        }
    }
}
