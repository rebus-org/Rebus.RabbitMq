using NUnit.Framework;
using Rebus.Activation;
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
        readonly string _publisherQueueName = TestConfig.GetName("publisher");
        readonly string _subscriberQueueName = TestConfig.GetName("subscriber");

        [Test]
        public async Task Test_ReceieveOnSubscribe_WHEN_SubscriberQueueDeleted_THEN_ItRecreates_SubscirberQuere_AND_ReceivesPublishedData()
        {
            var message = "Test-Message-123";
            var receivedEvent = new ManualResetEvent(false);
            var publisher = GetBus("publisher");

            var subscriber = GetBus("subscriber", async data =>
            {
                if (string.Equals(data, message))
                    receivedEvent.Set();
            });

            await subscriber.Bus.Subscribe<string>();

            RabbitMqTransportFactory.DeleteQueue(_subscriberQueueName);

            await publisher.Bus.Publish(message);

            receivedEvent.WaitOrDie(TimeSpan.FromSeconds(5));
        }


        BuiltinHandlerActivator GetBus(string queueName, Func<string, Task> handlerMethod = null)
        {
            var activator = Using(new BuiltinHandlerActivator());
            activator?.Handle(handlerMethod);

            Configure.With(activator).Transport(t =>
            {
                t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName)
                    .AddClientProperties(new Dictionary<string, string> { { "description", "Created for RabbitMqReceiveTests" } });
            }).Start();

            return activator;
        }
    }
}
