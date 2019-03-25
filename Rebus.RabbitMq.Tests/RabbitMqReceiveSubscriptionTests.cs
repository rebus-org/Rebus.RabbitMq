using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        public async Task Test_ReceieveOnSubscribe_WHEN_SubscriberQueueDeleted_THEN_ItRecreates_SubscirberQuere_AND_ReceivesPublishedData()
        {
            string message = "Test-Message-123";

            using (var receivedEvent = new ManualResetEvent(false))
            {
                using (var publisher = StartBus(_publisherQueueName))
                {
                    using (var subscriber = StartBus(_subscriberQueueName, data => Task.Run(() =>
                       {
                           if (string.Equals(data, message))
                               receivedEvent.Set();
                       })))
                    {

                        await subscriber.Subscribe<string>();

                        RabbitMqTransportFactory.DeleteQueue(_subscriberQueueName);
                        await Task.Delay(1000);

                        await publisher.Publish(message);

                        receivedEvent.WaitOrDie(TimeSpan.FromSeconds(2), "The event has not been receved by the subscriber within the expected time");
                    }
                }
            }
        }


        private IBus StartBus(string queueName, Func<string, Task> handlerMethod = null)
        {
            var activator = Using(new BuiltinHandlerActivator());
            activator?.Handle(handlerMethod);

            Configure.With(activator).Transport(t =>
            {
                t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName)
                    .AddClientProperties(new Dictionary<string, string> { { "description", "Created for RabbitMqReceiveTests" } });
            }).Start();

            return activator.Bus;
        }
    }
}
