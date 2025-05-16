using ServiceSiteScheduling.Solutions;
using System.Diagnostics;

namespace ServiceSiteScheduling.LocalSearch
{
    class HillClimber
    {
        Random random;
        public PlanGraph Graph { get; private set; }

        public HillClimber(Random random)
        {
            var graph = Initial.SimpleHeuristic.Construct(random);
            graph.Cost = graph.ComputeModel();
            Console.WriteLine($"initial cost = {graph.Cost.BaseCost}");

            this.Graph = graph;
            this.random = random;
        }

        public HillClimber(Random random, PlanGraph graph)
        {
            this.Graph = graph;
            this.random = random;
        }

        public void Run()
        {
            int iteration = 0, neighborsvisited = 0;
            double bestcost = this.Graph.ComputeModel().BaseCost;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                List<LocalSearchMove> currentmoves = new List<LocalSearchMove>();

                var servicemachineordermoves = ServiceMachineOrderMove.GetMoves(this.Graph);
                currentmoves.AddRange(servicemachineordermoves);
                var servicemachineswitchmoves = ServiceMachineSwitchMove.GetMoves(this.Graph);
                currentmoves.AddRange(servicemachineswitchmoves);
                var servicetrainordermoves = ServiceTrainOrderMove.GetMoves(this.Graph);
                currentmoves.AddRange(servicetrainordermoves);
                var parkinginsertmoves = ParkingInsertMove.GetMoves(this.Graph);
                currentmoves.AddRange(parkinginsertmoves);
                var matchingswapmoves = MatchingSwapMove.GetMoves(this.Graph);
                currentmoves.AddRange(matchingswapmoves);
                var parkingshiftmoves = ParkingSwitchMove.GetMoves(this.Graph);
                currentmoves.AddRange(parkingshiftmoves);
                var parkingswapmoves = ParkingSwapMove.GetMoves(this.Graph);
                currentmoves.AddRange(parkingswapmoves);
                var routingshiftmoves = RoutingMove.GetMoves(this.Graph);
                currentmoves.AddRange(routingshiftmoves);

                neighborsvisited += currentmoves.Count;

                foreach (var move in currentmoves)
                {
                    move.Execute();
                    move.Revert();
                }

                LocalSearchMove next = currentmoves.Min();

                if (next.Cost.BaseCost < bestcost)
                {
                    bestcost = next.Cost.BaseCost;
                    Console.WriteLine($"{next.Cost}");
                    next.Execute();
                }
                else
                    break;

                if (++iteration % 100 == 0)
                    Console.WriteLine(iteration);
            }

            stopwatch.Stop();

            Console.WriteLine("-----------------------");
            Console.WriteLine($"{this.Graph.ComputeModel()}");
            this.Graph.OutputMovementSchedule();
            Console.WriteLine("-----------------------");
            this.Graph.OutputTrainUnitSchedule();
            Console.WriteLine("-----------------------");
            this.Graph.OutputConstraintViolations();
            Console.WriteLine("-----------------------");
            Console.WriteLine($"Finished after {(stopwatch.ElapsedMilliseconds / (double)1000).ToString("N2")} seconds");
            Console.WriteLine($"Neighbors visited = {neighborsvisited}");
        }
    }
}
