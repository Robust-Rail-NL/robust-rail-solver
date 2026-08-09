namespace ServiceSiteScheduling.Parking
{
    class Deque<T>
        where T : DequeNode<T>
    {
        public T A { get; private set; }
        public T B { get; private set; }
        public int Count { get; private set; }

        public T Head(Side side)
        {
            if (side == Side.A)
                return this.A;
            if (side == Side.B)
                return this.B;

            throw new ArgumentException("The side should be precisely one of {A, B}");
        }

        public IEnumerable<T> A2B
        {
            get
            {
                T node = this.A;
                while (node != null)
                {
                    yield return node;
                    node = node.B;
                }
                yield break;
            }
        }

        public IEnumerable<T> B2A
        {
            get
            {
                T node = this.B;
                while (node != null)
                {
                    yield return node;
                    node = node.A;
                }
                yield break;
            }
        }

        public void Add(T node, Side side)
        {
            if (this.A == null)
            {
                this.A = this.B = node;
                node.A = node.B = null;
            }
            else
            {
                if (side == Side.A)
                {
                    this.A.A = node;
                    node.B = this.A;
                    this.A = node;
                }
                else
                {
                    this.B.B = node;
                    node.A = this.B;
                    this.B = node;
                }
            }

            this.Count++;
        }

        public void RemoveHead(Side side)
        {
            if (this.Count == 0)
                throw new ArgumentException("Cannot remove from empty deque");

            if (side == Side.A)
                this.Remove(this.A);
            else if (side == Side.B)
                this.Remove(this.B);

            throw new ArgumentException("The side should be precisely one of {A, B}");
        }

        public void Remove(T node)
        {
            ArgumentNullException.ThrowIfNull(node);

            if ((this.A == null && this.B == null) || this.Count == 0)
                throw new ArgumentException("Cannot remove from an empty deque.");

            // Removing a node that is not in this deque used to succeed silently:
            // the links were rewritten and Count decremented regardless, so the
            // damage surfaced later at an unrelated call once Count hit zero. That
            // is why #11 presented as a random crash in whichever local-search move
            // happened to be running. A node that is neither end and is linked to
            // nothing is in no deque at all — it was already removed, or never
            // added — so this catches the mismatched caller rather than its victim.
            if (node != this.A && node != this.B && node.A == null && node.B == null)
                throw new ArgumentException(
                    "The node is not in this deque: it is detached, so it was either "
                        + "already removed or never added."
                );

#if DEBUG
            // The cheap test above cannot tell an interior node of *another* deque
            // from one of ours. Walking the deque can, at O(n) — acceptable only in
            // the assertions build, which exists for exactly this kind of check.
            var present = false;
            for (var current = this.A; current != null; current = current.B)
                if (current == node)
                {
                    present = true;
                    break;
                }
            if (!present)
                throw new ArgumentException(
                    "The node is linked, but into a different deque than this one."
                );
#endif

            if (node == this.A)
                this.A = node.B;
            if (node == this.B)
                this.B = node.A;
            node.Remove();
            this.Count--;
        }

        public void Clear()
        {
            if (this.A != null)
            {
                var node = this.A;
                while (node != null)
                {
                    var next = node.B;
                    node.A = node.B = null;
                    node = next;
                }
            }
            this.A = this.B = null;
            this.Count = 0;
        }
    }
}
