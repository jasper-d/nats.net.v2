namespace NATS.Client.Core;

public partial class NatsConnection
{
    /// <inheritdoc />
    public async ValueTask<INatsSub<T>> SubscribeAsync<T>(string subject, NatsSubOpts<T> opts, string? queueGroup = default, CancellationToken cancellationToken = default)
    {
        var sub = new NatsSub<T>(this, SubscriptionManager.GetManagerFor(subject), subject, queueGroup, opts);
        await SubAsync(subject, queueGroup, opts, sub, cancellationToken).ConfigureAwait(false);
        return sub;
    }
}
