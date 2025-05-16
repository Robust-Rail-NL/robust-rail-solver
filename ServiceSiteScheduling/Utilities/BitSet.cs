using System.Collections;

namespace ServiceSiteScheduling.Utilities
{
    class BitSet : IEnumerable, IEquatable<BitSet>
    {
        #region Private Variables
        private ulong[] elements;
        private int length;

        private const int elementSize = 8 * sizeof(long);
        private readonly ulong mask = ulong.MaxValue;

        private bool changed = false;
        private int count = 0;
        #endregion

        #region Constructors
        public BitSet(int length)
        {
            this.length = length;
            this.elements = new ulong[(length - 1) / elementSize + 1];

            int remainder = length % elementSize;
            if (remainder != 0)
                this.mask = ulong.MaxValue >> (elementSize - remainder);
        }

        public BitSet(int length, int index) : this(length)
        {
            this[index] = true;
            this.count = 1;
        }

        public BitSet(BitSet b)
        {
            this.length = b.length;
            this.elements = new ulong[b.elements.Length];
            Array.Copy(b.elements, this.elements, b.elements.Length);
            this.mask = b.mask;
            this.changed = b.changed;
            this.count = b.count;
        }

        public BitSet(int length, ulong[] bits) : this(length)
        {
            Array.Copy(bits, this.elements, bits.Length);
        }
        #endregion

        #region Public Properties
        public bool this[int i]
        {
            get
            {
                ulong position = 1ul << (i % elementSize);
                return (this.elements[i / elementSize] & position) == position;
            }
            set
            {
                if (value)
                    this.elements[i / elementSize] |= (1ul << (i % elementSize));
                else
                    this.elements[i / elementSize] &= ulong.MaxValue - (1ul << (i % elementSize));
                changed = true;
            }
        }

        public int Length
        {
            get { return this.length; }
        }

        public bool Empty
        {
            get
            {
                if (!changed)
                    return this.count == 0;

                foreach (ulong ul in this.elements)
                    if (ul > 0)
                        return false;
                return true;
            }
        }

        public int Count
        {
            get
            {
                if (!changed)
                    return this.count;

                this.count = 0;
                for (int i = 0; i < this.elements.Length; i++)
                {
                    ulong element = this.elements[i];
                    element = element - ((element >> 1) & 0x5555555555555555UL);
                    element = (element & 0x3333333333333333UL) + ((element >> 2) & 0x3333333333333333UL);
                    this.count += (int)(unchecked(((element + (element >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
                }
                this.changed = false;
                return this.count;
            }
        }
        #endregion

        #region Public Methods
        public override bool Equals(object obj)
        {
            BitSet other = obj as BitSet;
            if (other == null)
                return false;

            return this.Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 29;
                for (int i = 0; i < this.elements.Length; i++)
                    hash = hash * 486187739 + this.elements[i].GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            char[] array = new char[this.length];
            for (int i = 0; i < this.length; i++)
                array[i] = (this[i] ? '1' : '0');

            return new string(array);
        }

        public BitSet And(BitSet b)
        {
            this.changed = true;
            for (int i = 0; i < this.elements.Length; i++)
                this.elements[i] &= b.elements[i];
            return this;
        }

        public BitSet Or(BitSet b)
        {
            this.changed = true;
            for (int i = 0; i < this.elements.Length; i++)
                this.elements[i] |= b.elements[i];
            return this;
        }

        public BitSet Or(ulong value, int size, int offset)
        {
            // Clean the value
            value = (value << (elementSize - size)) >> (elementSize - size);

            int index = offset / elementSize;
            this.elements[index] |= value << (offset % elementSize);
            if ((offset + size) / elementSize > index)
                this.elements[index + 1] |= value >> (elementSize - (offset % elementSize));
            return this;
        }

        public BitSet Xor(BitSet b)
        {
            this.changed = true;
            for (int i = 0; i < this.elements.Length; i++)
                this.elements[i] ^= b.elements[i];
            return this;
        }

        public BitSet Exclude(BitSet b)
        {
            this.changed = true;
            for (int i = 0; i < this.elements.Length; i++)
                this.elements[i] &= ~b.elements[i];
            return this;
        }

        public BitSet Not()
        {
            this.count = this.length - this.count;

            for (int i = 0; i < this.elements.Length; i++)
                this.elements[i] = ~this.elements[i];
            this.elements[this.elements.Length - 1] &= this.mask;
            return this;
        }

        public bool Intersects(BitSet b)
        {
            for (int i = 0; i < this.elements.Length; i++)
                if ((this.elements[i] & b.elements[i]) > 0)
                    return true;
            return false;
        }

        public bool IsSupersetOf(BitSet b)
        {
            for (int i = 0; i < this.elements.Length; i++)
                if ((b.elements[i] & ~this.elements[i]) > 0)
                    return false;
            return true;
        }

        public bool IsSubsetOf(BitSet b)
        {
            for (int i = 0; i < this.elements.Length; i++)
                if ((this.elements[i] & ~b.elements[i]) > 0)
                    return false;
            return true;
        }

        public BitSet Clear()
        {
            for (int i = 0; i < this.elements.Length; i++)
                this.elements[i] = 0;
            return this;
        }
        #endregion

        #region Operator Overloads
        public static BitSet operator &(BitSet b1, BitSet b2)
        {
            BitSet result = new BitSet(b1.length);
            for (int i = 0; i < b1.elements.Length; i++)
                result.elements[i] = b1.elements[i] & b2.elements[i];
            result.changed = true;
            return result;
        }

        public static BitSet operator |(BitSet b1, BitSet b2)
        {
            BitSet result = new BitSet(b1.length);
            for (int i = 0; i < b1.elements.Length; i++)
                result.elements[i] = b1.elements[i] | b2.elements[i];
            result.changed = true;
            return result;
        }

        public static BitSet operator ^(BitSet b1, BitSet b2)
        {
            BitSet result = new BitSet(b1.length);
            for (int i = 0; i < b1.elements.Length; i++)
                result.elements[i] = b1.elements[i] ^ b2.elements[i];
            result.changed = true;
            return result;
        }

        public static BitSet operator -(BitSet b1, BitSet b2)
        {
            BitSet result = new BitSet(b1.length);
            for (int i = 0; i < b2.elements.Length; i++)
                result.elements[i] = b1.elements[i] & ~b2.elements[i];
            result.changed = true;
            return result;
        }

        public static BitSet operator ~(BitSet b)
        {
            BitSet result = new BitSet(b.length);
            for (int i = 0; i < b.elements.Length; i++)
                result.elements[i] = ~b.elements[i];
            result.elements[result.elements.Length - 1] &= result.mask;
            result.changed = true;
            return result;
        }
        #endregion

        #region IEnumerable Members
        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int i = 0; i < this.elements.Length; i++)
            {
                int index = elementSize * i;
                for (ulong element = this.elements[i]; element != 0; element = element >> 1)
                {
                    if ((element & 1ul) == 1ul)
                        yield return index;
                    index++;
                }
            }
        }
        #endregion

        #region IEquatable<BitSet> Members
        public bool Equals(BitSet other)
        {
            if (other == null || this.length != other.length)
                return false;

            for (int i = 0; i < this.elements.Length; i++)
                if (this.elements[i] != other.elements[i])
                    return false;

            return true;
        }
        #endregion
    }
}
