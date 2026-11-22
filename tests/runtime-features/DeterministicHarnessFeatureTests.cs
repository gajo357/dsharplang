using DSharp.Runtime.Tests.Harness;
using Xunit;

namespace DSharp.Runtime.Tests.Features;

public sealed class DeterministicHarnessFeatureTests
{
    [Fact]
    public void ScriptedDelayCanBeCheckedWithoutWaitingForWallTime()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var jitter = new DeterministicRandom(2);
        var due = clock.UtcNow.AddSeconds(5 + jitter.Next(3));
        clock.Advance(TimeSpan.FromSeconds(6));
        Assert.True(clock.UtcNow < due);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(due, clock.UtcNow);
    }
}
