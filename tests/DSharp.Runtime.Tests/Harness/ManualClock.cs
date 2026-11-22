namespace DSharp.Runtime.Tests.Harness;

// Test-only time source: progress happens only when a test explicitly advances it.
internal sealed class ManualClock(DateTimeOffset initial)
{
    private readonly object gate = new();
    private DateTimeOffset now = initial.ToUniversalTime();

    public DateTimeOffset UtcNow
    {
        get { lock (gate) return now; }
    }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        lock (gate) now = now.Add(elapsed);
    }
}
