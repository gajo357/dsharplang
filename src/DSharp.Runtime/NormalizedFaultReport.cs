namespace DSharp.Runtime;

// These are caller-normalized observations. This reference does not normalize
// exceptions, redact diagnostics, authenticate origins or propagate actor chains.
internal sealed class NormalizedFaultObservation
{
    private readonly byte[] diagnostic;
    private readonly byte[] metadata;

    public NormalizedFaultObservation(string classification, byte[] diagnostic, byte[] metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classification);
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(metadata);
        Classification = classification;
        this.diagnostic = (byte[])diagnostic.Clone();
        this.metadata = (byte[])metadata.Clone();
    }

    public string Classification { get; }
    public byte[] Diagnostic => (byte[])diagnostic.Clone();
    public byte[] Metadata => (byte[])metadata.Clone();
}

internal sealed class NormalizedFaultReport
{
    public NormalizedFaultReport(NormalizedFaultObservation? primary,
        IEnumerable<NormalizedFaultObservation> cleanup, Guid? originInvocationId = null)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        if (originInvocationId == Guid.Empty)
            throw new ArgumentException("A supplied origin must be nonempty.", nameof(originInvocationId));

        // Materialize at construction, before a report can be passed into the
        // store. Caller enumeration never runs under a store transaction.
        var copied = new List<NormalizedFaultObservation>();
        foreach (var observation in cleanup)
        {
            if (observation is null)
                throw new ArgumentException("Cleanup observations cannot contain null.", nameof(cleanup));
            copied.Add(observation);
        }
        if (primary is null && copied.Count == 0)
            throw new ArgumentException("At least one fault observation is required.", nameof(cleanup));
        Primary = primary;
        Cleanup = Array.AsReadOnly(copied.ToArray());
        OriginInvocationId = originInvocationId;
    }

    public NormalizedFaultObservation? Primary { get; }
    public IReadOnlyList<NormalizedFaultObservation> Cleanup { get; }
    public Guid? OriginInvocationId { get; }
}
