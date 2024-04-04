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
public class RabbitMqPublisherConfirmsPublishPerformanceTest : FixtureBase
{
    string ConnectionString => RabbitMqTestContainerManager.GetConnectionString();

    /// <summary>
    /// Without confirms: 15508.5 msg/s
    ///
    /// With confirms without transaction
    ///     - initial:                      645 msg/s
    ///     - move call to ConfirmSelect:   815.0 msg/s
    /// 
    /// (The following was done on a different machine, not directly comparable to other results)
    /// With confirms and in transaction
    ///     - initial                       118 msg/s
    ///     - moved outside of send loop:   4315 msg/s 
    /// 
    /// </summary>
    [TestCase(true, 10000)]
    [TestCase(false, 10000)]
    public async Task PublishBunchOfMessages(bool enablePublisherConfirms, int count)
    {
        var queueName = TestConfig.GetName("pub-conf");

        Using(new QueueDeleter(queueName));

        var activator = new BuiltinHandlerActivator();

        Using(activator);

        activator.Handle<string>(async _ => { });

        Configure.With(activator)
            .Logging(l => l.Console(LogLevel.Info))
            .Transport(t => t.UseRabbitMq(ConnectionString, queueName)
                .SetPublisherConfirms(enabled: enablePublisherConfirms))
            .Start();

        // In transaction
        using(var scope = new RebusTransactionScope())
        {
            var stopwatch = Stopwatch.StartNew();

            await Task.WhenAll(Enumerable.Range(0, count)
                .Select(n => $"THIS IS MESSAGE NUMBER {n} OUT OF {count}")
                .Select(str => activator.Bus.SendLocal(str)));

            await scope.CompleteAsync();

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            Console.WriteLine($@"Publishing 

                    {count} 

                messages in transaction with PUBLISHER CONFIRMS = {enablePublisherConfirms} took 

                    {elapsedSeconds:0.0} s

                - that's {count/elapsedSeconds:0.0} msg/s");
        }

        // Without transaction
        var stopwatch2 = Stopwatch.StartNew();

        await Task.WhenAll(Enumerable.Range(0, count)
            .Select(n => $"THIS IS MESSAGE NUMBER {n} OUT OF {count}")
            .Select(str => activator.Bus.SendLocal(str)));

        var elapsedSeconds2 = stopwatch2.Elapsed.TotalSeconds;         

        Console.WriteLine($@"Publishing 

                {count} 

            messages without transaction with PUBLISHER CONFIRMS = {enablePublisherConfirms} took 

                {elapsedSeconds2:0.0} s

            - that's {count/elapsedSeconds2:0.0} msg/s");
    }
}