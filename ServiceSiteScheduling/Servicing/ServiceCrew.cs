using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Servicing
{
    class ServiceCrew : ServiceResource
    {
        public ServiceCrew(string name, IEnumerable<ServiceType> types) : base(name, types)
        {
        }
    }
}
