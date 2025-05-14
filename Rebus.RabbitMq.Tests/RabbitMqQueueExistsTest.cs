using System.Threading.Tasks;
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
    public async Task ThrowExceptionWhenQueueDoesNotExist()
    {
        var queueName = TestConfig.GetName("non-existing-queue");
        await RabbitMqTransportFactory.DeleteQueue(queueName);

        Assert.ThrowsAsync<RebusApplicationException>(() => _bus.Advanced.Routing.Send(queueName, "hej"));
    }

    [Test]
    public async Task DiscoversThatQueuesHaveBeenCreated()
    {
        var queueName = TestConfig.GetName("non-existing-queue");
        await RabbitMqTransportFactory.DeleteQueue(queueName);
        
        Assert.ThrowsAsync<RebusApplicationException>(() => _bus.Advanced.Routing.Send(queueName, "hej"));

        await RabbitMqTransportFactory.CreateQueue(queueName);
        
        Assert.That(async () =>
        {
            await _bus.Advanced.Routing.Send(queueName, "hej");
        }, Throws.Nothing);
    }
}