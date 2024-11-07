using ServiceSiteScheduling.Tasks;
using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Trains
{
    class ShuntTrain : IEnumerable, IEquatable<ShuntTrain>
    {
        public int Length { get; private set; }
        public List<ShuntTrainUnit> Units { get; set; }
        public ShuntTrainUnit A { get { return this.Units[0]; } }
        public ShuntTrainUnit B { get { return this.Units[this.Units.Count - 1]; } }
        public BitSet UnitBits { get; set; }
        public Track[] ParkingLocations { get; set; }
        public Track[] RoutingLocations { get; set; }

        protected Time _serviceDuration = -1;
        public Time ServiceDuration
        {
            get
            {
                if (this._serviceDuration == -1)
                    this._serviceDuration = this.Units.Sum(u => u.RequiredServices.Sum(t => t.Duration));
                return this._serviceDuration;
            }
        }

        protected Time _reversalDuration = -1;
        public Time ReversalDuration
        {
            get
            {
                if (this._reversalDuration == -1)
                    this._reversalDuration = this.Units.Sum(u => u.Type.VariableReversalDuration) + this.Units[0].Type.BaseReversalDuration;
                return this._reversalDuration;
            }
        }

        public IEnumerable<TrainUnit> A2B
        {
            get
            {
                for (int i = 0; i < this.Units.Count; i++)
                    yield return this.Units[i];
                yield break;
            }
        }

        public IEnumerable<TrainUnit> B2A
        {
            get
            {
                for (int i = this.Units.Count - 1; i >= 0; i--)
                    yield return this.Units[i];
                yield break;
            }
        }

        public ShuntTrain(IEnumerable<ShuntTrainUnit> units)
        {
            this.Units = units.ToList();
            this.Length = units.Sum(unit => unit.Type.Length);

            List<Track> allowedparking = new List<Track>();
            IEnumerable<TrainType> types = units.Select(unit => unit.Type).Distinct();
            foreach (Track track in this.Units[0].Type.ParkingLocations)
                if (types.All(type => type.ParkingLocations.Contains(track)))
                    allowedparking.Add(track);
            this.ParkingLocations = allowedparking.ToArray();

            this.RoutingLocations = ProblemInstance.Current.Tracks;
            // Do something with it

            this.UnitBits = new BitSet(ProblemInstance.Current.TrainUnits.Length);
            foreach (var unit in units)
                this.UnitBits[unit.Index] = true;
        }

        public ShuntTrain(ShuntTrain train)
        {
            this.Units = new List<ShuntTrainUnit>(train.Units);
            this.Length = train.Length;
            this.ParkingLocations = train.ParkingLocations;
            this.RoutingLocations = train.RoutingLocations;
            this.UnitBits = train.UnitBits;
        }

        public IEnumerable<IEnumerable<ShuntTrainUnit>> OrderedOverlap(ShuntTrain other)
        {
            List<List<ShuntTrainUnit>> result = new List<List<ShuntTrainUnit>>();

            for (int i = 0; i < this.Units.Count; i++)
            {
                var unit = this.Units[i];
                for (int j = 0; j < other.Units.Count; j++)
                {
                    if (other.Units[j] == unit)
                    {

                        var units = new List<ShuntTrainUnit>();
                        while (i < this.Units.Count && j < other.Units.Count && this.Units[i] == this.Units[j])
                            units.Add(this.Units[i]);
                        result.Add(units);
                        i += result.Count - 1;
                        j += result.Count - 1;
                        break;
                    }
                }
            }

            return result;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < this.Units.Count; i++)
                yield return this.Units[i];
            yield break;
        }

        public override string ToString()
        {
            return $"({string.Join(",", this.Units.Select(unit => unit.Name))})";
        }

        public override bool Equals(object obj)
        {
            ShuntTrain other = obj as ShuntTrain;
            if (other == null)
                return false;

            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            return this.UnitBits.GetHashCode();
        }

        public bool Equals(ShuntTrain other)
        {
            if (other == null || other.Units == null || this.Units == null || this.Units.Count != other.Units.Count)
                return false;

            for (int i = 0; i < this.Units.Count; i++)
                if (this.Units[i] != other.Units[i])
                    return false;
            return true;
        }
    }
}
