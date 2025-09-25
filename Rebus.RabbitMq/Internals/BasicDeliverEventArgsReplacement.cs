using System;
using RabbitMQ.Client;

namespace Rebus.Internals;

public class BasicDeliverEventArgsReplacement(ulong deliveryTag, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body)
{
    public ulong DeliveryTag { get; } = deliveryTag;
    public IReadOnlyBasicProperties Properties { get; } = properties;
    
    /// <summary>
    /// it's important to take a copy of the message body here, because the memory area pointed to by the body reference will be mutated later
    /// </summary>
    public byte[] Body { get; } = body.ToArray();
}