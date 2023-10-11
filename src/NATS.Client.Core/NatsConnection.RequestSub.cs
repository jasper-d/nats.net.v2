using System.Buffers;

namespace NATS.Client.Core;

public partial class NatsConnection
{
    internal async ValueTask<INatsSub<TReply>> RequestSubAsync<TRequest, TReply>(
        string subject,
        TRequest? data,
        Action<TRequest, IBufferWriter<byte>> serialize,
        NatsSubOpts<TReply> replyOpts,
        NatsHeaders? headers = default,
        NatsPubOpts? requestOpts = default,
        CancellationToken cancellationToken = default)
    {
        var replyTo = NewInbox();

        var sub = new NatsSub<TReply>(this, SubscriptionManager.InboxSubBuilder, replyTo, queueGroup: default, replyOpts);
        await SubAsync(replyTo, queueGroup: default, replyOpts, sub, cancellationToken).ConfigureAwait(false);

        if (requestOpts?.WaitUntilSent == true)
        {
            await PubModelAsync(subject, data, serialize, replyTo, headers, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await PubModelPostAsync(subject, data, serialize, replyTo, headers, requestOpts?.ErrorHandler, cancellationToken).ConfigureAwait(false);
        }

        return sub;
    }
}
