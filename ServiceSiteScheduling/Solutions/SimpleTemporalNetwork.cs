#nullable enable

using ServiceSiteScheduling.Utilities;

namespace ServiceSiteScheduling.Solutions
{
    /// <summary>
    /// A vertex in a <see cref="SimpleTemporalNetwork"/>: an opaque time-point whose
    /// <see cref="Value"/> is assigned by <see cref="SimpleTemporalNetwork.Dispatch"/>.
    /// </summary>
    class TimePoint
    {
        public string? Label { get; }
        public Time Value { get; internal set; }

        // Edges are recorded only on their target: `to.Incoming` holds every
        // (from, minDistance) constraint requiring `to.Value >= from.Value + minDistance`.
        internal readonly List<(TimePoint From, Time MinDistance)> Incoming = [];

        internal TimePoint(string? label)
        {
            this.Label = label;
        }

        public override string ToString() => this.Label ?? base.ToString()!;
    }

    /// <summary>
    /// A Simple Temporal Network: <see cref="TimePoint"/> vertices plus lower-bound edges
    /// between them ("this must be at least this much later than that"), dispatched by
    /// assigning every vertex the earliest time consistent with all of its constraints.
    /// <para>
    /// Every edge added via <see cref="Require"/> here is currently a lower bound
    /// (<c>to &gt;= from + minDistance</c>) -- the restricted PERT/CPM special case of an
    /// STN, not the fully general form (which also allows upper bounds / negative edges,
    /// and needs Bellman-Ford-style negative-cycle detection to check consistency).
    /// <see cref="Require"/> itself rejects a negative weight -- <see cref="Dispatch"/>'s
    /// cycle resolution relies on every edge being non-negative (see its own doc comment)
    /// -- so a future upper-bound/negative edge would need both to generalize together.
    /// </para>
    /// </summary>
    class SimpleTemporalNetwork
    {
        /// <summary>
        /// The network's fixed zero-reference time-point. Always dispatches to
        /// <c>Value == 0</c>; an absolute lower bound on some other point is expressed as
        /// an edge from here (see <see cref="RequireAbsolute"/>).
        /// </summary>
        public TimePoint Origin { get; } = new TimePoint("origin");

        private readonly List<TimePoint> vertices;

        public SimpleTemporalNetwork()
        {
            this.vertices = [this.Origin];
        }

        public TimePoint CreateTimePoint(string? label = null)
        {
            var point = new TimePoint(label);
            this.vertices.Add(point);
            return point;
        }

        /// <summary>
        /// Adds the constraint <c>to.Value &gt;= from.Value + minDistance</c>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="minDistance"/> is negative -- <see cref="Dispatch"/>'s cycle
        /// resolution relies on every edge being non-negative (see its own doc comment).
        /// </exception>
        public void Require(TimePoint from, TimePoint to, Time minDistance)
        {
            if (minDistance < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(minDistance),
                    (int)minDistance,
                    "SimpleTemporalNetwork currently only supports non-negative lower-bound edges."
                );

            to.Incoming.Add((from, minDistance));
        }

        /// <summary>
        /// Sugar for <c>Require(Origin, point, earliest)</c>: an absolute lower bound on
        /// <paramref name="point"/>, independent of any other time-point.
        /// </summary>
        public void RequireAbsolute(TimePoint point, Time earliest)
        {
            this.Require(this.Origin, point, earliest);
        }

        /// <summary>
        /// Assigns every vertex the earliest <see cref="TimePoint.Value"/> consistent with
        /// every constraint added via <see cref="Require"/>: a Kahn's-algorithm topological
        /// pass, relaxing each vertex as the max over its incoming edges of
        /// <c>from.Value + minDistance</c> (0 for a vertex with none). O(V+E) -- the same
        /// complexity class as the linear walk this replaces -- as long as the constraint
        /// graph is acyclic, which it's expected but not structurally guaranteed to be: see
        /// <c>PlanGraph.BuildTemporalNetwork</c> for why (the cross-train <c>ServiceTask</c>
        /// resource chain could in principle disagree with per-train causal order).
        /// <para>
        /// Vertices left over once the topological pass gets stuck form one or more cycles
        /// (plus anything downstream of them). Every edge added currently carries a
        /// non-negative weight, so such a leftover component is resolved by relaxing it to a
        /// fixed point directly (bounded Bellman-Ford, restricted to just that component): a
        /// cycle whose edges are all exactly weight 0 is still consistent (those vertices
        /// simply share a value) and converges; a cycle with any positive-weight edge never
        /// converges (values keep increasing every round), which throws
        /// <see cref="InvalidOperationException"/> rather than silently producing a wrong time
        /// -- a genuine consistency check replacing today's silent-wrong-answer behavior.
        /// </para>
        /// </summary>
        public void Dispatch()
        {
            // Initialise data structures
            var incomingCount = new Dictionary<TimePoint, int>(this.vertices.Count);
            var ready = new Queue<TimePoint>();
            var outgoing = new Dictionary<TimePoint, List<TimePoint>>();

            foreach (var vertex in this.vertices)
            {
                incomingCount[vertex] = vertex.Incoming.Count;
                if (vertex.Incoming.Count == 0)
                    ready.Enqueue(vertex);

                // `Require` only records edges on their target (`to.Incoming`), so we build up
                // the reverse index (source -> targets) needed by Kahn's algorithm once here.
                foreach (var (from, _) in vertex.Incoming)
                {
                    if (!outgoing.TryGetValue(from, out var successors))
                        outgoing[from] = successors = [];
                    successors.Add(vertex);
                }
            }

            int processed = 0;
            while (ready.Count > 0)
            {
                var vertex = ready.Dequeue();
                processed++;
                vertex.Value = EarliestFeasible(vertex);

                if (!outgoing.TryGetValue(vertex, out var successors))
                    continue;
                foreach (var successor in successors)
                    if (--incomingCount[successor] == 0)
                        ready.Enqueue(successor);
            }

            if (processed < this.vertices.Count)
            {
                var remaining = this.vertices.Where(v => incomingCount[v] > 0).ToList();
                ResolveCycle(remaining);
            }
        }

        private static Time EarliestFeasible(TimePoint vertex)
        {
            Time value = 0;
            foreach (var (from, minDistance) in vertex.Incoming)
            {
                Time candidate = from.Value + minDistance;
                if (candidate > value)
                    value = candidate;
            }
            return value;
        }

        private static void ResolveCycle(List<TimePoint> remaining)
        {
            // Kahn's phase never touched these -- they're still whatever they held before
            // this Dispatch() call (stale, if this TimePoint/network were ever reused).
            // Reset to 0 so the relaxation below starts from the same safe floor
            // EarliestFeasible uses for an unconstrained vertex, not a stale leftover.
            foreach (var vertex in remaining)
                vertex.Value = 0;

            // Standard bounded Bellman-Ford: with |remaining| vertices, any acyclic
            // dependency chain among them has at most |remaining|-1 edges, so that many
            // relaxation rounds reach a fixed point unless a positive-weight cycle keeps
            // pushing some vertex's value up forever.
            bool changed = true;
            for (int round = 0; changed && round <= remaining.Count; round++)
            {
                changed = false;
                foreach (var vertex in remaining)
                {
                    Time value = EarliestFeasible(vertex);
                    if (value != vertex.Value)
                    {
                        vertex.Value = value;
                        changed = true;
                    }
                }
            }

            if (changed)
                throw new InvalidOperationException(
                    "Inconsistent temporal network: a positive-weight cycle prevents "
                        + $"{remaining.Count} time-point(s) from converging to a fixed "
                        + "schedule -- some time-point would have to be later than itself: "
                        + string.Join(", ", remaining.Select(v => v.Label ?? "<unnamed>"))
                );
        }
    }
}
