using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Tests.Contracts;
// ReSharper disable AccessToModifiedClosure
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Rebus.RabbitMq.Tests.Examples;

[TestFixture]
public class VerifyParallelism : FixtureBase
{
    [Test]
    [Description("Timing is a little bit critical in this test. It'll send a bunch of messages to itself and measure how many handlers are being executed in parallel after waiting one second.")]
    public async Task ExecutesHandlersInParallel()
    {
        const int parallelism = 15;

        var queueName = Guid.NewGuid().ToString("N");

        Using(new QueueDeleter(queueName));

        var handlersCurrentlyExecuting = 0L;

        using var activator = new BuiltinHandlerActivator();

        activator.Handle<string>(async _ =>
        {
            try
            {
                Interlocked.Increment(ref handlersCurrentlyExecuting);

                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            finally
            {
                Interlocked.Decrement(ref handlersCurrentlyExecuting);
            }
        });

        var bus = Configure.With(activator)
            .Logging(l => l.Console(minLevel: LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(RabbitMqTestContainerManager.GetConnectionString(), queueName))
            .Options(o => o.SetMaxParallelism(parallelism))
            .Start();

        // send lots of messages
        await Task.WhenAll(Enumerable.Range(0, 3 * parallelism).Select(_ => bus.SendLocal("HEJ")));

        await Task.Delay(TimeSpan.FromSeconds(1));

        var numberOfHandlersCurrentlyRunning = Interlocked.Read(ref handlersCurrentlyExecuting);

        Assert.That(numberOfHandlersCurrentlyRunning, Is.EqualTo(parallelism));
    }
}