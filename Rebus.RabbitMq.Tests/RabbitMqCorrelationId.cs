using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqCorrelationId : FixtureBase
{
    [Test]
    public async Task IncomingMessageHasRabbitMqCorrelationId()
    {
        var activator = Using(new BuiltinHandlerActivator());

        var messageHandled = new ManualResetEvent(initialState: false);
        var receivedCorrelationId = "";

        activator.Handle<string>(async (_, context, _) =>
        {
            if (context.Headers.TryGetValue(RabbitMqHeaders.CorrelationId, out var correlationId))
            {
                receivedCorrelationId = correlationId;
            }

            messageHandled.Set();
        });

        var bus = Configure.With(activator)
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, TestConfig.GetName("corr-id")))
            .Start();

        var expectedCorrelationId = Guid.NewGuid().ToString();
        var headers = new Dictionary<string, string>
        {
            [RabbitMqHeaders.CorrelationId] = expectedCorrelationId,
            [Headers.CorrelationId] = Guid.NewGuid().ToString()
        };

        await bus.SendLocal("hej med dig 🍟", headers);

        messageHandled.WaitOrDie(timeout: TimeSpan.FromSeconds(3));

        Assert.That(receivedCorrelationId, Is.EqualTo(expectedCorrelationId));
    }
}