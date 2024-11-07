using ServiceSiteScheduling.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Routing
{
    static class Settings
    {
        public const int CrossingWeight = 3600;
        public const int SwitchesIfInvalidRoute = 120;
        public const int CrossingsIfInvalidRoute = 100;
        public static readonly Time SwitchCrossingTime = 30 * Time.Second;
        public static readonly Time TrackCrossingTime = 60 * Time.Second;
    }
}
