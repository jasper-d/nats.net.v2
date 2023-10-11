using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;

var subject = "bar.*";
var options = NatsOpts.Default with { LoggerFactory = new MinimumConsoleLoggerFactory(LogLevel.Error) };
var subOpts = new NatsSubOpts<Bar?>() { Serializer = mem => JsonSerializer.Deserialize(mem.Memory.Span, BarJsonContext.Default.Bar), };

Print("[CON] Connecting...\n");

await using var connection = new NatsConnection(options);

Print($"[SUB] Subscribing to subject '{subject}'...\n");

INatsSub<Bar?> sub = await connection.SubscribeAsync(subject, subOpts);

await foreach (var msg in sub.Msgs.ReadAllAsync())
{
    Print($"[RCV] {msg.Subject}: {msg.Data}\n");
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

