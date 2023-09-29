using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using NATS.Client.Core;

namespace AotApp;

public struct S
{
    public long A;
}

public static class Program
{
    public static async Task Main()
    {
        var value = new S { A = 42 };
        using var timeout = new CancellationTokenSource(5_000);
        await using var pubConn = new NatsConnection();
        await using var subCon = new NatsConnection();

        await pubConn.ConnectAsync();
        await subCon.ConnectAsync();

        var pubOpts = new NatsPubOpts { WaitUntilSent = true };
        var subOpts = new NatsSubOpts { };

        await using var sub = await subCon.SubscribeAsync<S>("foo", opts: subOpts, cancellationToken: timeout.Token);

        await Task.Delay(500);

        await pubConn.PublishAsync<S>("foo", value, opts: pubOpts, cancellationToken: timeout.Token);

        await sub.Msgs.WaitToReadAsync(timeout.Token);

        sub.Msgs.TryRead(out var result);

        if (result.Data.A != value.A)
        {
            throw new InvalidOperationException($"Unexpected value {result.Data.A}");
        }

        Console.WriteLine($"Retrieved value {result.Data}");
    }

    public sealed class Serializer : INatsSerializer
    {
        public int Serialize<T>(ICountableBufferWriter bufferWriter, T? value)
        {
            if (value is null)
            {
                return 0;
            }

            if (typeof(T) == typeof(S))
            {
                var buf = bufferWriter.GetMemory(Unsafe.SizeOf<long>());
                var s = (S)(object)value;
                Unsafe.WriteUnaligned(ref buf.Span[0], s.A);
                bufferWriter.Advance(Unsafe.SizeOf<long>());
                return Unsafe.SizeOf<long>();
            }

            if (typeof(T) == typeof(string))
            {
                var byteCount = Encoding.ASCII.GetByteCount((string)(object)value);
                var buf = bufferWriter.GetMemory(byteCount);
                var written = Encoding.ASCII.GetBytes((string)(object)value, buf.Span);
                bufferWriter.Advance(written);
                return written;
            }

            throw new NotSupportedException();
        }

        public T? Deserialize<T>(in ReadOnlySequence<byte> buffer)
        {
            if (typeof(T) == typeof(string))
            {
                var str = Encoding.ASCII.GetString(buffer);
                return (T)(object)str;
            }

            if (typeof(T) == typeof(S))
            {
                var s = default(S);
                s.A = Unsafe.ReadUnaligned<long>(ref buffer.ToArray().AsSpan()[0]);
                return (T)(object)s;
            }

            throw new NotSupportedException();
        }

        public object? Deserialize(in ReadOnlySequence<byte> buffer, Type type)
        {
            if (type == typeof(string))
            {
                var str = Encoding.ASCII.GetString(buffer);
                return str;
            }

            if (type == typeof(S))
            {
                var s = default(S);
                s.A = Unsafe.ReadUnaligned<long>(ref buffer.ToArray().AsSpan()[0]);
                return s;
            }

            throw new NotSupportedException();
        }
    }
}
