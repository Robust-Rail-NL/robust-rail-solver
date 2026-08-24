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
                throw new ArgumentException("Cannot remove from an empty deque.");

            // Head is where a side is checked to be precisely one of {A, B}, and it
            // throws otherwise, so there is nothing to repeat here.
            this.Remove(this.Head(side));
        }

        public void Remove(T node)
        {
            ArgumentNullException.ThrowIfNull(node);

            this.CheckPresent(node);

            if (node == this.A)
                this.A = node.B;
            if (node == this.B)
                this.B = node.A;
            node.Remove();
            this.Count--;
        }

        /// <summary>
        /// Throws unless <paramref name="node"/> is in this deque. Shared by every
        /// operation that rewrites links around an existing node, so that a
        /// mismatched caller is caught where it offends rather than later.
        /// </summary>
        private void CheckPresent(T node)
        {
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
        }

        /// <summary>
        /// Replaces <paramref name="node"/> with <paramref name="replacements"/>, which
        /// take over exactly the stretch it occupied and keep its neighbours on either
        /// side. This is what splitting a train where it stands does to a track: the
        /// parts end up adjacent, in the same place and the same order as the whole.
        /// Adding them instead would put them at one end of the track, which is only
        /// right if the train drove out and back.
        /// </summary>
        /// <param name="replacements">In A-to-B order, so the first ends up nearest A.</param>
        public void Replace(T node, IReadOnlyList<T> replacements)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(replacements);
            if (replacements.Count == 0)
                throw new ArgumentException(
                    "Use Remove to take a node out; Replace must put something in its place."
                );

            this.CheckPresent(node);

            // Hold on to the neighbours before unlinking: node.Remove clears both.
            T towardsA = node.A,
                towardsB = node.B;
            node.Remove();

            for (int i = 0; i < replacements.Count; i++)
            {
                replacements[i].A = i == 0 ? towardsA : replacements[i - 1];
                replacements[i].B = i == replacements.Count - 1 ? towardsB : replacements[i + 1];
            }

            T first = replacements[0],
                last = replacements[^1];
            if (towardsA != null)
                towardsA.B = first;
            if (towardsB != null)
                towardsB.A = last;

            if (this.A == node)
                this.A = first;
            if (this.B == node)
                this.B = last;

            this.Count += replacements.Count - 1;
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
