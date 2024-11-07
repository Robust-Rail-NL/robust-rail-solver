using ServiceSiteScheduling.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Routing
{
    struct Cost
    {
        public Time Time { get; private set; }
        public int Crossings { get; private set; }
        public int Value { get; private set; }

        public Cost(Time time, int crossings)
        {
            this.Time = time;
            this.Crossings = crossings;
            this.Value = this.Time + Settings.CrossingWeight * crossings;
        }

        public override string ToString()
        {
            return $"{this.Value} = {this.Time} + {this.Crossings} x {Settings.CrossingWeight}";
        }
    }
}
