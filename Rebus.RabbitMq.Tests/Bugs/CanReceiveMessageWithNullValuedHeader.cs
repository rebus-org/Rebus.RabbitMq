using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
// ReSharper disable AccessToDisposedClosure
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
public class CanReceiveMessageWithNullValuedHeader : FixtureBase
{
    [Test]
    public async Task CanDoWhatTheFixtureSays()
    {
        using var nullHeaderSuccessfullyReceived = new ManualResetEvent(false);

        const string headerKey = "custom-header";

        using var activator = new BuiltinHandlerActivator();

        activator.Handle<string>(async (_, context, _) =>
        {
            if (context.Headers[headerKey] == null)
            {
                nullHeaderSuccessfullyReceived.Set();
            }
        });

        var bus = Configure.With(activator)
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, TestConfig.GetName("null-header")))
            .Start();

        await bus.SendLocal("Hej søtte", new Dictionary<string, string> { { headerKey, null } });

        nullHeaderSuccessfullyReceived.WaitOrDie(TimeSpan.FromSeconds(5));
    }
}