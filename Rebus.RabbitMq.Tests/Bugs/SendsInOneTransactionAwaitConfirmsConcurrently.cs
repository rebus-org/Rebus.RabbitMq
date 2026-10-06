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

namespace Rebus.RabbitMq.Tests.Bugs;

[TestFixture]
[Description("Messages sent in one transaction must not wait for their publisher confirms one at a time")]
public class SendsInOneTransactionAwaitConfirmsConcurrently : FixtureBase
{
    [Test]
    public async Task TransactionalSendIsNotSlowerThanConcurrentSeparateSends()
    {
        const int count = 500;
        var queueName = TestConfig.GetName("confirm-batch");
        Using(new QueueDeleter(queueName));

        var activator = Using(new BuiltinHandlerActivator());

        Configure.With(activator)
            .Logging(l => l.Console(LogLevel.Warn))
            .Transport(t => t.UseRabbitMq(RabbitMqTransportFactory.ConnectionString, queueName)
                // quorum queues have a higher confirm latency, which makes sequential confirms stand out
                .InputQueueOptions(q => q.AddArgument("x-queue-type", "quorum"))
                .SetPublisherConfirms(enabled: true))
            .Options(o => o.SetNumberOfWorkers(0))
            .Start();

        // warm up the publisher channels, so neither measurement pays for creating them
        await activator.Bus.SendLocal("warm-up");

        var inTransaction = Stopwatch.StartNew();
        using (var scope = new RebusTransactionScope())
        {
            foreach (var n in Enumerable.Range(0, count)) await activator.Bus.SendLocal($"tx {n}");
            await scope.CompleteAsync();
        }
        inTransaction.Stop();

        var separate = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, count).Select(n => activator.Bus.SendLocal($"sep {n}")));
        separate.Stop();

        Console.WriteLine($"in transaction: {inTransaction.Elapsed.TotalMilliseconds:0} ms, separate: {separate.Elapsed.TotalMilliseconds:0} ms");

        Assert.That(inTransaction.Elapsed, Is.LessThanOrEqualTo(separate.Elapsed * 2));
    }
}
