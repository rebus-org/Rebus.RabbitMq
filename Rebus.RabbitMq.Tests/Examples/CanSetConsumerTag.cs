using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Tests.Contracts;
using Rebus.Tests.Contracts.Extensions;

// ReSharper disable AccessToDisposedClosure
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Rebus.RabbitMq.Tests.Examples;

[TestFixture]
public class CanSetConsumerTag : FixtureBase
{
    [Test]
    public async Task CanSetIt()
    {
        var queueName = "some-queue";
        Using(new QueueDeleter(queueName));

        var myConsumerTag = "💀";

        using var activator = new BuiltinHandlerActivator();
        using var gotTheMessage = new ManualResetEvent(initialState: false);

        activator.Handle<string>(async str =>
        {
            Console.WriteLine(str);
            gotTheMessage.Set();
        });

        var bus = Configure.With(activator)
            .Transport(t => t.UseRabbitMq(RabbitMqTestContainerManager.GetConnectionString(), queueName).SetConsumerTag(myConsumerTag))
            .Start();

        await bus.SendLocal("HELLO THERE 🚗");

        gotTheMessage.WaitOrDie(TimeSpan.FromSeconds(5));
    }
}