using NUnit.Framework;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Tests.Contracts;

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class RabbitMqQueueExistsTest : FixtureBase
{
    IBus _bus;

    protected override void SetUp()
    {
        var client = Using(new BuiltinHandlerActivator());

        _bus = Configure.With(client)
            .Logging(l => l.Console(minLevel: LogLevel.Warn))
            .Transport(t => t.UseRabbitMqAsOneWayClient(RabbitMqTransportFactory.ConnectionString))
            .Start();
    }

    [Test]
    public void ThrowExceptionWhenQueueDoesNotExist()
    {
        var queueName = TestConfig.GetName("non-existing-queue");
        RabbitMqTransportFactory.DeleteQueue(queueName);

        Assert.ThrowsAsync<RebusApplicationException>(() => _bus.Advanced.Routing.Send(queueName, "hej"));
    }
}