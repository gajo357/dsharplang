namespace DSharp.Runtime.Tests.Harness;

// Tests prescribe each draw; exhaustion or an invalid draw fails the test.
internal sealed class DeterministicRandom(params int[] values)
{
    private readonly Queue<int> pending = new(values);
    private readonly object gate = new();

    public int Next(int exclusiveUpperBound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveUpperBound);
        lock (gate)
        {
            if (!pending.TryPeek(out var value))
                throw new InvalidOperationException("No scripted random value remains.");
            if (value < 0 || value >= exclusiveUpperBound)
                throw new InvalidOperationException("Scripted random value is outside the requested range.");
            pending.Dequeue();
            return value;
        }
    }
}
