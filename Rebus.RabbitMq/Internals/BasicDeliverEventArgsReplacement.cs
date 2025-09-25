using System;
using System.Threading;
using RabbitMQ.Client;

namespace Rebus.Internals;

public class BasicDeliverEventArgsReplacement(
    string consumerTag, ulong deliveryTag, bool redelivered,
    string exchange, string routingKey, IReadOnlyBasicProperties properties, 
    ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
{
    public string ConsumerTag { get; } = consumerTag;
    public ulong DeliveryTag { get; } = deliveryTag;
    public bool Redelivered { get; } = redelivered;
    public string Exchange { get; } = exchange;
    public string RoutingKey { get; } = routingKey;
    public IReadOnlyBasicProperties Properties { get; } = properties;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    
    /// <summary>
    /// it's important to take a copy of the message body here, because the memory area pointed to by the body reference will be mutated later
    /// </summary>
    public byte[] Body { get; } = body.ToArray();
}