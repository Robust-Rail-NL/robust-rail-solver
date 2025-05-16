
namespace ServiceSiteScheduling.Parking
{
    abstract class DequeNode<T> where T : DequeNode<T>
    {
        public T A, B;

        public T Next(Side side)
        {
            if (side == Side.A)
                return this.A;
            if (side == Side.B)
                return this.B;

            throw new ArgumentException("The side should be precisely one of {A, B}");
        }

        public void Remove()
        {
            if (this.A != null)
                this.A.B = this.B;
            if (this.B != null)
                this.B.A = this.A;

            this.A = this.B = null;
        }
    }
}
