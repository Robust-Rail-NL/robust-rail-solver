using ServiceSiteScheduling.TrackParts;
using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Trains
{
    class TrainType
    {
        public int Index { get; private set; }
        public string Name { get; private set; }
        public int Length { get; private set; }

        public Track[] ParkingLocations { get; private set; }
        public Time BaseReversalDuration { get; set; }
        public Time VariableReversalDuration { get; set; }
        public Time CombineDuration { get; set; }
        public Time SplitDuration { get; set; }

        public TrainType(
            int index,
            string name,
            int length,
            Track[] locations,
            Time reversalbase,
            Time reversalvariable,
            Time combineduration,
            Time splitduration
        )
        {
            if (reversalbase.Seconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(reversalbase),
                    reversalbase.Seconds,
                    "Reversal base duration must be non-negative."
                );
            if (reversalvariable.Seconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(reversalvariable),
                    reversalvariable.Seconds,
                    "Reversal variable duration must be non-negative."
                );
            if (combineduration.Seconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(combineduration),
                    combineduration.Seconds,
                    "Combine duration must be non-negative."
                );
            if (splitduration.Seconds < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(splitduration),
                    splitduration.Seconds,
                    "Split duration must be non-negative."
                );

            this.Index = index;
            this.Name = name;
            this.Length = length;
            this.ParkingLocations = locations;
            this.BaseReversalDuration = reversalbase;
            this.VariableReversalDuration = reversalvariable;
            this.CombineDuration = combineduration;
            this.SplitDuration = splitduration;
        }

        public override string ToString()
        {
            return $"({this.Index}) {this.Name} = {this.Length}m";
        }
    }
}
