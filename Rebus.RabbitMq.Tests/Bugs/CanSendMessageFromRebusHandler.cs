using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Routing.TypeBased;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Utilities;
// ReSharper disable ArgumentsStyleAnonymousFunction
#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
[Description("Added after bug report. Issue could not be reproduced though")]
public class CanSendMessageFromRebusHandler : FixtureBase
{
    [Test]
    //[Repeat(100)]
    public async Task ItWorksAsExpected()
    {
        var sender = StartBus(
            queueName: "sender",
            routing: r => r.Map<TheMessage>("middleman")
        );

        StartBus(
            queueName: "middleman",
            handlers: a => a.Handle<TheMessage>(async (bus, msg) => await bus.Send(msg)),
            routing: r => r.Map<TheMessage>("receiver")
        );

        var counter = Using(new SharedCounter(initialValue: 10));

        StartBus("receiver", a => a.Handle<TheMessage>(async _ => counter.Decrement()));

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => sender.Bus.Send(new TheMessage())));

        counter.WaitForResetEvent();
    }

    class TheMessage { }

    BuiltinHandlerActivator StartBus(string queueName, Action<BuiltinHandlerActivator> handlers = null, Action<TypeBasedRouterConfigurationExtensions.TypeBasedRouterConfigurationBuilder> routing = null)
    {
        Using(new QueueDeleter(queueName));

        var activator = Using(new BuiltinHandlerActivator());

        handlers?.Invoke(activator);

        Configure.With(activator)
            .Logging(l => l.Console(minLevel: LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName))
            .Routing(r =>
            {
                var builder = r.TypeBased();
                routing?.Invoke(builder);
            })
            .Start();

        return activator;
    }
}