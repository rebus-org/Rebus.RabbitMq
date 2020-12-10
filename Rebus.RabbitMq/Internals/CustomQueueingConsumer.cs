using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Rebus.Internals
{
    class CustomQueueingConsumer : AsyncDefaultBasicConsumer
    {
        private Channel<BasicDeliverEventArgs> Queue { get; }
        public CustomQueueingConsumer(IModel model, int maxSize) : base(model)
        {
            // Use a bounded queue to avoid a memory explosion somewhere in case something goes haywire.
            Queue = Channel.CreateBounded<BasicDeliverEventArgs>(maxSize);
        }

        public override async Task HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            await Queue.Writer.WriteAsync(new BasicDeliverEventArgs
            {
                ConsumerTag = consumerTag,
                DeliveryTag = deliveryTag,
                Redelivered = redelivered,
                Exchange = exchange,
                RoutingKey = routingKey,
                BasicProperties = properties,
                Body = body.ToArray()
            });
        }

        public override Task OnCancel(params string[] consumerTags)
        {
            base.OnCancel(consumerTags);
            Queue.Writer.Complete();
            return Task.CompletedTask;
        }

        public ValueTask<BasicDeliverEventArgs> GetNext(CancellationToken cancellationToken)
        {
            return Queue.Reader.ReadAsync(cancellationToken);
        }

        public void Dispose()
        {
            try
            {
                // it's so fucked up that these can throw exceptions
                Model?.Close();
                Model?.Dispose();
            }
            catch
            {
            }
        }
    }
}