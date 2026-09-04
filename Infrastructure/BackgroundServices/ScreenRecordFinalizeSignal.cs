namespace Luxira.Api.Infrastructure.BackgroundServices;

/// <summary>Coalesces recording-day rollover notifications into one background migration pass.</summary>
public sealed class ScreenRecordFinalizeSignal : IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Request()
    {
        try { _signal.Release(); }
        catch (SemaphoreFullException) { }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _signal.WaitAsync(timeout, ct);

    public void Dispose() => _signal.Dispose();
}
