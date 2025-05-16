using ServiceSiteScheduling.Solutions;

namespace ServiceSiteScheduling.LocalSearch
{
    abstract class RoutingMove : LocalSearchMove
    {
        public RoutingMove(PlanGraph graph) : base(graph)
        {
        }

        public static IList<RoutingMove> GetMoves(PlanGraph graph)
        {
            List<RoutingMove> moves = new List<RoutingMove>();

            Tasks.MoveTask selected = graph.First;

            while (selected != null)
            {
                // Shift earlier
                var position = selected.PreviousMove;
                while (RoutingShiftMove.Allowed(position, selected))
                {
                    RoutingShiftMove move = new RoutingShiftMove(graph, selected, position, true);
                    moves.Add(move);

                    if (selected.Train.Equals(position.Train))
                        break;

                    position = position.PreviousMove;
                }
                // Check merge
                if (RoutingMergeMove.Allowed(position, selected))
                {
                    RoutingMergeMove move = new RoutingMergeMove(graph, position.AllNext.First() as Tasks.ParkingTask, false);
                    moves.Add(move);
                }

                // Shift later
                position = selected.NextMove;
                while (RoutingShiftMove.Allowed(selected, position))
                {
                    RoutingShiftMove move = new RoutingShiftMove(graph, selected, position, false);
                    moves.Add(move);

                    if (selected.Train.Equals(position.Train))
                        break;

                    position = position.NextMove;
                }
                // Check merge
                if (RoutingMergeMove.Allowed(selected, position))
                {
                    RoutingMergeMove move = new RoutingMergeMove(graph, selected.AllNext.First() as Tasks.ParkingTask, true);
                    moves.Add(move);
                }

                selected = selected.NextMove;
            }

            // Return best / all
            return moves;
        }
    }
}
