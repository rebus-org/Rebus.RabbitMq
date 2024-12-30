using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
// ReSharper disable ArgumentsStyleNamedExpression

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests
{
    [TestFixture]
    public class RabbitMqConsistentHashExchangeTest : FixtureBase
    {
        [Test]
        public async Task WorksWithConsistentHashExchange()
        {
            const string connectionString = RabbitMqTransportFactory.ConnectionString;

            const string exchangeName = "RebusTestConsistentHashExchange";
            const string queueNamePattern = "consistent-hash-xchange-bound-queue";

            using (var activator = new BuiltinHandlerActivator())
            {
                Configure.With(activator)
                    .Transport(t =>
                    {
                        t
                            .UseRabbitMq(connectionString, queueNamePattern)
                            // Create 2 Queues & bind to the same consistent hash exchange:
                            .UseConsistentHashExchange(2, exchangeName);
                    })
                    .Start();

                // Send N messages to spread semi-evenly among queues
                for (int i = 1; i <= 10; i++)
                {
                    await activator.Bus.Advanced.Routing.Send($"{i}@{exchangeName}", $"test_{i}", new Dictionary<string, string>());
                }
            }

            // Assert the exchange and all queues were created:
            Assert.That(RabbitMqTransportFactory.ExchangeExists(exchangeName), Is.True);
            Assert.That(RabbitMqTransportFactory.QueueExists(queueNamePattern + "_1"), Is.True);
            Assert.That(RabbitMqTransportFactory.QueueExists(queueNamePattern + "_2"), Is.True);
        }
    }
}