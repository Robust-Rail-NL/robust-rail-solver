#nullable enable

using System.Diagnostics;

namespace ServiceSiteScheduling.Matching
{
    class BipartiteGraph
    {
        public readonly ArrivalVertex[] Arrivals;
        public readonly DepartureVertex[] Departures;

        private readonly bool[,] adjacencyMatrix;
        private readonly List<Match> fixedMatches;
        private readonly int[] distance;
        private readonly ArrivalVertex[] matchableArrivals;
        private readonly int infinity;
        private readonly ArrivalVertex dummy;
        private readonly DepartureVertex?[] arrivalmatch;
        private readonly ArrivalVertex[] departurematch;

        private readonly Train[] departuretrains;

        public BipartiteGraph()
        {
            int unitCount = ProblemInstance.Current.TrainUnits.Length;
            this.adjacencyMatrix = new bool[unitCount, unitCount];
            this.fixedMatches = [];

            // Construct the departure trains
            this.departuretrains = new Train[ProblemInstance.Current.DeparturesOrdered.Length];
            for (int i = 0; i < ProblemInstance.Current.DeparturesOrdered.Length; i++)
            {
                Trains.DepartureTrain departure = ProblemInstance.Current.DeparturesOrdered[i];
                Unit[] units = new Unit[departure.Units.Length];
                for (int j = 0; j < departure.Units.Length; j++)
                {
                    Trains.DepartureTrainUnit departuretrainunit = departure.Units[j];
                    var unit = new Unit(departuretrainunit);
                    units[j] = unit;
                }
                this.departuretrains[i] = new Train(departure, units);
                foreach (Unit unit in units)
                    unit.Train = this.departuretrains[i];
            }

            int arrivalindex = 0;
            List<ArrivalVertex> arrivals = [];
            foreach (var arrival in ProblemInstance.Current.ArrivalsOrdered)
            {
                foreach (var unit in arrival.Units)
                    arrivals.Add(new ArrivalVertex(arrivalindex++, unit, arrival));
            }
            this.Arrivals = arrivals.ToArray();

            int departureindex = 0;
            List<DepartureVertex> departures = [];
            foreach (var departure in this.departuretrains)
            {
                foreach (var unit in departure.Units)
                    departures.Add(new DepartureVertex(departureindex++, unit, departure));
            }
            this.Departures = departures.ToArray();

            int matchCount = this.Arrivals.Length;
            this.distance = new int[matchCount + 1];
            this.infinity = matchCount + 1;
            this.dummy = new ArrivalVertex(matchCount, null!, null!);
            this.arrivalmatch = new DepartureVertex?[matchCount + 1];
            this.departurematch = new ArrivalVertex[matchCount + 1];

            arrivalindex = 0;
            // Create all possible edges
            foreach (var arrival in ProblemInstance.Current.ArrivalsOrdered)
            {
                departureindex = 0;
                foreach (var departure in ProblemInstance.Current.DeparturesOrdered)
                {
                    if (arrival.Time > departure.Time)
                    {
                        departureindex += departure.Units.Length;
                        continue;
                    }

                    foreach (var arrivalunit in arrival.Units)
                    {
                        foreach (var departureunit in departure.Units)
                        {
                            if (
                                arrival.Time
                                    + arrivalunit.RequiredServices.Sum(service => service.Duration)
                                < departure.Time
                            )
                            {
                                if (
                                    !departureunit.IsFixed
                                    && arrivalunit.Type == departureunit.Type
                                )
                                    this.adjacencyMatrix[arrivalindex, departureindex] = true;
                                else if (departureunit.Unit == arrivalunit)
                                    this.fixedMatches.Add(
                                        new Match(
                                            arrivals[arrivalindex],
                                            departures[departureindex]
                                        )
                                    );
                            }

                            departureindex++;
                        }
                        arrivalindex++;
                        departureindex -= departure.Units.Length;
                    }

                    departureindex += departure.Units.Length;
                    arrivalindex -= arrival.Units.Length;
                }
                arrivalindex += arrival.Units.Length;
            }

            foreach (var match in this.fixedMatches)
                for (int i = 0; i < matchCount; i++)
                    this.adjacencyMatrix[match.Arrival.Index, i] = this.adjacencyMatrix[
                        i,
                        match.Departure!.Index
                    ] = false;

            // Prune unnecessary edges
            bool change = true;
            while (change)
            {
                change = false;
                for (int i = 0; i < matchCount && !change; i++)
                {
                    int counter = 0;
                    for (int j = 0; j < matchCount && counter <= 1; j++)
                    {
                        if (this.adjacencyMatrix[i, j])
                            counter++;
                    }

                    if (counter == 1)
                    {
                        for (int j = 0; j < matchCount; j++)
                            if (this.adjacencyMatrix[i, j])
                            {
                                this.fixedMatches.Add(new Match(arrivals[i], departures[j]));
                                for (int k = 0; k < matchCount; k++)
                                    this.adjacencyMatrix[k, j] = false;
                                break;
                            }
                        change = true;
                    }
                }

                for (int i = 0; i < matchCount && !change; i++)
                {
                    int counter = 0;
                    for (int j = 0; j < matchCount && counter <= 1; j++)
                    {
                        if (this.adjacencyMatrix[j, i])
                            counter++;
                    }

                    if (counter == 1)
                    {
                        for (int j = 0; j < matchCount; j++)
                            if (this.adjacencyMatrix[j, i])
                            {
                                this.fixedMatches.Add(new Match(arrivals[i], departures[j]));
                                for (int k = 0; k < matchCount; k++)
                                    this.adjacencyMatrix[j, k] = false;
                                break;
                            }
                        change = true;
                    }
                }
            }

            // construct a (kind of) adjacency list
            List<ArrivalVertex> vertices = [];
            for (int i = 0; i < matchCount; i++)
            {
                var arrival = Arrivals[i];
                for (int j = 0; j < matchCount; j++)
                    if (this.adjacencyMatrix[i, j])
                        arrival.Adjacent.Add(this.Departures[j]);
                if (arrival.Adjacent.Count > 0)
                {
                    Debug.Assert(arrival.Adjacent.Count > 1);
                    vertices.Add(arrival);
                }
            }
            this.matchableArrivals = vertices.ToArray();
        }

        public IList<Match> MaximumMatching()
        {
            // LP: This is probably the Hopcroft-Karp algorithm (which uses both bfs and dfs)

            for (int i = 0; i < arrivalmatch.Length; i++)
            {
                arrivalmatch[i] = null;
                departurematch[i] = this.dummy;
            }

            while (this.BFS())
            {
                foreach (var arrival in this.matchableArrivals)
                    if (this.arrivalmatch[arrival.Index] == null)
                    {
                        this.DFS(arrival);
                    }
            }

            var result = this
                .arrivalmatch.Take(this.Arrivals.Length)
                .Select((departure, index) => new Match(this.Arrivals[index], departure!))
                .Where(match => match.Departure != null)
                .ToList();
            result.AddRange(this.fixedMatches);
            result = result.OrderBy(match => match.Arrival.Index).ToList();

            if (result.Count < this.Arrivals.Length)
            {
                var unmatchedarrivalunits = this
                    .Arrivals.Where(arrival => !result.Any(m => m.Arrival == arrival))
                    .Select(arrival =>
                        $"{arrival.Unit?.Name ?? "null"} {arrival.Train?.Time.ToString() ?? "null"}"
                    );
                var unmatcheddepartureunits = this
                    .Departures.Where(departure => !result.Any(m => m.Departure == departure))
                    .Select(departure =>
                        $"{departure.Unit.Departure} {departure.Train.Departure.Time}"
                    );
                throw new InvalidOperationException(
                    $"No feasible matching possible. Unmatched arrivals = {string.Join(",", unmatchedarrivalunits)}, departures = {string.Join(",", unmatcheddepartureunits)}"
                );
            }
            return result;
        }

        public TrainMatching LocalSearch(
            IList<Match> initialmatching,
            Random random,
            IList<Trains.ShuntTrainUnit> shunttrainunits,
            int iterations = 10000000,
            double T = 20,
            double alpha = 0.99,
            int Q = 40000,
            int reset = 5000
        )
        {
            // For each train: all train units except the first.  Used to count (prevented) splits.
            ArrivalVertex[] multiparts = this
                .Arrivals.Where(vertex => vertex.Unit != vertex.Train.Units.First())
                .ToArray();

            // Cost function: the number resulting shuntunits after splitting.  Every match error counts for five extra splits.
            int cost(int[] m)
            {
                int counter = 0;
                foreach (var vertex in multiparts)
                {
                    int match = m[vertex.Index],
                        previousmatch = m[vertex.Index - 1];
                    if (
                        match == previousmatch + 1
                        && this.Departures[match].Train == this.Departures[previousmatch].Train
                    )
                        counter++;
                }
                int errors = 0;
                for (int i = 0; i < m.Length; i++)
                    if (!this.adjacencyMatrix[i, m[i]])
                        errors++;
                return this.Arrivals.Length - counter + 5 * errors;
            }

            Dictionary<Trains.TrainType, List<ArrivalVertex>> arrivalsbytype = [];
            foreach (var arrival in this.Arrivals)
            {
                bool active = false;
                for (int i = 0; i < this.Arrivals.Length && !active; i++)
                    active = this.adjacencyMatrix[arrival.Index, i];
                if (!active)
                {
                    // This is a fixed match
                    Debug.Assert(this.fixedMatches.Any(m => m.Arrival == arrival));
                    // if the following is indeed true, this is a quicker way to check than iterating over the adjacency matrix:
                    Debug.Assert(arrival.Adjacent.Count == 0);
                    continue;
                }

                if (!arrivalsbytype.TryGetValue(arrival.Unit.Type, out var list))
                    arrivalsbytype[arrival.Unit.Type] = list = [];
                list.Add(arrival);
            }
            // For each type, we have at least two arrivals. Otherwise, it would have resulted in a fixed match.
            Debug.Assert(arrivalsbytype.Values.All(v => v.Count > 1));

            int[] matching = new int[this.Arrivals.Length];
            foreach (var match in initialmatching)
                matching[match.Arrival.Index] = match.Departure.Index;

            int currentcost = cost(matching),
                best = currentcost;
            int[] bestsolution = new int[matching.Length];
            Array.Copy(matching, bestsolution, matching.Length);
            int iteration = 0;
            while (arrivalsbytype.Count > 0)
            {
                if (iteration++ > iterations)
                    break;

                ArrivalVertex first,
                    second;

                var set = arrivalsbytype.Values.ElementAt(random.Next(arrivalsbytype.Count));
                first = set[random.Next(set.Count)];
                var secondindex = random.Next(set.Count);
                if (secondindex == first.Index)
                    secondindex = (secondindex + 1) % set.Count;
                second = set[secondindex];

                if (
                    this.adjacencyMatrix[first.Index, matching[second.Index]]
                    && this.adjacencyMatrix[second.Index, matching[first.Index]]
                )
                {
                    int temp = matching[first.Index];
                    matching[first.Index] = matching[second.Index];
                    matching[second.Index] = temp;

                    int c = cost(matching);

                    if (c < currentcost || Math.Exp((c - currentcost) / T) > random.NextDouble())
                    {
                        currentcost = c;
                        if (c < best)
                        {
                            Array.Copy(matching, bestsolution, matching.Length);
                            best = c;
                        }
                    }
                    else
                    {
                        matching[second.Index] = matching[first.Index];
                        matching[first.Index] = temp;
                    }
                }

                if (iteration % Q == 0)
                    T *= alpha;

                if (iteration % reset == 0)
                {
                    Array.Copy(bestsolution, matching, matching.Length);
                    currentcost = best;
                }
            }

            // Now we construct all sets of matches of trains (parts) that can be kept together
            List<List<Match>> partsmatching = [];
            for (int index = 0; index < bestsolution.Length; index++)
            {
                List<Match> part =
                [
                    new Match(this.Arrivals[index], this.Departures[bestsolution[index]]),
                ];

                int next = index + 1;
                while (
                    next < bestsolution.Length
                    && bestsolution[next] == bestsolution[next - 1] + 1
                    && this.Departures[bestsolution[next]].Train
                        == this.Departures[bestsolution[next - 1]].Train
                    && this.Arrivals[next].Train == this.Arrivals[next - 1].Train
                )
                {
                    part.Add(new Match(this.Arrivals[next], this.Departures[bestsolution[next]]));
                    next++;
                }

                partsmatching.Add(part);
                index += part.Count - 1;
            }

            Dictionary<Train, List<Part>> trainparts = [];
            foreach (var matchpart in partsmatching)
            {
                if (!trainparts.TryGetValue(matchpart.First().Departure.Train, out var parts))
                    trainparts[matchpart.First().Departure.Train] = parts = [];
                var part = new Part(matchpart.Select(m => m.Departure.Unit).ToArray());
                parts.Add(part);

                for (int i = 0; i < matchpart.Count; i++)
                {
                    matchpart[i].Departure.Unit.Index = matchpart[i].Arrival.Unit.Index;
                    matchpart[i].Departure.Unit.Part = part;
                }
            }
            foreach (var kvp in trainparts)
                kvp.Key.Parts = kvp.Value.ToArray();

            Debug.Assert(departuretrains.All(t => t.Parts != null));
            TrainMatching result = new(this.departuretrains, shunttrainunits);
            return result;
        }

        private bool BFS()
        {
            Queue<ArrivalVertex> queue = new();

            foreach (var arrival in this.matchableArrivals)
                if (this.arrivalmatch[arrival.Index] == null)
                {
                    this.distance[arrival.Index] = 0;
                    queue.Enqueue(arrival);
                }
                else
                    this.distance[arrival.Index] = this.infinity;

            this.distance[this.dummy.Index] = this.infinity;

            while (queue.Count > 0)
            {
                ArrivalVertex arrival = queue.Dequeue();
                int dist = this.distance[arrival.Index];
                if (dist < this.distance[this.dummy.Index])
                    foreach (DepartureVertex departure in arrival.Adjacent)
                    {
                        ArrivalVertex match = this.departurematch[departure.Index];
                        if (this.distance[match.Index] >= this.infinity)
                        {
                            this.distance[match.Index] = dist + 1;
                            queue.Enqueue(match);
                        }
                    }
            }
            // return: did we find an augmenting path?
            return this.distance[this.dummy.Index] < this.infinity;
        }

        private bool DFS(ArrivalVertex arrival)
        {
            if (arrival == this.dummy)
                return true;

            foreach (var departure in arrival.Adjacent)
            {
                var match = this.departurematch[departure.Index];
                if (
                    this.distance[match.Index] == this.distance[arrival.Index] + 1
                    && this.DFS(match)
                )
                {
                    this.departurematch[departure.Index] = arrival;
                    this.arrivalmatch[arrival.Index] = departure;
                    return true;
                }
            }

            this.distance[arrival.Index] = this.infinity;
            return false;
        }
    }

    class Vertex
    {
        public int Index;

        public Vertex(int index)
        {
            this.Index = index;
        }
    }

    class ArrivalVertex(int index, Trains.TrainUnit unit, Trains.ArrivalTrain train) : Vertex(index)
    {
        public readonly List<DepartureVertex> Adjacent = [];
        public readonly Trains.TrainUnit Unit = unit;
        public readonly Trains.ArrivalTrain Train = train;

        public override string ToString()
        {
            return $"Arrival index {this.Index} {this.Unit}";
        }
    }

    class DepartureVertex(int index, Unit unit, Train train) : Vertex(index)
    {
        public readonly Unit Unit = unit;
        public readonly Train Train = train;

        public override string ToString()
        {
            return $"Departure index {this.Index} {this.Unit}";
        }
    }

    class Match(ArrivalVertex arrival, DepartureVertex departure)
    {
        public readonly ArrivalVertex Arrival = arrival;
        public readonly DepartureVertex Departure = departure;

        public override string ToString()
        {
            return $"{this.Arrival} <-> {this.Departure}";
        }
    }
}
