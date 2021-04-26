using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Threading.Channels;

namespace Rebus.Internals
{
    class CustomQueueingConsumer : DefaultBasicConsumer
    {
        public Channel<BasicDeliverEventArgs> Queue { get; } = Channel.CreateUnbounded<BasicDeliverEventArgs>();
        public CustomQueueingConsumer(IModel model) : base(model)
        {
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
        {
            Console.WriteLine("Handle basic deliver");
            Queue.Writer.TryWrite(new BasicDeliverEventArgs
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
            Console.WriteLine("Completing");
            Queue.Writer.Complete();
        }

        public void Dispose()
        {
            Console.WriteLine("Disposing");
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
