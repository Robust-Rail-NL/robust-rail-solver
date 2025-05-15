
namespace ServiceSiteScheduling.Servicing
{
    class ServiceCrew : ServiceResource
    {
        public ServiceCrew(string name, IEnumerable<ServiceType> types) : base(name, types)
        {
        }
    }
}
