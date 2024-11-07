using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Matching
{
    class Part
    {
        public Unit[] Units { get; private set; }
        public Trains.TrainType[] Types { get { return this.Units.Select(unit => unit.Departure.Unit?.Type ?? unit.Departure.Type).ToArray(); } }
        public bool IsFixed { get { return this.Units.Any(unit => unit.IsFixed); } }
        public TrainMatching Matching { get; set; }
        public Train Train { get { return this.Units[0].Train; } }
        public Trains.DepartureTrain DepartureTrain { get { return this.Units[0].Departure.Train; } }

        public Part(Unit[] units)
        {
            this.Units = units;
        }
    }
}
