using ServiceSiteScheduling.TrackParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceSiteScheduling.Parking
{
    class Deque<T> where T : DequeNode<T>
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
            if (node == null)
                throw new ArgumentNullException();

            if ((this.A == null && this.B == null) || this.Count == 0)
                throw new ArgumentException();

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
