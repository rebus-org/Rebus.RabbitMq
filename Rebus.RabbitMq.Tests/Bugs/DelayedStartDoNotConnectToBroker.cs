using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Rebus.Config;
using Rebus.Tests.Contracts;

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
public class DelayedStartDoNotConnectToBroker : FixtureBase
{
    [Test]
    public async Task CanDelayStart()
    {
        // non-existent hostname to provoke an error, if it tries to connection anyway
        const string connectionString = "amqp://04a635bf-9bb1-4133-a258-c46f2aec13c5.local";

        var services = new ServiceCollection();

        services.AddRebus(
            configure => configure
                .Transport(t => t.UseRabbitMq(connectionString, "whatever")
                    .Declarations(
                        declareExchanges: false,
                        declareInputQueue: false,
                        bindInputQueue: false
                    ))
        );

        await using var provider = services.BuildServiceProvider();

        provider.StartRebus();
    }
}