using DSharp.Runtime.Tests.Harness;
using Xunit;

namespace DSharp.Runtime.Tests;

public sealed class HarnessTests
{
    [Fact]
    public void ClockAdvancesOnlyWhenRequestedAndRejectsBackwardTime()
    {
        var start = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));
        var clock = new ManualClock(start);
        var other = new ManualClock(start);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(start.ToUniversalTime().AddSeconds(3), clock.UtcNow);
        Assert.Equal(start.ToUniversalTime(), other.UtcNow);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
        Assert.Equal(start.ToUniversalTime().AddSeconds(3), clock.UtcNow);
    }

    [Fact]
    public void RandomDrawsAreScriptedBoundedAndDoNotConsumeAnInvalidValue()
    {
        var random = new DeterministicRandom(1, 3);
        Assert.Equal(1, random.Next(2));
        Assert.Throws<InvalidOperationException>(() => random.Next(3));
        Assert.Equal(3, random.Next(4));
        Assert.Throws<InvalidOperationException>(() => random.Next(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(0));
    }
}
