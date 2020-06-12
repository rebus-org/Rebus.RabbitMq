using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Rebus.Internals
{
    class CustomQueueingConsumer : DefaultBasicConsumer
    {
        public ConcurrentQueue<BasicDeliverEventArgs> Queue { get; } = new ConcurrentQueue<BasicDeliverEventArgs>();

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
                Body = body
            });
        }

        public override void HandleBasicCancelOk(string consumerTag)
        {
            NackReceivedMessages();

            base.HandleBasicCancelOk(consumerTag);
        }

        public async Task<BasicDeliverEventArgs> TryDequeue(TimeSpan patience, CancellationToken cancellationToken)
        {
            if (Queue.TryDequeue(out var result)) return result;

            using (var cancellationTokenSource = new CancellationTokenSource(patience))
            {
                try
                {
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);

                        if (Queue.TryDequeue(out var ea)) return ea;
                    }
                }
                catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                {
                    // we're on out way out now
                }
            }

            return null;
        }

        void NackReceivedMessages()
        {
            try
            {
                while (Queue.TryDequeue(out var message))
                {
                    Model.BasicCancelNoWait(message.ConsumerTag);
                }
            }
            catch
            {
            }
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