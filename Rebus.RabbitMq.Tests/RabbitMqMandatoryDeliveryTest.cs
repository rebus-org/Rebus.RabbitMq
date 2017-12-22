using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
        readonly string _noneExistingQueueName = TestConfig.GetName("non-existing-queue");

        protected override void SetUp()
        {
            RabbitMqTransportFactory.DeleteQueue(_noneExistingQueueName);
        }

        [Test]
        public void MandatoryHeaderWithoutHandlerThrows()
        {
            var bus = StartOneWayClient(null);

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

            void Callback(object sender, BasicReturnEventArgs eventArgs)
            {
                if (eventArgs.Message.GetMessageId().Equals(messageId.ToString()))
                {
                    gotCallback.Set();
                }
            }

            var bus = StartOneWayClient(Callback);

            await bus.Advanced.Routing.Send(_noneExistingQueueName, "I'm mandatory", new Dictionary<string, string>
            {
                [RabbitMqHeaders.MessageId] = messageId.ToString(),
                [RabbitMqHeaders.Mandatory] = bool.TrueString,
            });

            gotCallback.WaitOrDie(TimeSpan.FromSeconds(2));
        }

        IBus StartOneWayClient(Action<object, BasicReturnEventArgs> basicReturnCallback)
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
