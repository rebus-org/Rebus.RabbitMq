using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading.Channels;

namespace Rebus.Internals;

class CustomQueueingConsumer : DefaultBasicConsumer
{
    public Channel<BasicDeliverEventArgs> Queue { get; } = Channel.CreateUnbounded<BasicDeliverEventArgs>();
        
    public CustomQueueingConsumer(IModel model) : base(model)
    {
    }

    public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
    {
        Queue.Writer.TryWrite(new BasicDeliverEventArgs
        {
            ConsumerTag = consumerTag,
            DeliveryTag = deliveryTag,
            Redelivered = redelivered,
            Exchange = exchange,
            RoutingKey = routingKey,
            BasicProperties = properties,
                
            //      \/- it's important to take a copy of the message body here, because the memory area pointed to by the body reference will be mutated later
            Body = body.ToArray()
        });
    }

    public override void OnCancel(params string[] consumerTags)
    {
        base.OnCancel(consumerTags);
        Queue.Writer.TryComplete();
    }

    public void Dispose()
    {
        Model.SafeDrop();
        Queue.Writer.TryComplete();
    }
}