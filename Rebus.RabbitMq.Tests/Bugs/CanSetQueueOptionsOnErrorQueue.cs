using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
public class CanSetQueueOptionsOnErrorQueue : FixtureBase
{
    [Test]
    [Explicit("Can be executed manually to see that it works")]
    public async Task CanDoIt()
    {
        Configure.With(Using(new BuiltinHandlerActivator()))
            .Transport(t =>
            {
                t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, TestConfig.GetName("props"))
                    .InputQueueOptions(b => b.AddArgument("x-queue-type", "quorum"))
                    .DefaultQueueOptions(b => b.AddArgument("x-queue-type", "quorum"));
            })
            .Start();

        await Task.Delay(TimeSpan.FromSeconds(30));
    }
}