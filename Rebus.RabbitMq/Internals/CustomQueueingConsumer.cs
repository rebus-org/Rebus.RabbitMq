using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace Rebus.Internals
{
    class CustomQueueingConsumer : DefaultBasicConsumer, IQueueingBasicConsumer
    {
        public SharedQueue<BasicDeliverEventArgs> Queue { get; } = new SharedQueue<BasicDeliverEventArgs>();

        public CustomQueueingConsumer(IModel model) : base(model)
        {
        }

        public override void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, byte[] body)
        {
            Queue.Enqueue(new BasicDeliverEventArgs
            {
                ConsumerTag = consumerTag,
                DeliveryTag = deliveryTag,
                Redelivered = redelivered,
                Exchange = exchange,
                RoutingKey = routingKey,
                BasicProperties = properties,
                Body = body
            });
        }

        public override void OnCancel()
        {
            base.OnCancel();
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