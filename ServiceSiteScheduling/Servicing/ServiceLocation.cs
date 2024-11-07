using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Servicing
{
    class ServiceLocation : ServiceResource
    {
        public TrackParts.Track Track { get; private set; }

        public ServiceLocation(string name, IEnumerable<ServiceType> types, TrackParts.Track track) : base(name, types)
        {
            this.Track = track;
        }
    }
}
