using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable AccessToDisposedClosure
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests
{
    [TestFixture]
    public class RabbitMqReceiveSubscriptionTests : FixtureBase
    {
        readonly string _publisherQueueName = TestConfig.GetName("publisher-RabbitMqReceiveSubscriptionTests");
        readonly string _subscriberQueueName = TestConfig.GetName("subscriber-RabbitMqReceiveSubscriptionTests");

        protected override void TearDown()
        {
            base.TearDown();
            RabbitMqTransportFactory.DeleteQueue(_publisherQueueName);
            RabbitMqTransportFactory.DeleteQueue(_subscriberQueueName);
        }

        [Test]
        public async Task ReceiveOnSubscribe_WHEN_SubscriberQueueDeleted_THEN_ItRecreates_SubscriberQueue_AND_ReceivesPublishedData()
        {
            const string message = "Test-Message-123";

            using (var receivedEvent = new ManualResetEvent(false))
            {
                using (var publisher = StartBus(_publisherQueueName))
                {
                    async Task HandlerMethod(string data)
                    {
                        if (string.Equals(data, message))
                        {
                            receivedEvent.Set();
                        }
                    }

                    using (var subscriber = StartBus(_subscriberQueueName, HandlerMethod))
                    {
                        await subscriber.Subscribe<string>();
                        subscriber.Advanced.Workers.SetNumberOfWorkers(1);

                        // remove the input queue
                        RabbitMqTransportFactory.DeleteQueue(_subscriberQueueName);

                        // wait a short while
                        await Task.Delay(10);

                        // check that published message is received without problems
                        await publisher.Publish(message);

                        receivedEvent.WaitOrDie(TimeSpan.FromSeconds(2),
                            "The event has not been received by the subscriber within the expected time");
                    }
                }
            }
        }


        IBus StartBus(string queueName, Func<string, Task> handlerMethod = null)
        {
            var activator = new BuiltinHandlerActivator();

            Using(activator);

            if (handlerMethod != null) {
                activator.Handle(handlerMethod);
            }

            Configure.With(activator)
                .Transport(t =>
                {
                    var properties = new Dictionary<string, string>
                    {
                        { "description", "Created for RabbitMqReceiveTests" }
                    };

                    t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName)
                        .AddClientProperties(properties);
                }).Options(o => o.SetNumberOfWorkers(0))
                .Start();

            return activator.Bus;
        }
    }
}
