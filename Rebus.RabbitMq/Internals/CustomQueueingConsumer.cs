using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Rebus.Internals;

sealed class CustomQueueingConsumer(IChannel model) : AsyncDefaultBasicConsumer(model), IAsyncDisposable
{
    public Channel<BasicDeliverEventArgsReplacement> Queue { get; } = System.Threading.Channels.Channel.CreateUnbounded<BasicDeliverEventArgsReplacement>();

    public override Task HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange,
        string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken = default)
    {
        var args = new BasicDeliverEventArgsReplacement(deliveryTag: deliveryTag, properties: properties, body: body);

        Queue.Writer.TryWrite(args);

        return base.HandleBasicDeliverAsync(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body, cancellationToken);
    }

    public override Task HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason)
    {
        Queue.Writer.TryComplete();

        return base.HandleChannelShutdownAsync(channel, reason);
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