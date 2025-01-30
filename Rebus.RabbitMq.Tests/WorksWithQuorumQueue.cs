using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
// ReSharper disable ArgumentsStyleOther
// ReSharper disable ArgumentsStyleLiteral
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class WorksWithQuorumQueue : FixtureBase
{
    [Test]
    public async Task CanDoAllThisWithQuorumQueue()
    {
        var connectionString = RabbitMqTransportFactory.ConnectionString;

        var queueName = TestConfig.GetName("quorum-test");

        Using(new QueueDeleter(queueName));

        var activator = Using(new BuiltinHandlerActivator());
        var gotTheString = new ManualResetEvent(initialState: false);

        activator.Handle<string>(async str => gotTheString.Set());

        Configure.With(activator)
            .Transport(t =>
            {
                t.UseRabbitMq(connectionString, queueName)
                    .InputQueueOptions(q => q.AddArgument("x-queue-type", "quorum"));
            })
            .Start();

        var bus = activator.Bus;

        await bus.SendLocal("HEJ HEJ 😘");

        gotTheString.WaitOrDie(timeout: TimeSpan.FromSeconds(2));
    }

}