// Time wraps a signed int and legitimately goes negative as an intermediate
// value (slack, "how far before/after a deadline"). Converting to ulong is the
// boundary where that stops being safe: PlanGraph uses it to cross into the
// plan's wire format, and an unchecked cast would silently wrap a negative
// value into a huge unsigned timestamp instead of failing loudly (#60).
namespace Tests.Time;

using ServiceSiteScheduling.Utilities;

public class TimeUlongConversionTests
{
    [Fact]
    public void ConvertingANonNegativeTime_ReturnsItsValue()
    {
        Time time = new(42);

        Assert.Equal(42UL, (ulong)time);
    }

    [Fact]
    public void ConvertingZero_ReturnsZero()
    {
        Time time = new(0);

        Assert.Equal(0UL, (ulong)time);
    }

    [Fact]
    public void ConvertingANegativeTime_ThrowsInsteadOfWrapping()
    {
        Time time = new(-1);

        Assert.Throws<OverflowException>(() => (ulong)time);
    }
}
