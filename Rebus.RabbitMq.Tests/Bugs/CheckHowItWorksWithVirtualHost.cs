using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
[Explicit("Requires a locally running RabbitMQ with a virtual host called 'whatever' or '/whatever'")]
public class CheckHowItWorksWithVirtualHost : FixtureBase
{
    [Test]
    public async Task CanStartBusAndDoStuff()
    {
        using var activator = new BuiltinHandlerActivator();

        activator.Handle<string>(async str => Console.WriteLine(str));

        var bus = Configure.With(activator)
            .Transport(t => t.UseRabbitMq("amqp://localhost/whatever", "whatever"))
            .Start();

        await bus.SendLocal("hej med dig");

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task CanStartBusAndDoStuff2()
    {
        using var activator = new BuiltinHandlerActivator();

        activator.Handle<string>(async str => Console.WriteLine(str));

        var bus = Configure.With(activator)
            .Transport(t => t.UseRabbitMq("amqp://localhost//whatever", "whatever"))
            .Start();

        await bus.SendLocal("hej med dig");

        await Task.Delay(TimeSpan.FromSeconds(2));
    }
}