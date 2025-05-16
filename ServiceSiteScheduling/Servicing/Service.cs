﻿
namespace ServiceSiteScheduling.Servicing
{
    class Service
    {
        public ServiceType Type { get; private set; }
        public Utilities.Time Duration { get; private set; }

        public Service(ServiceType type, Utilities.Time duration)
        {
            this.Type = type;
            this.Duration = duration;
        }

        public override string ToString()
        {
            return $"{this.Type.Name} {this.Duration}";
        }
    }
}
