using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NATS.Client.Core.Internal;

namespace NATS.Client.Core;

public partial class NatsConnection
{
    private static readonly NatsSubOptsBase DefaultReplyOpts = NatsSubOptsBase.Default.Instance;

    /// <inheritdoc />
    public string NewInbox() => NewInbox(InboxPrefix);

    /// <inheritdoc />
    public async ValueTask<NatsMsg<TReply>> RequestAsync<TRequest, TReply>(
        string subject,
        TRequest data,
        Action<TRequest, IBufferWriter<byte>> serializer,
        NatsSubOpts<TReply> replyOpts,
        NatsHeaders? headers = default,
        NatsPubOpts? requestOpts = default,
        CancellationToken cancellationToken = default)
    {
        var opts = SetReplyOptsDefaults(replyOpts);

        await using var sub = await RequestSubAsync(subject, data, serializer, opts, headers, requestOpts, cancellationToken)
            .ConfigureAwait(false);

        if (await sub.Msgs.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (sub.Msgs.TryRead(out var msg))
            {
                return msg;
            }
        }

        throw new Exception("Nobody responding :(");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NatsMsg<TReply>> RequestManyAsync<TRequest, TReply>(
        string subject,
        TRequest? data,
        Action<TRequest, IBufferWriter<byte>> serializer,
        NatsSubOpts<TReply> replyOpts,
        NatsHeaders? headers = default,
        NatsPubOpts? requestOpts = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var sub = await RequestSubAsync(subject, data, serializer, replyOpts, headers, requestOpts, cancellationToken)
            .ConfigureAwait(false);

        while (await sub.Msgs.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (sub.Msgs.TryRead(out var msg))
            {
                // Received end of stream sentinel
                if (msg.Data is null)
                {
                    yield break;
                }

                yield return msg;
            }
        }
    }

    [SkipLocalsInit]
    private static string NewInbox(ReadOnlySpan<char> prefix)
    {
        Span<char> buffer = stackalloc char[64];
        var separatorLength = prefix.Length > 0 ? 1u : 0u;
        var totalLength = (uint)prefix.Length + (uint)NuidWriter.NuidLength + separatorLength;
        if (totalLength <= buffer.Length)
        {
            buffer = buffer.Slice(0, (int)totalLength);
        }
        else
        {
            buffer = new char[totalLength];
        }

        var totalPrefixLength = (uint)prefix.Length + separatorLength;
        if ((uint)buffer.Length > totalPrefixLength && (uint)buffer.Length > (uint)prefix.Length)
        {
            prefix.CopyTo(buffer);
            buffer[prefix.Length] = '.';
            var remaining = buffer.Slice((int)totalPrefixLength);
            var didWrite = NuidWriter.TryWriteNuid(remaining);
            Debug.Assert(didWrite, "didWrite");
            return new string(buffer);
        }

        return Throw();

        [DoesNotReturn]
        string Throw()
        {
            Debug.Fail("Must not happen");
            throw new InvalidOperationException("This should never be raised!");
        }
    }

    private NatsSubOpts<T> SetReplyOptsDefaults<T>(NatsSubOpts<T> replyOpts)
    {
        if ((replyOpts.Timeout ?? default) == default)
        {
            return replyOpts with { Timeout = Opts.RequestTimeout };
        }

        return replyOpts;
    }
}
