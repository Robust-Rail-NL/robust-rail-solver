namespace ServiceSiteScheduling.LocalSearch
{
    abstract class LocalSearchMove : IComparable
    {
        public Solutions.PlanGraph Graph { get; set; }
        public Solutions.SolutionCost Cost { get; set; }
        public Utilities.BitSet AffectedTracks { get; protected set; }

        protected string routingordering;
        protected Tasks.MoveTask executestart,
            executeend,
            revertstart,
            revertend;

#if DEBUG
        /// <summary>
        /// Check the plan graph on one in every this many candidate moves.
        /// </summary>
        /// <remarks>
        /// The two debug-only checks below — CheckCorrectness, and serialising
        /// the routing order before and after to prove Revert restored the graph
        /// — walk or stringify the entire graph for every candidate move. Between
        /// them they account for ~97% of the cost of an assertions-enabled build
        /// (measured on KleineBinckhorst 30t_random_98s_test: 817 neighbours in a
        /// 10s budget against 24904 with both disabled, of which the routing-order
        /// round trip is ~60% and CheckCorrectness ~36%). That is too slow to run
        /// the integration pipeline against, and because the search is bounded by
        /// wall clock it also changes which plan comes out.
        ///
        /// Sampling keeps the checks meaningful — a corruption still surfaces
        /// within this many moves — at a fraction of the cost. Set to 1 to check
        /// every move, as builds before this change did.
        /// </remarks>
        public static int DebugCheckInterval { get; set; } = 100;

        private static int debugCheckCounter;

        /// <summary>Whether this particular move is one of the sampled ones.</summary>
        private readonly bool debugCheckThisMove;
#endif

        public LocalSearchMove(Solutions.PlanGraph graph)
        {
            this.Graph = graph;
#if DEBUG
            // Decided once per move so that the Revert comparison below is only
            // made when the constructor actually recorded a routing order.
            this.debugCheckThisMove =
                DebugCheckInterval <= 1 || ++debugCheckCounter % DebugCheckInterval == 0;
            if (this.debugCheckThisMove && this.Graph != null)
            {
                this.routingordering = graph.RoutingOrdering();
                this.Graph.CheckCorrectness();
            }
#endif
        }

        public virtual Solutions.SolutionCost Execute()
        {
#if DEBUG
            this.Graph.UpdateRoutingOrder();
#endif
            this.Cost = this.Graph.ComputeModel(
                this.executestart ?? this.Graph.First,
                this.executeend ?? this.Graph.Last
            );
#if DEBUG
            if (this.debugCheckThisMove)
                this.Graph.CheckCorrectness();
#endif
            return this.Cost;
        }

        public virtual Solutions.SolutionCost Revert()
        {
            this.Graph.UpdateRoutingOrder();
            var cost = this.Graph.ComputeModel(
                this.revertstart ?? this.Graph.First,
                this.revertend ?? this.Graph.Last
            );
#if DEBUG
            if (this.debugCheckThisMove)
            {
                this.Graph.CheckCorrectness();
                if (this.routingordering != this.Graph.RoutingOrdering())
                    throw new InvalidOperationException();
            }
#endif
            return cost;
        }

        public virtual void Finish()
        {
            this.Graph.UpdateRoutingOrder();
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;

            var other = obj as LocalSearchMove;

            if (this.Cost == null && other.Cost == null)
                return 0;
            if (this.Cost == null)
                return 1;
            if (other.Cost == null)
                return -1;

            return this.Cost.BaseCost.CompareTo(other.Cost.BaseCost);
        }

        public abstract bool IsSimilarMove(LocalSearchMove move);

        public virtual bool IsTabu(IEnumerable<LocalSearchMove> tabu)
        {
            return tabu.Any(move => this.IsSimilarMove(move));
        }
    }

    class IdentityMove : LocalSearchMove
    {
        public IdentityMove(Solutions.PlanGraph graph)
            : base(graph)
        {
            this.Cost = graph.Cost;
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            return move is IdentityMove;
        }
    }
}
