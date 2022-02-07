using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
public class CanReceiveMessageWithNullValuedHeader : FixtureBase
{
    [Test]
    public async Task CanDoWhatTheFixtureSays()
    {
        const string headerKey = "custom-header";

        var activator = new BuiltinHandlerActivator();

        Using(activator);

        var nullHeaderSuccessfullyReceived = new ManualResetEvent(false);
            
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

        await bus.SendLocal("Hej søtte", new Dictionary<string, string> {{headerKey, null}});

        nullHeaderSuccessfullyReceived.WaitOrDie(TimeSpan.FromSeconds(5));
    }
}