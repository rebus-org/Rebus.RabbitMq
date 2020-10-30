using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Rebus.Internals
{
    class CustomQueueingConsumer : DefaultBasicConsumer
    {
        public SharedQueue<BasicDeliverEventArgs> Queue { get; } = new SharedQueue<BasicDeliverEventArgs>();
        public CustomQueueingConsumer(IModel model) : base(model)
        {
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            Queue.Enqueue(new BasicDeliverEventArgs
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

        public override void OnCancel(params string[] consumerTags)
        {
            base.OnCancel(consumerTags);
            Queue.Close();
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