using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using RabbitMQ.Client;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
[Ignore("just playing around")]
public class CanConnectToBrokerBothWays : FixtureBase
{
    [Test]
    public async Task ItWorks_ConnectionString()
    {
        var activator = Using(new BuiltinHandlerActivator());

        Configure.With(activator)
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, TestConfig.GetName("random-queue")))
            .Start();

        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ItWorks_ConnectionEndpoints()
    {
        var activator = Using(new BuiltinHandlerActivator());

        Configure.With(activator)
            .Transport(t => t.UseRabbitMq(new List<ConnectionEndpoint> { new() { ConnectionString = RabbitMqTransportFactory.ConnectionString } }, TestConfig.GetName("random-queue")))
            .Start();

        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    [Test]
    public void CheckThisOut()
    {
        var uri = new Uri(RabbitMqTransportFactory.ConnectionString);
        var endpoint = new AmqpTcpEndpoint(uri);
    }
}