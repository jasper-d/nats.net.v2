using System.Buffers;

namespace NATS.Client.Core;

public partial class NatsConnection
{
    /// <inheritdoc />
    public ValueTask PublishAsync(string subject, NatsHeaders? headers = default, string? replyTo = default, NatsPubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        if (opts?.WaitUntilSent ?? false)
        {
            return PubAsync(subject, replyTo, payload: default, headers, cancellationToken);
        }
        else
        {
            return PubPostAsync(subject, replyTo, payload: default, headers, cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask PublishAsync<T>(string subject, T? data, Action<T, IBufferWriter<byte>> serializer, NatsHeaders? headers = default, string? replyTo = default, NatsPubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        if (opts?.WaitUntilSent ?? false)
        {
            return PubModelAsync(subject, data, serializer, replyTo, headers, cancellationToken);
        }
        else
        {
            return PubModelPostAsync(subject, data, serializer, replyTo, headers, opts?.ErrorHandler, cancellationToken);
        }
    }

    /// <inheritdoc />
    public ValueTask PublishAsync<T>(in NatsMsg<T> msg, Action<T, IBufferWriter<byte>> serializer, NatsPubOpts? opts = default, CancellationToken cancellationToken = default)
    {
        return PublishAsync(msg.Subject, msg.Data, serializer, msg.Headers, msg.ReplyTo, opts, cancellationToken);
    }
    }
}
