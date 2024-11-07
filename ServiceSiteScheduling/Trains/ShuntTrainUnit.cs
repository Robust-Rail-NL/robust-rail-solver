using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceSiteScheduling.Servicing;
using ServiceSiteScheduling.Tasks;

namespace ServiceSiteScheduling.Trains
{
    class ShuntTrainUnit : TrainUnit
    {
        public TrainUnit Base { get; private set; }

        public ArrivalTask Arrival { get; set; }
        public DepartureTask Departure { get; set; }

        public RoutingTask Split { get; set; }
        public RoutingTask Combine { get; set; }

        public ShuntTrainUnit(TrainUnit unit) : base(unit.Name, unit.Index, unit.Type, unit.RequiredServices, ProblemInstance.Current.ServiceTypes)
        {
            this.Base = unit;
        }
    }
}
