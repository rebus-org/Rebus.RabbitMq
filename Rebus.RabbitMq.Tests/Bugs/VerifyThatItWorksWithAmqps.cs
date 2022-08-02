using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

// ReSharper disable AccessToDisposedClosure
#pragma warning disable CS1998

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
[Description("Not really a bug, just played with connecting to an amqps://-enabled broker")]
public class VerifyThatItWorksWithAmqps : FixtureBase
{
    [Test]
    [Explicit]
    public async Task SimpleRoundtrippingTest()
    {
        const string ConnectionStringWithAmqpsScheme = "amqps://(...)";

        using var activator = new BuiltinHandlerActivator();
        using var gotTheMessage = new ManualResetEvent(initialState: false);

        activator.Handle<string>(async _ => gotTheMessage.Set());

        var bus = Configure.With(activator)
            .Transport(t => t.UseRabbitMq(ConnectionStringWithAmqpsScheme, "test-queue"))
            .Start();

        await bus.SendLocal("HEJ");

        gotTheMessage.WaitOrDie(TimeSpan.FromSeconds(5));
    }
}