#nullable enable

using System.Diagnostics;
using ServiceSiteScheduling.Trains;

namespace ServiceSiteScheduling.Matching
{
    class TrainMatching
    {
        public Train[] DepartureTrains { get; private set; }
        public List<Part>[] DeparturePartsByType { get; private set; }

        private readonly IList<ShuntTrainUnit> shuntUnits;

        public TrainMatching(Train[] trains, IList<ShuntTrainUnit> shuntunits)
        {
            this.DepartureTrains = trains;
            this.shuntUnits = shuntunits;

            var bytype = new List<List<Part>>();
            foreach (Train train in trains)
            {
                foreach (Part part in train.Parts)
                {
                    List<Part>? parts = null;
                    foreach (var list in bytype)
                        if (list[0].Types.SequenceEqual(part.Types))
                        {
                            parts = list;
                            break;
                        }
                    if (parts == null)
                    {
                        parts = [];
                        bytype.Add(parts);
                    }

                    parts.Add(part);
                    part.Matching = this;
                }
            }
            Debug.Assert(bytype.All(l => l.All(p => p.Matching != null)));
            this.DeparturePartsByType = bytype.ToArray();
        }

        public ShuntTrain GetShuntTrain(Train departure)
        {
            return new ShuntTrain(
                departure.Units.Select(departureunit => this.shuntUnits[departureunit.Index]),
                departure.Departure.OutStanding
            );
        }
    }
}
