
namespace ServiceSiteScheduling.Utilities
{
    struct Time : IComparable
    {
        public const int Second = 1;
        public const int Minute = 60;
        public const int Hour = 60 * 60;
        public const int Day = 24 * 60 * 60;

        public readonly int Seconds;

        public Time(int time)
        {
            this.Seconds = time;
        }

        public Time(Time ts)
        {
            this.Seconds = ts.Seconds;
        }

        public static Time operator +(Time ts1, Time ts2)
        {
            return new Time(ts1.Seconds + ts2.Seconds);
        }

        public static Time operator -(Time ts1, Time ts2)
        {
            return new Time(ts1.Seconds - ts2.Seconds);
        }

        public static implicit operator Time(int time)
        {
            return new Time(time);
        }

        public static implicit operator int(Time time)
        {
            return time.Seconds;
        }

        public static explicit operator ulong(Time time)
        {
            return (ulong)time.Seconds;
        }

        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;

            return this.Seconds.CompareTo((Time)obj);
        }

        public override string ToString()
        {
            int time = this.Seconds;
            int hours = time / 3600;
            time -= hours * 3600;
            int minutes = time / 60;
            time -= minutes * 60;

            return "" + hours.ToString("D2") + ":" + minutes.ToString("D2") + ":" + time.ToString("D2");
        }

        public string ToMinuteString()
        {
            int time = this.Seconds;
            int hours = time / 3600;
            time -= hours * 3600;
            int minutes = time / 60;

            return "" + hours.ToString("D2") + ":" + minutes.ToString("D2");
        }
    }
}
