using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Solutions
{
    class SolutionCost
    {
        public int Crossings;
        public int ArrivalDelays;
        public int ArrivalDelaySum;
        public int DepartureDelays;
        public int DepartureDelaySum;
        public int TrackLengthViolations;
        public int TrackLengthViolationSum;
        public double ShuntMoves;
        public int RoutingDurationSum;
        public int CombineOnDepartureTrack;
        public int UnplannedMaintenance;

        public static double CrossingWeight = 10;
        public static double ArrivalDelayWeight = 20;
        public static double ArrivalDelaySumWeight = 0.03;
        public static double DepartureDelayWeight = 50;
        public static double DepartureDelaySumWeight = 0.05;
        public static double TrackWeight = 15;
        public static double TrackSumWeight = 0.05;
        public static double ShuntWeight = 0.02;
        public static double RoutingWeight = 0.00001;
        public static double CombineDepartureWeight = 20;
        public static double MaintenanceWeight = 10;

        public BitSet ProblemTracks;
        public BitSet ProblemTrains;

        public bool IsFeasible { get { return this.Crossings + this.ArrivalDelays + this.DepartureDelays + this.TrackLengthViolations + this.CombineOnDepartureTrack == 0; } }

        public bool IsDelayFeasible { get { return this.Crossings + this.TrackLengthViolations + this.CombineOnDepartureTrack == 0; } }

        public SolutionCost()
        {
            this.ProblemTracks = new BitSet(ProblemInstance.Current.Tracks.Length);
            this.ProblemTrains = new BitSet(ProblemInstance.Current.TrainUnits.Length);
        }

        public double BaseCost
        {
            get
            {
                return
                    CrossingWeight * this.Crossings +
                    DepartureDelayWeight * this.DepartureDelays +
                    DepartureDelaySumWeight * this.DepartureDelaySum / (this.DepartureDelays + 1) +
                    ArrivalDelayWeight * this.ArrivalDelays +
                    ArrivalDelaySumWeight * this.ArrivalDelaySum / (this.ArrivalDelays + 1) +
                    TrackWeight * this.TrackLengthViolations +
                    MaintenanceWeight * this.UnplannedMaintenance + 
                    CombineDepartureWeight * this.CombineOnDepartureTrack;
            }
        }

        public double FullCost
        {
            get
            {
                return
                    CrossingWeight * this.Crossings +
                    DepartureDelayWeight * this.DepartureDelays +
                    DepartureDelaySumWeight * this.DepartureDelaySum / (this.DepartureDelays + 1) +
                    ArrivalDelayWeight * this.ArrivalDelays +
                    ArrivalDelaySumWeight * this.ArrivalDelaySum / (this.ArrivalDelays + 1) +
                    TrackWeight * this.TrackLengthViolations +
                    TrackSumWeight * this.TrackLengthViolationSum +
                    CombineDepartureWeight * this.CombineOnDepartureTrack +
                    ShuntWeight * this.ShuntMoves +
                    MaintenanceWeight * this.UnplannedMaintenance +
                    RoutingWeight * this.RoutingDurationSum / this.ShuntMoves;
            }
        }

        public double Cost(bool full)
        {
            return full ? this.FullCost : this.BaseCost;
        }

        public override string ToString()
        {
            return string.Format("Cost = {0} : {8} | cr={1}, dd={2}, da={3}, tlv={4}, sm={5}, rd={6}, cd={7}, um={9}",
                this.BaseCost.ToString("N1"),
                this.Crossings,
                this.DepartureDelays,
                this.ArrivalDelays,
                this.TrackLengthViolations,
                this.ShuntMoves,
                (this.RoutingDurationSum / this.ShuntMoves).ToString("N2"),
                this.CombineOnDepartureTrack,
                this.FullCost.ToString("N2"),
                this.UnplannedMaintenance);
        }
    }
}
