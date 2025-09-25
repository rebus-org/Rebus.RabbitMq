using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Tests.Contracts;

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
public class ReproduceIssueWithResumingConsumptionAfterRestart : FixtureBase
{
    [Test]
    [Explicit]
    public async Task RunForVeryLongTime()
    {
        using var bus = Configure.With(new BuiltinHandlerActivator())
            .Logging(l => l.Console(minLevel: LogLevel.Info))
            .Transport(t => t.UseRabbitMq("amqp://localhost", TestConfig.GetName("consumer")))
            .Start();

        await Task.Delay(TimeSpan.FromMinutes(5));
    }
}