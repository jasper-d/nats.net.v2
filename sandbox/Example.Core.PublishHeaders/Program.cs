// > nats sub bar.*

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

var subject = "bar.xyz";
var options = NatsOpts.Default with { LoggerFactory = new MinimumConsoleLoggerFactory(LogLevel.Error) };

Print("[CON] Connecting...\n");

await using var connection = new NatsConnection(options);

for (var i = 0; i < 10; i++)
{
    Print($"[PUB] Publishing to subject ({i}) '{subject}'...\n");
    await connection.PublishAsync<Bar>(
        subject,
        new Bar { Id = i, Name = "Baz" },
        (obj, bw) => BarJsonContext.Default.Bar.Serialize(obj, bw),
        headers: new NatsHeaders { ["XFoo"] = $"bar{i}" });
}

void Print(string message)
{
    Console.Write($"{DateTime.Now:HH:mm:ss} {message}");
}


public record Bar
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

[JsonSerializable(typeof(Bar))]
public sealed partial class BarJsonContext : JsonSerializerContext
{

}

public static class JsonExtension
{
    public static void Serialize<T>(this JsonTypeInfo<T> typeInfo, T obj, IBufferWriter<byte> bw)
    {
        // writer could be pooled...
        using var writer = new Utf8JsonWriter(bw);
        JsonSerializer.Serialize(writer, obj, typeInfo);
    }
}
