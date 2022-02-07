using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebus.Activation;
using Rebus.Config;
using Rebus.Logging;
using Rebus.Tests.Contracts;
using Rebus.Transport;

#pragma warning disable 1998

namespace Rebus.RabbitMq.Tests;

[TestFixture]
public class SeeHowFastWeCanPublish : FixtureBase
{
    [TestCase(100, true)]
    [TestCase(1000, true)]
    [TestCase(10000, true)]
    [TestCase(100, false)]
    [TestCase(1000, false)]
    [TestCase(10000, false)]
    public async Task SeeHowFast(int count, bool useRebusTransactionScope)
    {
        var activator = new BuiltinHandlerActivator();

        // ignore messages
        activator.Handle<string>(async _ => { });

        Using(activator);

        var queueName = TestConfig.GetName("fast-publish-check");

        Using(new QueueDeleter(queueName));

        var bus = Configure.With(activator)
            .Logging(l => l.Console(LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName))
            .Start();

        var stopwatch = Stopwatch.StartNew();

        if (useRebusTransactionScope)
        {
            using var scope = new RebusTransactionScope();

            await Task.WhenAll(Enumerable.Range(0, count)
                .Select(async n => await bus.SendLocal($"huigehuig3huigehgueisubliminalmessagejiogjeioge{n}hiuhgiuehgiuehgiue")));

            await scope.CompleteAsync();
        }
        else
        {
            await Task.WhenAll(Enumerable.Range(0, count)
                .Select(async n => await bus.SendLocal($"huigehuig3huigehgueisubliminalmessagejiogjeioge{n}hiuhgiuehgiuehgiue")));
        }


        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

        Console.WriteLine($"Sent {count} messages in {elapsedSeconds:0.0} s - that's {count / elapsedSeconds:0.0} msg/s");
    }
}