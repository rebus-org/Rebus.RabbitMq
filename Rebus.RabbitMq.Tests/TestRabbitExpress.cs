using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Messages;
using Rebus.RabbitMq.Tests.Extensions;
using Rebus.Tests.Contracts;
using Rebus.Transport;

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class TestRabbitExpress : FixtureBase
{
    string _queueName;

    protected override void SetUp()
    {
        _queueName = TestConfig.GetName("expressperf5");

        Using(new QueueDeleter(_queueName));
    }

    [TestCase(100, true)]
    [TestCase(100, false)]
    [TestCase(1000, true)]
    [TestCase(1000, false)]
    public async Task TestPerformance(int messageCount, bool express)
    {
        var receivedMessages = 0L;

        using var activator = new BuiltinHandlerActivator();

        activator.Handle<object>(async _ => Interlocked.Increment(ref receivedMessages));

        var bus = Configure.With(activator)
            .Logging(l => l.ColoredConsole(LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, _queueName))
            .Options(o => o.SetMaxParallelism(100))
            .Start();

        bus.Advanced.Workers.SetNumberOfWorkers(0);
        
        var stopwatch = Stopwatch.StartNew();

        var messages = Enumerable.Range(0, messageCount).Select(i => express ? (object)new ExpressMessage() : new NormalMessage());

        foreach (var batch in messages.Batch(100))
        {
            using var scope = new RebusTransactionScope();

            foreach (var msg in batch)
            {
                await bus.SendLocal(msg);
            }

            await scope.CompleteAsync();
        }

        var elapsedSending = stopwatch.Elapsed;
        stopwatch.Restart();

        var totalSecondsElapsedSending = elapsedSending.TotalSeconds;

        Console.WriteLine("Sent {0} messages in {1:0.0} s - that's {2:0.0} msg/s", messageCount, totalSecondsElapsedSending, messageCount / totalSecondsElapsedSending);

        bus.Advanced.Workers.SetNumberOfWorkers(5);

        while (Interlocked.Read(ref receivedMessages) < messageCount)
        {
            Thread.Sleep(2000);
            Console.Write($"Got {Interlocked.Read(ref receivedMessages)} msg... ");
        }

        Console.WriteLine();

        var elapsedReceiving = stopwatch.Elapsed;
        var totalSecondsElapsedReceiving = elapsedReceiving.TotalSeconds;

        Console.WriteLine("Received {0} messages in {1:0.0} s - that's {2:0.0} msg/s", messageCount, totalSecondsElapsedReceiving, messageCount / totalSecondsElapsedReceiving);
    }

    [Express]
    class ExpressMessage
    {
    }

    class NormalMessage
    {
    }

    class ExpressAttribute : HeaderAttribute
    {
        public ExpressAttribute() : base(Headers.Express, "")
        {
        }
    }
}