using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NATS.Client.Core;

public static class NatsRequestExtensions
{
    public static ValueTask PublishJsonAsync<T>(this INatsConnection conn, string subject, T obj, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        // NOTE: This allocates because typeInfo must be captured.
        //       To avoid the allocation, there could be a PublisAsync overload
        //       which takes another argument "serializationState" which would
        //       be passed to the serializer, similar to TState
        //       ThreadPool.QueueUserWorkItem(Action<TState>, TState, bool) or
        //       CancellationToken.UnsafeRegister(Action<object> callback, object state)
        //       As an alternative, users could writer their own extension methods
        //       for JsonTypeInfo<T> and use the static properties of a generated
        //       JsonSerializerContext
        void Serialize(T obj, IBufferWriter<byte> bw)
        {
            // This could be pooled
            using var writer = new Utf8JsonWriter(bw);
            JsonSerializer.Serialize(writer, obj, typeInfo);
        }

        return conn.PublishAsync(subject, obj, Serialize, null, null, null, cancellationToken);
    }

    /// <summary>
    /// Request and receive a single reply from a responder.
    /// </summary>
    /// <param name="nats">NATS connection</param>
    /// <param name="msg">Message to be sent as request</param>
    /// <param name="requestOpts">Request publish options</param>
    /// <param name="serialize"></param>
    /// <param name="replyOpts">Reply handler subscription options</param>
    /// <param name="cancellationToken">Cancel this request</param>
    /// <typeparam name="TRequest">Request type</typeparam>
    /// <typeparam name="TReply">Reply type</typeparam>
    /// <returns>Returns the <see cref="NatsMsg{T}"/> received from the responder as reply.</returns>
    /// <exception cref="OperationCanceledException">Raised when cancellation token is used</exception>
    /// <remarks>
    /// Response can be (null) or one <see cref="NatsMsg{T}"/>.
    /// Reply option's max messages will be set to 1.
    /// if reply option's timeout is not defined then it will be set to NatsOpts.RequestTimeout.
    /// </remarks>
    public static ValueTask<NatsMsg<TReply>> RequestAsync<TRequest, TReply>(
        this INatsConnection nats,
        in NatsMsg<TRequest> msg,
        Action<TRequest, IBufferWriter<byte>> serialize,
        NatsSubOpts<TReply> replyOpts,
        NatsPubOpts? requestOpts = default,
        CancellationToken cancellationToken = default)
    {
        CheckMsgForRequestReply(msg);

        return nats.RequestAsync(
            msg.Subject,
            msg.Data,
            serialize,
            replyOpts,
            msg.Headers,
            requestOpts,
            cancellationToken);
    }

    internal static void CheckMsgForRequestReply<T>(in NatsMsg<T> msg) => CheckForRequestReply(msg.ReplyTo);

    private static void CheckForRequestReply(string? replyTo)
    {
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            throw new NatsException($"Can't set reply-to for a request");
        }
    }
}
