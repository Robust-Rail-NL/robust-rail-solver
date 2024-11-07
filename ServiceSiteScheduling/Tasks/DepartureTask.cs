using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Tasks
{
    class DepartureTask : TrackTask, IFixedSchedule
    {
        public Utilities.Time ScheduledTime { get; set; }
        public Side DepartureSide { get; private set; }

        public DepartureTask(Trains.ShuntTrain train, TrackParts.Track track, Side side, Utilities.Time time) : base(train, track, TrackTaskType.Departure)
        {
            this.ScheduledTime = time;
            this.Start = this.End = time;
            this.DepartureSide = side;
        }
    }
}
