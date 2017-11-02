using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client.Events;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

namespace Rebus.RabbitMq.Tests
{
    [TestFixture, Category("rabbitmq")]
    public class RabbitMqMandatoryDeliveryTest : FixtureBase
    {
        private readonly string _noneExistingQueueName = TestConfig.GetName("non-existing-queue");

        protected override void SetUp()
        {
            RabbitMqTransportFactory.DeleteQueue(_noneExistingQueueName);
        }

        [Test]
        public async Task MandatoryHeaderWithoutHandlerThrows()
        {
            Action<object, BasicReturnEventArgs> callback = null;
            var bus = StartOneWayClient(callback);

            Assert.ThrowsAsync<MandatoryDeliveryException>(async () =>
            {
                await bus.Advanced.Routing.Send(_noneExistingQueueName, "I'm mandatory", new Dictionary<string, string>
                {
                    [RabbitMqHeaders.Mandatory] = bool.TrueString,
                });
            });
        }

        [Test]
        public async Task MandatoryHeaderCallback()
        {
            var messageId = Guid.NewGuid();
            var gotCallback = new ManualResetEvent(false);

            Action<object, BasicReturnEventArgs> callback = (sender, eventArgs) =>
            {
                if (eventArgs.BasicProperties.MessageId.Equals(messageId.ToString()))
                {
                    gotCallback.Set();
                }
            };

            var bus = StartOneWayClient(callback);

            await bus.Advanced.Routing.Send(_noneExistingQueueName, "I'm mandatory", new Dictionary<string, string>
            {
                [RabbitMqHeaders.MessageId] = messageId.ToString(),
                [RabbitMqHeaders.Mandatory] = bool.TrueString,
            });

            gotCallback.WaitOrDie(TimeSpan.FromSeconds(2));
        }

        private IBus StartOneWayClient(Action<object, BasicReturnEventArgs> basicReturnCallback)
        {
            var client = Using(new BuiltinHandlerActivator());

            return Configure.With(client)
                .Logging(l => l.Console(minLevel: LogLevel.Warn))
                .Transport(t => t.UseRabbitMqAsOneWayClient(RabbitMqTransportFactory.ConnectionString)
                    .Mandatory(basicReturnCallback))
                .Start();
        }
    }
}
