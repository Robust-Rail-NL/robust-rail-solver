using ServiceSiteScheduling.Solutions;
using ServiceSiteScheduling.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ServiceSiteScheduling.LocalSearch
{
    class TabuSearch
    {
        public static int Iterations = 2500, IterationsUntilReset = 60, TabuListLength = 12;

        Random random;
        public PlanGraph Graph { get; private set; }

        public TabuSearch(Random random)
        {
            var graph = Initial.SimpleHeuristic.Construct(random);
            graph.Cost = graph.ComputeModel();

           // Integrate here the POS creation?

            this.Graph = graph;
            this.random = random;
        }

        public TabuSearch(Random random, PlanGraph graph)
        {
            this.Graph = graph;
            this.random = random;
        }

        //@iterations: maximum iterations in the searching algorithm if it is achieved the search ends
        //@iterationsUntilReset: the current solution should be improved until that number of iteration if this number is hit, the current solution  cannot be improved -> the current solution is reverted to the original solution
        //@tabuListLength: lenght of tabu list conaining LocalSerachMoves -> solution graphs (e.g., 16) 
        //@bias: restricted probability (e.g., 0.75)
        //@suppressConsoleOutput: enables extra logs
        public void Run(int iterations, int iterationsUntilReset, int tabuListLength, double bias = 0.75, bool suppressConsoleOutput = false)
        {
            //if (iterations == 0)
            //    return;

            List<LocalSearchMove> moves = new List<LocalSearchMove>();
            moves.Add(new IdentityMove(this.Graph));
            int noimprovement = 0, iteration = 0, neighborsvisited = 0;
            SolutionCost bestcost = this.Graph.ComputeModel(), current = bestcost;
            LinkedList<LocalSearchMove> tabu = new LinkedList<LocalSearchMove>();

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                List<LocalSearchMove> currentmoves = new List<LocalSearchMove>();

                if (iteration >= iterations)
                    break;

                var servicemachineswapmoves = ServiceMachineSwapMove.GetMoves(this.Graph);
                currentmoves.AddRange(servicemachineswapmoves);
                var servicemachineordermoves = ServiceMachineOrderMove.GetMoves(this.Graph);
                currentmoves.AddRange(servicemachineordermoves);
                var servicemachineswitchmoves = ServiceMachineSwitchMove.GetMoves(this.Graph);
                currentmoves.AddRange(servicemachineswitchmoves);
                var servicetrainordermoves = ServiceTrainOrderMove.GetMoves(this.Graph);
                currentmoves.AddRange(servicetrainordermoves);
                var matchingswapmoves = MatchingSwapMove.GetMoves(this.Graph);
                currentmoves.AddRange(matchingswapmoves);
                var parkingshiftmoves = ParkingSwitchMove.GetMoves(this.Graph);
                currentmoves.AddRange(parkingshiftmoves);
                var parkingswapmoves = ParkingSwapMove.GetMoves(this.Graph);
                currentmoves.AddRange(parkingswapmoves);
                var routingshiftmoves = RoutingMove.GetMoves(this.Graph);
                currentmoves.AddRange(routingshiftmoves);
                var parkinginsertmoves = ParkingInsertMove.GetMoves(this.Graph);
                currentmoves.AddRange(parkinginsertmoves);
                var parkingroutingtemporarymoves = ParkingRoutingTemporaryMove.GetMoves(this.Graph);
                currentmoves.AddRange(parkingroutingtemporarymoves);

                currentmoves = currentmoves.Where(move => !move.IsTabu(tabu)).ToList();
                neighborsvisited += currentmoves.Count;

                bool fullcost = this.Graph.Cost.IsFeasible;

                // Console.WriteLine("-------- Here --------");
                foreach (var move in currentmoves)
                {
                    move.Execute();
                    move.Revert();

                    if (move.Cost.Cost(fullcost) + SolutionCost.CombineDepartureWeight < bestcost.Cost(fullcost) || move.Cost.Cost(fullcost) + 5 * SolutionCost.CombineDepartureWeight < current.Cost(fullcost))
                        break;
                }

                LocalSearchMove next = currentmoves.Min();

                // If no moves are possible
                if (next == null)
                {
                    // Try to clear empty the tabu list
                    if (tabu.Count == 0)
                    {
                        // Else try to revert to previous state
                        this.Revert(moves, fullcost);
                        current = bestcost;
                        noimprovement = 0;
                    }
                    else
                        tabu.RemoveLast();

                    iteration++;
                    continue;
                }

                // If we improved the best solution
                if (next.Cost.Cost(fullcost) < bestcost.Cost(fullcost))
                {
                    current = bestcost = next.Cost;
                    if (!suppressConsoleOutput)
                        Console.WriteLine($"{next.Cost}");
                    noimprovement = 0;
                }
                else
                {
                    // If there was no improvement for several iterations
                    if (noimprovement++ > iterationsUntilReset || next.Cost.Cost(fullcost) > 1.5 * bestcost.Cost(fullcost))
                    {
                        // Revert to previous best
                        Revert(moves, fullcost);
                        current = bestcost;
                        noimprovement = 0;
                        iteration++;
                        continue;
                    }

                    // If we did not improve the current solution
                    if (next.Cost.Cost(fullcost) >= current.Cost(fullcost))
                    {
                        List<LocalSearchMove> possiblemoves = new List<LocalSearchMove>();

                        bool selected = false;
                        if (this.random.NextDouble() < bias)
                        {
                            possiblemoves.AddRange(parkingshiftmoves.Where(move => 
                                this.Graph.Cost.ProblemTracks[move.Track.Index] || 
                                move.RelatedTasks.Any(task => this.Graph.Cost.ProblemTrains.Intersects(task.Train.UnitBits))));
                            possiblemoves.AddRange(parkingswapmoves.Where(move => 
                                this.Graph.Cost.ProblemTracks[move.ParkingFirst.First().Track.Index] || 
                                this.Graph.Cost.ProblemTracks[move.ParkingSecond.First().Track.Index] ||
                                move.ParkingFirst.Any(task => this.Graph.Cost.ProblemTrains.Intersects(task.Train.UnitBits)) ||
                                move.ParkingSecond.Any(task => this.Graph.Cost.ProblemTrains.Intersects(task.Train.UnitBits))));
                            possiblemoves.AddRange(servicemachineordermoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.First.Track.Index] ||
                                this.Graph.Cost.ProblemTracks[move.Second.Track.Index] ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.First.Train.UnitBits) ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.Second.Train.UnitBits)));
                            possiblemoves.AddRange(servicemachineswapmoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.First.Track.Index] ||
                                this.Graph.Cost.ProblemTracks[move.Second.Track.Index] ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.First.Train.UnitBits) ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.Second.Train.UnitBits)));
                            possiblemoves.AddRange(servicemachineswitchmoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.Selected.Track.Index] ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.Selected.Train.UnitBits)));
                            possiblemoves.AddRange(servicetrainordermoves.Where(move =>
                                this.Graph.Cost.ProblemTracks[move.First.Track.Index] ||
                                this.Graph.Cost.ProblemTracks[move.Second.Track.Index] ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.First.Train.UnitBits)));
                            possiblemoves.AddRange(matchingswapmoves.Where(move =>
                                this.Graph.Cost.ProblemTrains.Intersects(move.First.Matching.GetShuntTrain(move.First.Train).UnitBits) ||
                                this.Graph.Cost.ProblemTrains.Intersects(move.Second.Matching.GetShuntTrain(move.Second.Train).UnitBits)));
                            possiblemoves.AddRange(routingshiftmoves.Where(move =>
                            {
                                var shift = move as RoutingShiftMove;
                                if (shift != null)
                                {
                                    return
                                        this.Graph.Cost.ProblemTracks[shift.Selected.ToTrack.Index] ||
                                        shift.Selected.AllNext.Any(task => this.Graph.Cost.ProblemTracks[task.Track.Index]) ||
                                        this.Graph.Cost.ProblemTrains.Intersects(shift.Selected.Train.UnitBits);
                                }
                                else
                                {
                                    var merge = move as RoutingMergeMove;
                                    return
                                        this.Graph.Cost.ProblemTracks[merge.To.ToTrack.Index] ||
                                        this.Graph.Cost.ProblemTrains.Intersects(merge.From.Train.UnitBits);
                                }
                            }));
                            possiblemoves.AddRange(parkinginsertmoves);
                            possiblemoves.AddRange(parkingroutingtemporarymoves);
                            possiblemoves = possiblemoves.Where(m => (m.Cost?.Cost(fullcost) ?? double.PositiveInfinity) < current.Cost(fullcost) + 50).ToList();

                            if (possiblemoves.Count > 0)
                                selected = true;
                        }
                        
                        if (!selected)
                        {
                            possiblemoves = currentmoves;
                        }

                        // Select a random move
                        next = possiblemoves[this.random.Next(possiblemoves.Count)];
                    }
                }

                tabu.AddFirst(next);
                if (tabu.Count > tabuListLength)
                    tabu.RemoveLast();

                moves.Add(next);
                this.Graph.Cost = next.Execute();
                next.Finish();
                current = next.Cost;

                //if (this.Graph.Cost.IsFeasible)
                //    break;

                /*if ((stopwatch.ElapsedMilliseconds / 1000) > TimeUntilRestart && !this.Graph.Cost.IsFeasible)
                {
                    this.Graph.Clear();
                    this.Graph = Initial.SimpleHeuristic.Construct(random);
                    this.Graph.Cost = this.Graph.ComputeModel();
                    current = double.PositiveInfinity;
                    bestcost = this.Graph.Cost.Cost;
                    tabu.Clear();
                    moves.Clear();
                    bestindex = -1;
                    index = 0;
                    noimprovement = 0;
                    TimeUntilRestart *= 2;
                    Console.WriteLine("Restart");
                    Console.WriteLine($"{this.Graph.Cost}");
                }*/


                if (iteration >= iterations)
                    break;

                if (++iteration % 100 == 0 && !suppressConsoleOutput)
                    Console.WriteLine(iteration);
            }

            stopwatch.Stop();

            if (!suppressConsoleOutput)
            {
                this.Revert(moves, this.Graph.Cost.IsFeasible);
                Console.WriteLine("-----------------------");
                Console.WriteLine($"{this.Graph.ComputeModel()}");
                /*this.Graph.OutputMovementSchedule();
                Console.WriteLine("-----------------------");
                this.Graph.OutputTrainUnitSchedule();
                Console.WriteLine("-----------------------");
                this.Graph.OutputConstraintViolations();*/
                Console.WriteLine("-----------------------");
                Console.WriteLine($"Finished after {(stopwatch.ElapsedMilliseconds / (double)1000).ToString("N2")} seconds");
                Console.WriteLine($"Neighbors visited = {neighborsvisited}");
            }
        }

        protected void Revert(List<LocalSearchMove> moves, bool fullcost)
        {
            int min = moves.MinIndex(move => move.Cost.Cost(fullcost));

            for (int i = moves.Count - 1; i > min; i--)
                this.Graph.Cost = moves[i].Revert();

            moves.RemoveRange(min + 1, moves.Count - min - 1);
        }
    }
}
