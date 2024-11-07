using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Tasks
{
    class ArrivalTask : TrackTask, IFixedSchedule
    {
        public Utilities.Time ScheduledTime { get; set; }

        public ArrivalTask(Trains.ShuntTrain train, TrackParts.Track track, Side side, Utilities.Time time) : base(train, track, TrackTaskType.Arrival)
        {
            this.ScheduledTime = time;
            this.Start = this.End = time;
            this.ArrivalSide = side;
        }
    }
}
