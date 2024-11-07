using ServiceSiteScheduling.Trains;
using ServiceSiteScheduling.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Matching
{
    static class Greedy
    {
        public static TrainMatching Construct(ShuntTrain[] shunttrains, IList<ShuntTrainUnit> shunttrainunits, Random random)
        {
            // Construct the departure trains
            Train[] departuretrains = new Train[ProblemInstance.Current.DeparturesOrdered.Length];
            List<Unit> departureunits = new List<Unit>();
            for (int i = 0; i < ProblemInstance.Current.DeparturesOrdered.Length; i++)
            {
                DepartureTrain departure = ProblemInstance.Current.DeparturesOrdered[i];
                Unit[] units = new Unit[departure.Units.Length];
                for (int j = 0; j < departure.Units.Length; j++)
                {
                    DepartureTrainUnit departuretrainunit = departure.Units[j];
                    var unit = new Unit(departuretrainunit);
                    units[j] = unit;
                    departureunits.Add(unit);
                }
                departuretrains[i] = new Train(departure, units);
                foreach (Unit unit in units)
                    unit.Train = departuretrains[i];
            }

            // Construct all subsets of trains
            List<ArrivalTrainPart> arrivals = new List<ArrivalTrainPart>();
            List<DepartureTrainPart> departures = new List<DepartureTrainPart>();

            for (int i = 0; i < shunttrains.Length; i++)
                arrivals.AddRange(GetArrivalSubsets(ProblemInstance.Current.ArrivalsOrdered[i], shunttrains[i]));
            for (int i = 0; i < departuretrains.Length; i++)
                departures.AddRange(GetDepartureSubsets(ProblemInstance.Current.DeparturesOrdered[i], departuretrains[i]));

            // Create bipartite graph
            Match[] possibleMatches = ConstructBipartiteGraph(arrivals, departures, ProblemInstance.Current.ArrivalsOrdered, ProblemInstance.Current.DeparturesOrdered);

            // Greedily select largest available subset
            List<Match> selectedMatches = new List<Match>();
            Comparison<Match> comparison = (a, b) =>
            {
                if (!a.Available && !b.Available)
                    return 0;
                if (!a.Available)
                    return 1;
                if (!b.Available)
                    return -1;

                var leastleftover = Math.Min(a.Arriving.Adjacent.Count(m => m.Available), a.Departing.Adjacent.Count(m => m.Available)).CompareTo(
                    Math.Min(b.Arriving.Adjacent.Count(m => m.Available), b.Departing.Adjacent.Count(m => m.Available)));

                if (leastleftover != 0)
                    return leastleftover;

                if (a.Size < b.Size)
                    return 1;
                if (b.Size < a.Size)
                    return -1;

                return 0;
            };

            Array.Sort(possibleMatches, comparison);
            while (possibleMatches[0].Available)
            {
                int selectedindex = 0;
                Match match = possibleMatches[selectedindex];
                while (selectedindex < possibleMatches.Length - 1 && random.NextDouble() < 0.5 && comparison(possibleMatches[selectedindex + 1], match) == 0)
                {
                    selectedindex++;
                    match = possibleMatches[selectedindex];
                }

                
                match.Arriving.MakeUnavailable();
                match.Departing.MakeUnavailable();
                selectedMatches.Add(match);

                Array.Sort(possibleMatches, comparison);
            }

            selectedMatches = selectedMatches.OrderBy(m => m.Arriving.Units.Min(u => u.Index)).ToList();

            Dictionary<Train, List<Part>> trainparts = new Dictionary<Train, List<Part>>();
            foreach (Match m in selectedMatches)
            {
                List<Part> parts = null;
                if (!trainparts.TryGetValue(m.Departing.TrainUnits[0].Train, out parts))
                    trainparts[m.Departing.TrainUnits[0].Train] = parts = new List<Part>();
                var part = new Part(m.Departing.TrainUnits);
                parts.Add(part);

                for (int i = 0; i < m.Size; i++)
                {
                    m.Departing.TrainUnits[i].Index = m.Arriving.Units[i].Index;
                    m.Departing.TrainUnits[i].Part = part;
                }
            }
            foreach (var kvp in trainparts)
                kvp.Key.Parts = kvp.Value.ToArray();

            TrainMatching result = new TrainMatching(departuretrains, departureunits, shunttrainunits);
            return result;
        }

        private static Match[] ConstructBipartiteGraph(IEnumerable<ArrivalTrainPart> arrivals, IEnumerable<DepartureTrainPart> departures, IList<ArrivalTrain> arrivaltrains, IList<DepartureTrain> departuretrains)
        {
            var unitmatching = ConstructUnitMatching(arrivaltrains, departuretrains);

            List <Match> result = new List<Match>();
            foreach (ArrivalTrainPart arriving in arrivals)
                foreach (DepartureTrainPart departing in departures)
                {
                    if (arriving.Units.Length != departing.Units.Length || arriving.Time >= departing.Time)
                        continue;

                    bool isMatch = true;
                    for (int i = 0; i < arriving.Units.Length && isMatch; i++)
                    {
                        var arrivalunit = arriving.Units[i];
                        var departureunit = departing.Units[i];

                        if ((departureunit.IsFixed && arrivalunit.Index != departureunit.Unit.Index) || (!departureunit.IsFixed && arrivalunit.Type != departureunit.Type))
                            isMatch = false;

                        if (!unitmatching[departureunit].Contains(arrivalunit))
                            isMatch = false;
                    }


                    if (isMatch)
                    {
                        Match match = new Match(arriving, departing);
                        result.Add(match);
                        arriving.Adjacent.Add(match);
                        departing.Adjacent.Add(match);
                    }
                }

            foreach (Match match in result.Where(m => m.Departing.Units.Any(unit => unit.IsFixed)).ToList())
            {
                foreach (var m in match.Arriving.Adjacent)
                {
                    if (m == match)
                        continue;
                    
                    m.Departing.Adjacent.Remove(m);
                    result.Remove(m);
                }
                match.Arriving.Adjacent.Clear();
                match.Arriving.Adjacent.Add(match);
            }
            return result.ToArray();
        }

        private static Dictionary<DepartureTrainUnit, List<TrainUnit>> ConstructUnitMatching(IList<ArrivalTrain> arrivals, IList<DepartureTrain> departures)
        {
            Dictionary<TrainType, List<UnitMatch>> matching = new Dictionary<TrainType, List<UnitMatch>>();
            foreach (DepartureTrain departuretrain in departures)
            {
                foreach (DepartureTrainUnit departureunit in departuretrain.Units)
                {
                    List<TrainUnit> possiblematches = new List<TrainUnit>();
                    foreach (ArrivalTrain arrivaltrain in arrivals)
                    {
                        if (arrivaltrain.Time > departuretrain.Time)
                            continue;

                        foreach (TrainUnit arrivalunit in arrivaltrain.Units)
                        {
                            if (departureunit.IsFixed && departureunit.Unit == arrivalunit)
                                possiblematches.Add(arrivalunit);
                            if (!departureunit.IsFixed && departureunit.Type == arrivalunit.Type)
                                possiblematches.Add(arrivalunit);
                        }
                    }

                    if (!matching.ContainsKey(departureunit.Unit?.Type ?? departureunit.Type))
                        matching[departureunit.Unit?.Type ?? departureunit.Type] = new List<UnitMatch>();
                    var list = matching[departureunit.Unit?.Type ?? departureunit.Type];
                    list.Insert(0, new UnitMatch(departureunit, possiblematches));
                }
            }


            Dictionary<DepartureTrainUnit, List<TrainUnit>> result = new Dictionary<DepartureTrainUnit, List<TrainUnit>>();
            foreach (var list in matching.Values)
            {
                bool change = true;
                while (change)
                {
                    change = false;
                    for (int length = 1; length < list.Count; length++)
                    {
                        if (change == true)
                            break;

                        for (int index = 0; index + length <= list.Count; index++)
                        {
                            if (length == 1)
                            {
                                if (list[index].ArrivalUnits.Count == length)
                                {
                                    var element = list[index].ArrivalUnits.First();
                                    result.Add(list[index].DepartureUnit, list[index].ArrivalUnits);
                                    list.RemoveAt(index);
                                    for (int i = 0; i < list.Count; i++)
                                    {
                                        if (list[i].ArrivalUnits.Contains(element))
                                            list[i].ArrivalUnits.Remove(element);
                                    }
                                    change = true;
                                    break;
                                }
                            }
                            else
                            {
                                var elements = list[index].ArrivalUnits;
                                if (elements.Count != length)
                                    continue;

                                bool same = true;
                                for (int i = index + 1; i < index + length && same; i++)
                                    same = elements.SequenceEqual(list[i].ArrivalUnits);
                                if (!same)
                                    continue;

                                for (int i = 0; i < length; i++)
                                {
                                    result.Add(list[index].DepartureUnit, list[index].ArrivalUnits);
                                    list.RemoveAt(index);
                                }
                                for (int i = 0; i < list.Count; i++)
                                {
                                    foreach (var element in elements)
                                        if (list[i].ArrivalUnits.Contains(element))
                                            list[i].ArrivalUnits.Remove(element);
                                }
                                change = true;
                                break;
                            }
                        }
                    }
                }

                foreach (var match in list)
                    result.Add(match.DepartureUnit, match.ArrivalUnits);
            }

            return result;
        }

        private static IEnumerable<ArrivalTrainPart> GetArrivalSubsets(ArrivalTrain arrival, ShuntTrain train)
        {
            List<ArrivalTrainPart> parts = new List<ArrivalTrainPart>();

            for (int i = 0; i < train.Units.Count; i++)
            {
                for (int j = 1; j < train.Units.Count - i + 1; j++)
                {
                    var units = train.Units.Skip(i).Take(j);
                    ArrivalTrainPart part = new ArrivalTrainPart(units.ToArray());
                    part.Time = arrival.Time + units.Sum(u => u.RequiredServices.Sum(t => t.Duration));
                    parts.Add(part);
                }
            }

            for (int i = 0; i < parts.Count; i++)
            {
                ArrivalTrainPart tp1 = parts[i];
                for (int j = i + 1; j < parts.Count; j++)
                {
                    ArrivalTrainPart tp2 = parts[j];
                    if (tp1.Units.Intersect(tp2.Units).Count() > 0)
                    {
                        tp1.Intersections.Add(tp2);
                        tp2.Intersections.Add(tp1);
                    }
                }
            }

            return parts;
        }

        private static IEnumerable<DepartureTrainPart> GetDepartureSubsets(DepartureTrain departure, Train train)
        {
            List<DepartureTrainPart> parts = new List<DepartureTrainPart>();

            for (int i = 0; i < departure.Units.Length; i++)
            {
                for (int j = 1; j < departure.Units.Length - i + 1; j++)
                {
                    var units = departure.Units.Skip(i).Take(j);
                    var departureunits = train.Units.Skip(i).Take(j);
                    DepartureTrainPart part = new DepartureTrainPart(units.ToArray(), departureunits.ToArray());
                    part.Time = departure.Time;
                    parts.Add(part);
                }
            }

            for (int i = 0; i < parts.Count; i++)
            {
                DepartureTrainPart tp1 = parts[i];
                for (int j = i + 1; j < parts.Count; j++)
                {
                    DepartureTrainPart tp2 = parts[j];
                    if (tp1.TrainUnits.Intersect(tp2.TrainUnits).Count() > 0)
                    {
                        tp1.Intersections.Add(tp2);
                        tp2.Intersections.Add(tp1);
                    }
                }
            }

            return parts;
        }

        private class TrainPart
        {
            public Time Time;
            public List<TrainPart> Subsets;
            public List<TrainPart> Supersets;
            public List<TrainPart> Intersections;
            public List<Match> Adjacent;

            public TrainPart()
            {

                this.Subsets = new List<TrainPart>();
                this.Supersets = new List<TrainPart>();
                this.Adjacent = new List<Match>();
                this.Intersections = new List<TrainPart>();
            }

            public void MakeUnavailable()
            {
                foreach (Match m in this.Adjacent)
                    m.Available = false;

                foreach (TrainPart tp in this.Intersections)
                    foreach (Match m in tp.Adjacent)
                        m.Available = false;
            }
        }

        private class ArrivalTrainPart : TrainPart
        {
            public ShuntTrainUnit[] Units;

            public ArrivalTrainPart(ShuntTrainUnit[] units)
            {
                this.Units = units;
            }

            public override string ToString()
            {
                return string.Join(",", this.Units.Select(unit => unit.Index));//string.Join(",", this.Units.Select(unit => $"{unit.Index}({unit.Type.Name})"));
            }
        }

        private class DepartureTrainPart : TrainPart
        {
            public DepartureTrainUnit[] Units;
            public Unit[] TrainUnits;

            public DepartureTrainPart(DepartureTrainUnit[] units, Unit[] departureunits)
            {
                this.Units = units;
                this.TrainUnits = departureunits;
            }



            public override string ToString()
            {
                return string.Join(",", this.Units.Select(unit => unit.ID));
            }
        }

        private class Match
        {
            public ArrivalTrainPart Arriving;
            public DepartureTrainPart Departing;
            public int Size;
            public bool Available;

            public Match(ArrivalTrainPart arriving, DepartureTrainPart departing)
            {
                this.Arriving = arriving;
                this.Departing = departing;
                this.Size = arriving.Units.Length;
                this.Available = true;
            }

            public override string ToString()
            {
                return $"{this.Arriving} ({this.Arriving.Adjacent.Count(m => m.Available)})-> {this.Departing} ({this.Departing.Adjacent.Count(m => m.Available)})";
            }
        }

        private class UnitMatch
        {
            public DepartureTrainUnit DepartureUnit;
            public List<TrainUnit> ArrivalUnits;

            public UnitMatch(DepartureTrainUnit unit, List<TrainUnit> units)
            {
                this.DepartureUnit = unit;
                this.ArrivalUnits = units;
            }

            public override string ToString()
            {
                return $"{this.DepartureUnit.ID} <- {string.Join(",", this.ArrivalUnits.Select(unit => unit.Index))}";
            }
        }
    }
}
