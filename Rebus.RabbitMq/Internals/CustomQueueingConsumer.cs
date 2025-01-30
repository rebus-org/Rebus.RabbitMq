using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Rebus.Internals;

sealed class CustomQueueingConsumer : AsyncDefaultBasicConsumer, IAsyncDisposable
{
    public Channel<BasicDeliverEventArgs> Queue { get; } = System.Threading.Channels.Channel.CreateUnbounded<BasicDeliverEventArgs>();
        
    public CustomQueueingConsumer(IChannel model) : base(model)
    {
    }

    public override Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
        string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        Queue.Writer.TryWrite(new BasicDeliverEventArgs(
            consumerTag: consumerTag,
            deliveryTag: deliveryTag,
            redelivered: redelivered,
            exchange: exchange,
            routingKey: routingKey,
            properties: properties,
            //      \/- it's important to take a copy of the message body here, because the memory area pointed to by the body reference will be mutated later
            // Also the underlying ReadOnlyMemory<byte> is not guaranteed to be valid after the method returns
            body: body.ToArray(), cancellationToken: cancellationToken));
        return base.HandleBasicDeliverAsync(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body, cancellationToken);
    }

    protected override Task OnCancelAsync(string[] consumerTags, CancellationToken cancellationToken = default)
    {
        Queue.Writer.TryComplete();
        return base.OnCancelAsync(consumerTags, cancellationToken);
    }


    public async ValueTask DisposeAsync()
    {
        Queue.Writer.TryComplete();
        await Channel.SafeDropAsync();
    }
}