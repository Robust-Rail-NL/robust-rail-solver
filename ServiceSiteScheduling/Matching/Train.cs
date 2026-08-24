#nullable enable

using System.Collections;

namespace ServiceSiteScheduling.Matching
{
    class Train(Trains.DepartureTrain train, Unit[] units) : IEnumerable
    {
        public Unit[] Units { get; private set; } = units;
        public Part[] Parts { get; set; } = null!;
        public Trains.DepartureTrain Departure { get; private set; } = train;
        public Tasks.TrackTask? Task { get; set; }
        public Tasks.DepartureRoutingTask? Routing { get; set; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < this.Units.Length; i++)
                yield return this.Units[i];
            yield break;
        }

        public override string ToString()
        {
            return $"{this.Departure.Time}: ({string.Join("|", this.Units.Select(unit => unit.ToString()))})";
        }
    }
}
