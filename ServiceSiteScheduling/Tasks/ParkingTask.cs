using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Tasks
{
    class ParkingTask : TrackTask
    {
        public bool IsInserted { get; }

        public ParkingTask(Trains.ShuntTrain train, TrackParts.Track track, bool isinserted = false) : base(train, track, TrackTaskType.Parking)
        {
            this.IsInserted = isinserted;
        }
    }
}
