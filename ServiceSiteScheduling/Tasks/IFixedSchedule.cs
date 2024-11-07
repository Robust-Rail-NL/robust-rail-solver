using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Tasks
{
    interface IFixedSchedule
    {
        Utilities.Time ScheduledTime { get; set; }
    }
}
