using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Matching
{
    class Train : IEnumerable
    {
        public Unit[] Units { get; private set; }
        public Part[] Parts { get; set; }
        public Trains.DepartureTrain Departure { get; private set; }
        public Tasks.DepartureTask Task { get; set; }
        public Tasks.DepartureRoutingTask Routing { get; set; }

        public Train(Trains.DepartureTrain train, Unit[] units)
        {
            this.Departure = train;
            this.Units = units;
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < this.Units.Length; i++)
                yield return this.Units[i];
            yield break;
        }

        public override string ToString()
        {
            return $"{this.Departure.Time}: {string.Join("|", this.Units.Select(unit => unit.ToString()))}";
        }
    }
}
