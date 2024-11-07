namespace ServiceSiteScheduling
{
    public abstract class Side
    {
        public static Side None { get; } = new NoSide();
        public static Side A { get; } = new ASide();
        public static Side B { get; } = new BSide();
        public static Side Both { get; } = new BothSides();

        public static Side AB { get; } = Both;

        public string Name;
        public abstract Side Flip { get; }

        private Side(string name) { this.Name = name; }

        public abstract bool HasFlag(Side side);

        public override string ToString()
        {
            return this.Name;
        }

        private class NoSide : Side
        {
            public NoSide() : base("None") { }
            public override Side Flip { get { return Both; } }
            public override bool HasFlag(Side side)
            {
                return this == side;
            }
        }

        private class ASide : Side
        {
            public ASide() : base("A") { }
            public override Side Flip { get { return B; } }
            public override bool HasFlag(Side side)
            {
                return this == side || side == None;
            }
        }

        private class BSide : Side
        {
            public BSide() : base("B") { }
            public override Side Flip { get { return A; } }
            public override bool HasFlag(Side side)
            {
                return this == side || side == None;
            }
        }

        private class BothSides : Side
        {
            public BothSides() : base("Both") { }
            public override Side Flip { get { return None; } }
            public override bool HasFlag(Side side)
            {
                return true;
            }
        }
    }
}
