
namespace ServiceSiteScheduling.LocalSearch
{
    abstract class LocalSearchMove : IComparable
    {
        public Solutions.PlanGraph Graph { get; set; }
        public Solutions.SolutionCost Cost { get; set; }
        public Utilities.BitSet AffectedTracks { get; protected set; }

        protected string routingordering, newroutingordering;
        protected Tasks.MoveTask executestart, executeend, revertstart, revertend;

        public LocalSearchMove(Solutions.PlanGraph graph)
        {
            this.Graph = graph;
#if DEBUG
            if (this.Graph != null)
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
            this.Cost = this.Graph.ComputeModel(this.executestart ?? this.Graph.First, this.executeend ?? this.Graph.Last);
#if DEBUG
            this.newroutingordering = this.Graph.RoutingOrdering();
            this.Graph.CheckCorrectness();
#endif
            return this.Cost;
        }

        public virtual Solutions.SolutionCost Revert()
        {
            this.Graph.UpdateRoutingOrder();
            var cost = this.Graph.ComputeModel(this.revertstart ?? this.Graph.First, this.revertend ?? this.Graph.Last);
#if DEBUG
            this.Graph.CheckCorrectness();
            if (this.routingordering != this.Graph.RoutingOrdering())
                throw new InvalidOperationException();
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
        public IdentityMove(Solutions.PlanGraph graph) : base(graph)
        {
            this.Cost = graph.Cost;
        }

        public override bool IsSimilarMove(LocalSearchMove move)
        {
            return move is IdentityMove;
        }
    }
}
