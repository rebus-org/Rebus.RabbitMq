using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Util;

namespace Rebus.RabbitMq
{
    class CustomQueueingConsumer : DefaultBasicConsumer, IQueueingBasicConsumer
    {
        public SharedQueue<BasicDeliverEventArgs> Queue { get; }

        public CustomQueueingConsumer(IModel model) : this(model, new SharedQueue<BasicDeliverEventArgs>())
        {
        }

        public CustomQueueingConsumer(IModel model, SharedQueue<BasicDeliverEventArgs> queue)
          : base(model)
        {
            Queue = queue;
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
    }
}