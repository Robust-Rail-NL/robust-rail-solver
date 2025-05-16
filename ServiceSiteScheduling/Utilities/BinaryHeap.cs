
namespace ServiceSiteScheduling.Utilities
{
    /// <summary>
    /// A Generic Binary Heap Class
    /// </summary>
    class BinaryHeap<T>
    {
        #region Variables
        private List<BinaryHeapNode<T>> nodes = new List<BinaryHeapNode<T>>();
        private Comparison<T> compare;
        #endregion

        #region Properties
        public int Size { get { return this.nodes.Count; } }
        public T First { get { return this.nodes.Count > 0 ? this.nodes[0].Item : default(T); } }
        public bool IsEmpty { get { return this.nodes.Count == 0; } }
        #endregion

        #region Constructors
        /// <summary>
        /// Initialize a binary heap with the given comparison function.
        /// </summary>
        /// <param name="compare">Comparison should return negative integer if first precedes second, zero if equal and positive integer if second precedes first.</param>
        public BinaryHeap(Comparison<T> compare)
        {
            this.compare = compare;
        }
        #endregion

        #region Public Methods
        public BinaryHeapNode<T> Insert(T item)
        {
            BinaryHeapNode<T> node = new BinaryHeapNode<T>(item, this.compare, this.nodes.Count);
            this.nodes.Add(node);
            this.bubbleup(node);
            return node;
        }

        public T ExtractFirst()
        {
            if (this.nodes.Count == 0)
                return default(T);

            BinaryHeapNode<T> first = this.nodes[0];
            BinaryHeapNode<T> last = this.nodes[this.nodes.Count - 1];
            this.swap(first, last);
            // Removal is an O(1) operation, as it is the last element of the list
            this.nodes.RemoveAt(first.Index);
            if (this.nodes.Count > 0)
                this.bubbledown(last);

            return first.Item;
        }

        public void Update(BinaryHeapNode<T> node)
        {
            this.bubbledown(node);
            this.bubbleup(node);
        }

        public void Clear()
        {
            this.nodes.Clear();
        }
        #endregion

        #region Private Methods
        private void swap(BinaryHeapNode<T> a, BinaryHeapNode<T> b)
        {
            // Update the indices
            int temp = a.Index;
            a.Index = b.Index;
            b.Index = temp;

            // Swap the nodes
            this.nodes[a.Index] = a;
            this.nodes[b.Index] = b;
        }

        private void bubbledown(BinaryHeapNode<T> root)
        {
            // While the root has children
            while (root.Left < this.nodes.Count)
            {
                BinaryHeapNode<T> node = this.nodes[root.Left];
                // Determine the child node that is first in precedence
                if (root.Right < this.nodes.Count && this.nodes[root.Right].Precedes(node))
                    node = this.nodes[root.Right];

                // Swap if the child node precedes the root
                if (node.Precedes(root))
                    this.swap(root, node);
                else
                    break;
            }
        }

        private void bubbleup(BinaryHeapNode<T> node)
        {
            while (node.Index > 0)
            {
                BinaryHeapNode<T> parent = this.nodes[node.Parent];
                if (node.Precedes(parent))
                    this.swap(node, parent);
                else
                    break;
            }
        }
        #endregion
    }

    /// <summary>
    /// Node used by the BinaryHeap class
    /// </summary>
    class BinaryHeapNode<T>
    {
        #region Variables
        private T item;
        private int index;
        private Comparison<T> compare;
        #endregion

        #region Properties
        public T Item
        {
            get { return this.item; }
            set { this.item = value; }
        }

        public int Index
        {
            get { return this.index; }
            set { this.index = value; }
        }

        public int Parent
        {
            get { return (this.index - 1) / 2; }
        }

        public int Left
        {
            get { return this.index * 2 + 1; }
        }

        public int Right
        {
            get { return this.index * 2 + 2; }
        }
        #endregion

        #region Constructors
        public BinaryHeapNode(T item, Comparison<T> compare, int index)
        {
            this.item = item;
            this.compare = compare;
            this.index = index;
        }

        public BinaryHeapNode(T item, Comparison<T> compare) : this(item, compare, -1) { }
        #endregion

        #region Public Methods
        public bool Precedes(BinaryHeapNode<T> node)
        {
            return this.compare(this.item, node.item) < 0;
        }

        public bool Succeeds(BinaryHeapNode<T> node)
        {
            return this.compare(this.item, node.item) > 0;
        }

        public override string ToString()
        {
            return "BinaryHeapNode of item: " + this.item.ToString();
        }
        #endregion
    }
}
