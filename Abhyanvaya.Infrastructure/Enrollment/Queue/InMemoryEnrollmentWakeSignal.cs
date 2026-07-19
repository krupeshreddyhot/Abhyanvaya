using System.Threading.Channels;
using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Infrastructure.Enrollment.Queue;

/// <summary>
/// In-memory wake signal for enrollment workers (docs/AI20_ENROLLMENT_BACKGROUND.md §3.1).
/// Item claiming is implemented by the background worker in a later milestone.
/// </summary>
public sealed class InMemoryEnrollmentWakeSignal : IEnrollmentJobQueue
{
    private readonly Channel<byte> _wake = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    private volatile bool _signalThrows;

    public void SignalWork()
    {
        if (_signalThrows)
        {
            throw new InvalidOperationException("Enrollment queue wake signal failed.");
        }

        _wake.Writer.TryWrite(0);
    }

    public async ValueTask WaitForSignalAsync(CancellationToken cancellationToken = default)
    {
        _ = await _wake.Reader.ReadAsync(cancellationToken);
    }

    public async IAsyncEnumerable<Guid> DequeueClaimedJobIdsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var _ in _wake.Reader.ReadAllAsync(cancellationToken))
        {
            yield break;
        }
    }

    internal void ConfigureThrowOnSignal(bool throws) => _signalThrows = throws;
}
